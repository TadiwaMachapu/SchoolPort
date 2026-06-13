using Microsoft.AspNetCore.Authorization;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Encodes a permission key into an authorization policy name and back, so we never have to
/// pre-register a named policy per permission. The "perm:" prefix is the contract between
/// <see cref="RequirePermissionAttribute"/> and <see cref="PermissionPolicyProvider"/>.
/// </summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string NameFor(string permissionKey) => Prefix + permissionKey;

    public static bool TryGetKey(string policyName, out string permissionKey)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            permissionKey = policyName[Prefix.Length..];
            return permissionKey.Length > 0;
        }

        permissionKey = string.Empty;
        return false;
    }
}

/// <summary>The single permission an endpoint demands (STEP 4). Satisfied by
/// <see cref="PermissionAuthorizationHandler"/>.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }

    public PermissionRequirement(string permissionKey) => PermissionKey = permissionKey;
}
