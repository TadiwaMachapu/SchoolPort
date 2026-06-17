using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// STEP 5: CurrentUserService reads the per-request resolved set out of the JWT claims.
/// Focus here is IsInScope — holding a position TYPE is not enough; the specific resource id
/// must appear in one of the position's scope entries. Pure in-memory (claim path only).
/// </summary>
public class CurrentUserServiceTests
{
    private static CurrentUserService ServiceFor(string identity, params PositionClaim[] positions)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("schoolId", Guid.NewGuid().ToString()),
            new Claim("identity", identity),
            new Claim("pos", PositionClaim.Serialize(positions)),
        }, "test"));

        var accessor = new HttpContextAccessor { HttpContext = ctx };
        // IsInScope/HasPosition read ActivePositions (from the claim), not catalogue permissions,
        // so an empty catalogue is sufficient here; db is never touched on the claim path.
        var resolver = new PermissionResolver(db: null!, new PermissionCatalogueCache());
        return new CurrentUserService(accessor, resolver);
    }

    private static PositionClaim Scoped(string key, ScopeType type, Guid id) => new()
    {
        Key = key,
        EffectiveFrom = DateTime.UtcNow.AddDays(-1),
        EffectiveTo = null,
        Scopes = new List<ScopeClaim> { new() { ScopeType = type, ScopeRefId = id } },
    };

    [Fact]
    public void IsInScope_TrueForScopedResource_FalseForOthers()
    {
        var classA = Guid.NewGuid();
        var svc = ServiceFor(IdentityKeys.Staff, Scoped(PositionKeys.SubjectTeacher, ScopeType.Class, classA));

        Assert.True(svc.HasPosition(PositionKeys.SubjectTeacher));
        Assert.True(svc.IsInScope(ScopeType.Class, classA));        // exact resource in scope
        Assert.False(svc.IsInScope(ScopeType.Class, Guid.NewGuid())); // a different class
        Assert.False(svc.IsInScope(ScopeType.Subject, classA));       // right id, wrong scope type
    }

    [Fact]
    public void IsInScope_FalseWhenPositionExpired()
    {
        var classA = Guid.NewGuid();
        var expired = new PositionClaim
        {
            Key = PositionKeys.SubjectTeacher,
            EffectiveFrom = DateTime.UtcNow.AddDays(-10),
            EffectiveTo = DateTime.UtcNow.AddDays(-1), // window closed
            Scopes = new List<ScopeClaim> { new() { ScopeType = ScopeType.Class, ScopeRefId = classA } },
        };
        var svc = ServiceFor(IdentityKeys.Staff, expired);

        Assert.False(svc.HasPosition(PositionKeys.SubjectTeacher));
        Assert.False(svc.IsInScope(ScopeType.Class, classA));
    }
}
