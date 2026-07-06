using System.Net;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security;

/// <summary>
/// .NET 10 upgrade smoke test: proves the HTTP QUERY method works end-to-end through the
/// real pipeline (routing → deny-by-default fallback policy → handler) via the MapQuery
/// extension, before Phase 1.5 builds real QUERY filter endpoints (gradebook filters,
/// at-risk queries, Pathways cohort views). The endpoint under test is mapped in the
/// Testing environment ONLY (Program.cs) — it does not exist in prod/dev.
/// </summary>
[Collection("SecurityApi")]
public class QueryMethodSmokeTests
{
    private static readonly HttpMethod Query = new("QUERY");

    private readonly ApiFactory _factory;

    public QueryMethodSmokeTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task QueryEndpoint_AuthenticatedHolder_Returns200()
    {
        var user = await _factory.MintTokenAsync(Guid.NewGuid(), "Staff");
        using var client = _factory.ClientFor(user);

        using var request = new HttpRequestMessage(Query, "/api/_smoke/query");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"method\":\"QUERY\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task QueryEndpoint_NoToken_Returns401()
    {
        using var client = _factory.AnonymousClient();

        using var request = new HttpRequestMessage(Query, "/api/_smoke/query");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QueryEndpoint_GetVerb_IsNotRouted()
    {
        // The route is QUERY-only: a GET to the same pattern must not match (405/404, never 200).
        var user = await _factory.MintTokenAsync(Guid.NewGuid(), "Staff");
        using var client = _factory.ClientFor(user);

        using var response = await client.GetAsync("/api/_smoke/query");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
