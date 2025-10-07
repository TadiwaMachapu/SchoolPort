using System.Security.Claims;

namespace SchoolPortal.Server.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for auth endpoints
        if (context.Request.Path.StartsWithSegments("/api/auth") || 
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var schoolIdClaim = context.User.FindFirst("schoolId")?.Value;
        
        if (string.IsNullOrEmpty(schoolIdClaim))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "SchoolId claim is missing" });
            return;
        }

        // Store SchoolId in HttpContext.Items for use in services
        if (int.TryParse(schoolIdClaim, out var schoolId))
        {
            context.Items["SchoolId"] = schoolId;
        }

        await _next(context);
    }
}
