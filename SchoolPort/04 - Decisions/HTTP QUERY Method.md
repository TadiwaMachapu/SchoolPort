# Decision: HTTP QUERY Method

---
date: 2026-07-06
status: decided
sprint: .NET 10 upgrade (PR #8)
---

## Context
HTTP QUERY is a proposed IETF standard — like GET but accepts a request body for complex filter parameters. Native in .NET 10 via `HttpMethod.Query`.

## The decision
Use QUERY for complex filter endpoints in Phase 1.5 where query strings are insufficient.

## Where to use QUERY
- Gradebook filter (by class, term, subject, task)
- At-risk learner search (multiple filter criteria)
- Smart Reports filtering (grade, subject, risk level, term)
- Pathways cohort filter (grade, APS range, career goal)
- Mark history search (learner, date range, task type)

## Where NOT to use QUERY
Simple lookups (`GET /api/me`, `GET /api/schools/current`) stay as GET. QUERY is only for operations where the filter is too complex for a query string.

## Implementation
`MapQuery` extension method in `EndpointRouteBuilderExtensions.cs`:
```csharp
public static IEndpointConventionBuilder MapQuery(
    this IEndpointRouteBuilder builder,
    string pattern,
    Delegate handler)
{
    return builder.MapMethods(pattern, [HttpMethods.Query], handler);
}
```

Note: MapQuery convenience helper was deliberately NOT shipped in .NET 10 (only primitives shipped). Our one-liner extension is all that's needed.

## Verified
3 smoke tests confirm QUERY works end-to-end through the real ASP.NET pipeline.

## Related
- [[Sprint 1.5.0 — Security Layer]]
