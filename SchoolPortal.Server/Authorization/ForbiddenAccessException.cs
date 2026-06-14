namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Thrown by the scope layer (Sprint 1.5.0 Step 7) when an authenticated user attempts to act on
/// a specific resource outside their scope (IDOR). Mapped to HTTP 403 by ExceptionMiddleware.
/// Used on load-then-mutate paths where an explicit "forbidden" reads better than a silent 404;
/// pure list/read queries instead filter the resource out (→ empty / 404).
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "You do not have access to this resource.")
        : base(message) { }
}
