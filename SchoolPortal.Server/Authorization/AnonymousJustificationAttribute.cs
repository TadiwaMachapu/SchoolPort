namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Records WHY an endpoint is deliberately exposed without authorization (STEP 4). Required
/// alongside every <c>[AllowAnonymous]</c> on a controller action: a code comment is invisible
/// to the controller-scan governance test, so the justification lives in an attribute the test
/// can read. The reason is surfaced in code review and the test fails the build if an
/// <c>[AllowAnonymous]</c> endpoint carries no non-empty justification.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AnonymousJustificationAttribute : Attribute
{
    public string Reason { get; }

    public AnonymousJustificationAttribute(string reason) => Reason = reason;
}
