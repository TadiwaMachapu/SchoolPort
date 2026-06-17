using Microsoft.AspNetCore.Authorization;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Declares that an endpoint (or controller) requires a single permission key (STEP 4).
/// The key is carried inside the authorization policy name (<see cref="PermissionPolicy"/>);
/// <see cref="PermissionPolicyProvider"/> materialises a one-requirement policy on demand and
/// <see cref="PermissionAuthorizationHandler"/> enforces it — routine permissions from the
/// per-request JWT set, sensitive permissions and External/System identities re-resolved from
/// the database (the Section C trust rule). Always pass a <see cref="PermissionKeys"/> constant;
/// never hand-type the permission string.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public string PermissionKey { get; }

    public RequirePermissionAttribute(string permissionKey)
    {
        PermissionKey = permissionKey;
        Policy = PermissionPolicy.NameFor(permissionKey);
    }
}
