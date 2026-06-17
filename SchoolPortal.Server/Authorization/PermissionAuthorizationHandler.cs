using Microsoft.AspNetCore.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Enforces a <see cref="PermissionRequirement"/> (STEP 4 Section C). Two paths, one rule:
/// <list type="bullet">
/// <item>Routine permissions are satisfied from the per-request JWT-resolved set
/// (zero DB hits; window/expiry already enforced by <see cref="PermissionResolver"/>).</item>
/// <item>Sensitive permissions and ALL External/System identities bypass the JWT cache
/// entirely and re-resolve positions from the database at call time — the token's position
/// cache is never trusted for these.</item>
/// </list>
/// Deny-by-default: the requirement is only marked succeeded on an affirmative grant; every
/// other path (unauthenticated, no permission, missing context) simply falls through to deny.
/// Registered as scoped because it depends on the scoped <see cref="PermissionResolver"/>
/// (which owns the request DbContext).
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>HttpContext.Items key for the DB-authority resolved set. Kept distinct from the
    /// routine JWT cache (<see cref="CurrentUserService.EffectivePermissionsItemKey"/>) so a
    /// request mixing routine and sensitive checks hits the database at most once.</summary>
    public const string DbResolvedItemKey = "EffectivePermissions.DbAuthority";

    private readonly ICurrentUserService _currentUser;
    private readonly PermissionResolver _resolver;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuthorizationHandler(
        ICurrentUserService currentUser,
        PermissionResolver resolver,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentUser = currentUser;
        _resolver = resolver;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!(context.User.Identity?.IsAuthenticated ?? false))
            return; // unauthenticated → deny (the policy also RequireAuthenticatedUser)

        var identity = _currentUser.Identity;

        var granted = PermissionResolver.RequiresDatabaseResolution(requirement.PermissionKey, identity)
            ? await GrantedFromDatabaseAsync(identity, requirement.PermissionKey)
            : _currentUser.HasPermission(requirement.PermissionKey);

        if (granted)
            context.Succeed(requirement);
    }

    /// <summary>Database authority path: re-read positions for the current user/school and
    /// derive permissions, ignoring the JWT cache. Cached per request under its own key.</summary>
    private async Task<bool> GrantedFromDatabaseAsync(string identity, string permissionKey)
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null)
            return false;

        if (http.Items[DbResolvedItemKey] is not EffectivePermissionSet resolved)
        {
            resolved = await _resolver.ResolveFromDatabaseAsync(
                _currentUser.UserId, _currentUser.SchoolId, identity, DateTime.UtcNow);
            http.Items[DbResolvedItemKey] = resolved;
        }

        return resolved.HasPermission(permissionKey);
    }
}
