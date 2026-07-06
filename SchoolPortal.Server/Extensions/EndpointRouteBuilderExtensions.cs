using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SchoolPortal.Server.Extensions;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a QUERY endpoint — safe, idempotent like GET
    /// but accepts a request body for complex filter
    /// parameters. Native in .NET 10 via MapMethods.
    /// Use for gradebook filters, at-risk queries,
    /// Pathways cohort views — anywhere the filter is
    /// too complex for a query string.
    /// </summary>
    public static IEndpointConventionBuilder MapQuery(
        this IEndpointRouteBuilder builder,
        string pattern,
        Delegate handler)
    {
        return builder.MapMethods(
            pattern,
            [HttpMethods.Query],
            handler);
    }
}
