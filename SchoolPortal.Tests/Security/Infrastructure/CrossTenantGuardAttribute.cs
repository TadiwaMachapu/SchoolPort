namespace SchoolPortal.Tests.Security.Infrastructure;

/// <summary>
/// Step 10 governance: marks a test as the cross-tenant write guard for a specific mutating endpoint.
/// The <see cref="SchoolPortal.Tests.Security.Governance.CrossTenantGuardScannerTests"/> scanner
/// enumerates every id-bearing POST/PUT/PATCH/DELETE action and FAILS the build unless each is either
/// registered here or explicitly exempted — the same ratchet pattern as the permission governance test.
/// A new id-bearing mutating endpoint therefore cannot ship without a cross-tenant rejection test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CrossTenantGuardAttribute : Attribute
{
    public Type Controller { get; }
    public string Action { get; }
    public CrossTenantGuardAttribute(Type controller, string action)
    {
        Controller = controller;
        Action = action;
    }

    /// <summary>"ControllerTypeName.ActionMethodName" — the key the scanner matches on.</summary>
    public string Key => $"{Controller.Name}.{Action}";
}
