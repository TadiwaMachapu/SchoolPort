using System.Security.Claims;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;

namespace SchoolPortal.Server.Services;

public class CurrentUserService : ICurrentUserService
{
    /// <summary>HttpContext.Items key for the per-request resolved permission set —
    /// resolution happens at most once per request (STEP 3 Section A step 4).</summary>
    public const string EffectivePermissionsItemKey = "EffectivePermissions";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PermissionResolver _resolver;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, PermissionResolver resolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _resolver = resolver;
    }

    public Guid SchoolId
    {
        get
        {
            var schoolIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("schoolId")?.Value;
            return Guid.TryParse(schoolIdClaim, out var schoolId) ? schoolId : Guid.Empty;
        }
    }

    public Guid UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string Identity
    {
        get
        {
            var identityClaim = _httpContextAccessor.HttpContext?.User.FindFirst("identity")?.Value;
            if (!string.IsNullOrEmpty(identityClaim)) return identityClaim;

            // Transition fallback for pre-1.5.0 tokens that carry only the legacy role claim.
            var roleClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
            return roleClaim switch
            {
                "Admin" or "Teacher" => IdentityKeys.Staff,
                "Student" => IdentityKeys.Learner,
                "Parent" => IdentityKeys.Parent,
                _ => string.Empty,
            };
        }
    }

    public bool HasPermission(string permissionKey) => GetResolved().HasPermission(permissionKey);

    public bool HasPosition(string positionKey) => GetResolved().HasPosition(positionKey);

    public bool IsInScope(ScopeType type, Guid scopeId) =>
        GetResolved().ActivePositions.Any(p =>
            p.Scopes.Any(s => s.ScopeType == type && s.ScopeRefId == scopeId));

    public IReadOnlySet<string> GetEffectivePermissions() => GetResolved().Permissions;

    /// <summary>Per-request resolve-once: JWT claim path (0 DB hits, expiry enforced
    /// against the server clock), cached in HttpContext.Items.</summary>
    private EffectivePermissionSet GetResolved()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return EffectivePermissionSet.Empty(string.Empty);

        if (ctx.Items[EffectivePermissionsItemKey] is EffectivePermissionSet cached)
            return cached;

        var positions = PositionClaim.Parse(ctx.User.FindFirst("pos")?.Value);
        var resolved = _resolver.ResolveFromClaims(Identity, positions, DateTime.UtcNow);
        ctx.Items[EffectivePermissionsItemKey] = resolved;
        return resolved;
    }
}
