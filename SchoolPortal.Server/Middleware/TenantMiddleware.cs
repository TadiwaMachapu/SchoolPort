using System.Security.Claims;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // ICurrentUserService is scoped, so it is injected per-request as a method parameter
    // rather than into the (singleton) constructor.
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        // Skip for auth endpoints, super-admin endpoints, health checks, and swagger
        if (context.Request.Path.StartsWithSegments("/api/auth") ||
            context.Request.Path.StartsWithSegments("/api/super") ||
            context.Request.Path.StartsWithSegments("/api/dev") ||
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var schoolIdClaim = context.User.FindFirst("schoolId")?.Value;

        if (string.IsNullOrEmpty(schoolIdClaim) ||
            !Guid.TryParse(schoolIdClaim, out var schoolId) ||
            schoolId == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "SchoolId claim is missing or invalid" });
            return;
        }

        context.Items["SchoolId"] = schoolId;

        // STEP 5: resolve the effective permission set ONCE per request and cache it in
        // HttpContext.Items (CurrentUserService does the caching). This is the routine JWT
        // fast path — zero DB hits; sensitive permissions and External/System identities are
        // re-resolved from the database later in the authorization handler
        // (PermissionResolver.RequiresDatabaseResolution), not here, so we never touch the DB
        // on the critical path for routine requests.
        //
        // Fail CLOSED: if resolution throws (e.g. the permission catalogue is unavailable),
        // deny the request rather than letting it proceed with an unresolved permission set.
        try
        {
            _ = currentUser.GetEffectivePermissions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Permission resolution failed for user {UserId} in school {SchoolId}; failing closed",
                currentUser.UserId, schoolId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Authorisation could not be resolved" });
            return;
        }

        await _next(context);
    }
}
