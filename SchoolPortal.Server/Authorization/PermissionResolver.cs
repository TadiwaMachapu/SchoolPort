using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// A user's resolved authorisation state for one request: active positions (with scopes)
/// and the derived effective permission set.
/// </summary>
public sealed class EffectivePermissionSet
{
    public required string Identity { get; init; }
    public required IReadOnlyList<PositionClaim> ActivePositions { get; init; }
    public required IReadOnlySet<string> Permissions { get; init; }

    public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
    public bool HasPosition(string positionKey) => ActivePositions.Any(p => p.Key == positionKey);

    public static EffectivePermissionSet Empty(string identity) => new()
    {
        Identity = identity,
        ActivePositions = Array.Empty<PositionClaim>(),
        Permissions = new HashSet<string>(),
    };
}

/// <summary>
/// Computes a user's effective permissions (STEP 3 Section A):
/// active in-window positions → union of catalogue permissions → plus identity-implicit.
///
/// Two paths with one trust rule (Section C):
/// - <see cref="ResolveFromClaims"/> — routine ops; positions come from the JWT "pos"
///   claim; zero DB hits. Expiry is still enforced here from the claim's own dates.
/// - <see cref="ResolveFromDatabaseAsync"/> — sensitive ops (PermissionKeys.Sensitive)
///   and ALL External/System identity requests; positions re-read from the database in
///   a single query; the JWT position cache is ignored entirely.
/// Per-request caching in HttpContext.Items is done by the caller (CurrentUserService /
/// the Step 4 handler), not here — this class is deliberately stateless.
/// </summary>
public class PermissionResolver
{
    private readonly SchoolPortalDbContext _db;
    private readonly PermissionCatalogueCache _catalogue;

    public PermissionResolver(SchoolPortalDbContext db, PermissionCatalogueCache catalogue)
    {
        _db = db;
        _catalogue = catalogue;
    }

    /// <summary>JWT fast path — 0 DB round trips. Expired/not-yet-effective positions in
    /// the token grant nothing: the window check runs against the server clock now, not
    /// at login.</summary>
    public EffectivePermissionSet ResolveFromClaims(
        string identity, IReadOnlyList<PositionClaim> tokenPositions, DateTime utcNow)
    {
        var active = tokenPositions.Where(p => p.IsActiveAt(utcNow)).ToList();
        return Build(identity, active);
    }

    /// <summary>Database authority path — exactly 1 query (positions + scopes projected
    /// together; the permission union uses the in-memory catalogue, no joins at request
    /// time). Used for sensitive operations and External/System identities.</summary>
    public async Task<EffectivePermissionSet> ResolveFromDatabaseAsync(
        Guid userId, Guid schoolId, string identity, DateTime utcNow)
    {
        var rows = await _db.UserPositions.AsNoTracking()
            .Where(up => up.UserId == userId
                      && up.SchoolId == schoolId      // tenant boundary: school A grants nothing in school B
                      && up.IsActive
                      && up.EffectiveFrom <= utcNow
                      && (up.EffectiveTo == null || up.EffectiveTo >= utcNow))
            .Select(up => new PositionClaim
            {
                Key = up.Position.Key,
                EffectiveFrom = up.EffectiveFrom,
                EffectiveTo = up.EffectiveTo,
                Scopes = up.Scopes.Select(s => new ScopeClaim
                {
                    ScopeType = s.ScopeType,
                    ScopeRefId = s.ScopeRefId,
                    ScopeValue = s.ScopeValue,
                }).ToList(),
            })
            .ToListAsync();

        return Build(identity, rows);
    }

    /// <summary>True when a check on this permission/identity must bypass the JWT cache
    /// and re-resolve from the database (Section C).</summary>
    public static bool RequiresDatabaseResolution(string permissionKey, string identity) =>
        PermissionKeys.Sensitive.Contains(permissionKey)
        || identity == IdentityKeys.External
        || identity == IdentityKeys.System;

    private EffectivePermissionSet Build(string identity, IReadOnlyList<PositionClaim> activePositions)
    {
        var permissions = new HashSet<string>();

        foreach (var position in activePositions)
            permissions.UnionWith(_catalogue.GetPermissions(position.Key));

        if (PermissionKeys.IdentityImplicit.TryGetValue(identity, out var implicitSet))
            permissions.UnionWith(implicitSet);

        return new EffectivePermissionSet
        {
            Identity = identity,
            ActivePositions = activePositions,
            Permissions = permissions,
        };
    }
}
