using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Middleware;
using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Middleware;

/// <summary>
/// STEP 5 critical-path guards for TenantMiddleware: the existing schoolId handling must be
/// preserved, and the new permission-resolution trigger must FAIL CLOSED (deny, never pass
/// through with an unresolved set). Pure in-memory — no database.
/// </summary>
public class TenantMiddlewareTests
{
    private static HttpContext ContextWith(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Path = "/api/anything"; // not in the skip list
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return ctx;
    }

    private static TenantMiddleware Middleware(RequestDelegate next) =>
        new(next, NullLogger<TenantMiddleware>.Instance);

    [Fact]
    public async Task ValidSchoolId_PassesThrough_AndStoresTenant_EmptyPositionsOk()
    {
        var schoolId = Guid.NewGuid();
        var nextCalled = false;
        var mw = Middleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = ContextWith(new Claim("schoolId", schoolId.ToString()));

        // A Learner/Parent (or any user) with no positions resolves to an empty/identity-only
        // set — that is a normal request, not a failure.
        await mw.InvokeAsync(ctx, new StubCurrentUser());

        Assert.True(nextCalled);
        Assert.Equal(schoolId, ctx.Items["SchoolId"]);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task MissingSchoolId_Returns401_AndDoesNotCallNext()
    {
        var nextCalled = false;
        var mw = Middleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = ContextWith(); // no schoolId claim

        await mw.InvokeAsync(ctx, new StubCurrentUser());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolutionThrows_FailsClosed_403_AndDoesNotCallNext()
    {
        var nextCalled = false;
        var mw = Middleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = ContextWith(new Claim("schoolId", Guid.NewGuid().ToString()));

        await mw.InvokeAsync(ctx, new StubCurrentUser { ThrowOnResolve = true });

        Assert.False(nextCalled); // never reached the controller
        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    /// <summary>Minimal ICurrentUserService double: GetEffectivePermissions either returns an
    /// empty set or throws, to drive the middleware's two branches.</summary>
    private sealed class StubCurrentUser : ICurrentUserService
    {
        public bool ThrowOnResolve { get; set; }
        public Guid SchoolId => Guid.NewGuid();
        public Guid UserId => Guid.NewGuid();
        public bool IsAuthenticated => true;
        public string Identity => IdentityKeys_Staff;
        private const string IdentityKeys_Staff = "Staff";

#pragma warning disable CS0618 // implementing the obsolete-but-required interface member
        public string Role => "Teacher";
#pragma warning restore CS0618

        public bool HasPermission(string permissionKey) => false;
        public bool HasPosition(string positionKey) => false;
        public bool IsInScope(ScopeType type, Guid scopeId) => false;

        public IReadOnlySet<string> GetEffectivePermissions() =>
            ThrowOnResolve
                ? throw new InvalidOperationException("permission catalogue unavailable")
                : new HashSet<string>();
    }
}
