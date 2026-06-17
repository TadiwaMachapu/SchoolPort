using Microsoft.AspNetCore.Authorization;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Platform-level authorization for super-admin endpoints (STEP 6 D3). SuperAdmin operates the
/// platform across ALL schools and sits OUTSIDE the per-school identity / positions / permissions
/// model — so these endpoints deliberately do NOT use <see cref="RequirePermissionAttribute"/>
/// (which resolves a school's positions and would be meaningless cross-tenant). Enforcement is via
/// the "SuperAdmin" role claim issued by the separate super-admin login. Pair with
/// <see cref="AnonymousJustificationAttribute"/> so the governance test records why the endpoint
/// is exempt from the school permission model.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireSuperAdminAttribute : AuthorizeAttribute
{
    public RequireSuperAdminAttribute() => Roles = "SuperAdmin";
}
