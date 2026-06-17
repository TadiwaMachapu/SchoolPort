using System.Net;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security;

/// <summary>
/// Step 10 foundation proof: the WebApplicationFactory harness boots the real API, mints a real
/// token via the production AuthService.LoginAsync path, and the auth pipeline behaves —
/// holder → 200, no token → 401. Everything in the security suite builds on this.
/// </summary>
[Collection("SecurityApi")]
public class HarnessSmokeTests
{
    private readonly ApiFactory _api;
    public HarnessSmokeTests(ApiFactory api) => _api = api;

    [Fact]
    public async Task Harness_MintsRealToken_AndMeReturns200ForHolder()
    {
        // platform.access is identity-implicit for Staff — no position needed.
        var staff = await _api.MintTokenAsync(Guid.NewGuid(), "Staff");
        Assert.False(string.IsNullOrWhiteSpace(staff.AccessToken));      // real token issued
        Assert.True(staff.ExpiresAt > DateTime.UtcNow);                  // real (future) TTL

        var resp = await _api.ClientFor(staff).GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);                // holder → 200
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var resp = await _api.AnonymousClient().GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);      // no token → 401
    }
}
