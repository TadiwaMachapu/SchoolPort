using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// In-process cache of the seeded position→permission map (STEP 3 Section F). The
/// catalogue is immutable at runtime (seeded in code, not user-editable), so it is
/// loaded once at startup and every request-time permission union is pure in-memory —
/// the JWT fast path costs zero DB round trips and there is no N+1 anywhere.
/// </summary>
public sealed class PermissionCatalogueCache
{
    private FrozenDictionary<string, FrozenSet<string>> _permissionsByPosition =
        FrozenDictionary<string, FrozenSet<string>>.Empty;

    public bool IsLoaded { get; private set; }

    public async Task LoadAsync(SchoolPortalDbContext db)
    {
        var rows = await db.PositionPermissions.AsNoTracking()
            .Select(pp => new { Position = pp.Position.Key, Permission = pp.Permission.Key })
            .ToListAsync();

        _permissionsByPosition = rows
            .GroupBy(r => r.Position)
            .ToFrozenDictionary(
                g => g.Key,
                g => g.Select(r => r.Permission).ToFrozenSet());

        IsLoaded = true;
    }

    /// <summary>Permissions for a position key. Unknown keys yield the empty set —
    /// an unrecognised position grants nothing (deny by default).</summary>
    public IReadOnlySet<string> GetPermissions(string positionKey) =>
        _permissionsByPosition.TryGetValue(positionKey, out var set)
            ? set
            : FrozenSet<string>.Empty;
}
