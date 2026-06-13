using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Materialises a one-requirement authorization policy on demand for every "perm:{key}"
/// policy name emitted by <see cref="RequirePermissionAttribute"/>, so the application never
/// has to register a named policy per permission. Non-permission policy names plus the
/// default and fallback policies delegate to the framework's
/// <see cref="DefaultAuthorizationPolicyProvider"/> (which is where the deny-by-default
/// fallback policy configured in Program.cs comes from).
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionPolicy.TryGetKey(policyName, out var permissionKey))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissionKey))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
