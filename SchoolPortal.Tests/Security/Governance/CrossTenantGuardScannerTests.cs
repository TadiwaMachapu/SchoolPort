using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.Governance;

/// <summary>
/// Step 10 (step 3) — the durable H1-prevention ratchet. 15/15 cross-tenant write gaps found in
/// Inventory B were body/route ids with no SchoolId validation, so this WILL recur. This scanner
/// enumerates every mutating action (POST/PUT/PATCH/DELETE) that accepts an id (a Guid route/query
/// parameter, or a [FromBody] DTO with a Guid property ending in "Id") and FAILS the build unless the
/// endpoint is either covered by a [CrossTenantGuard]-registered test or listed in the justified
/// exemption set below. Same shape as the permission governance ratchet — a new id-bearing mutating
/// endpoint cannot ship without a cross-tenant rejection test.
/// </summary>
public class CrossTenantGuardScannerTests
{
    /// <summary>
    /// Endpoints that take a Guid id but are NOT tenant-bound, with justification. Mirrors the
    /// AnonymousJustification pattern — every entry is a reviewed decision, not a silent skip.
    /// </summary>
    private static readonly IReadOnlySet<string> Exempt = new HashSet<string>
    {
        // SuperAdmin is the platform-level (D3) role operating ACROSS schools by design — its ids are
        // not tenant-bound and it sits outside the per-school identity/permission model.
        "SuperAdminController.UpdateFeatures",
        "SuperAdminController.SetStatus",
        // Plugins are a GLOBAL marketplace (not school-owned). install/uninstall target a public plugin
        // by its global id and stamp/scope the per-school installation; dispatch is school-scoped.
        "PluginsController.Install",
        "PluginsController.Uninstall",
        // School-level settings write: takes no resource id and updates the CALLER'S OWN school. The
        // scanner flags it only because the settings jsonb blob nests AcademicTerm.TermId — opaque blob
        // data, not a cross-tenant FK reference. No foreign id can be forged here.
        "SchoolsController.UpdateSettings",
    };

    /// <summary>
    /// STEP 10 BURN-DOWN BACKLOG. Id-bearing mutating endpoints not yet covered by a cross-tenant guard
    /// test — the surface the 5-cluster manual pass (Academics/Teaching/Finance/Comms/Admin) did not
    /// reach. The scanner still FAILS for any NEW endpoint (or any not in this list), so this can only
    /// shrink. Each entry must be either guard-tested (move to a [CrossTenantGuard]) or, if genuinely
    /// not tenant-bound, exempted with justification. Tracked for completion before Step 11 closeout.
    /// NOTE: several are code-verified-guarded but lack a test (e.g. Assignments create/update,
    /// Gradebook SetCategories, Quizzes publish/start, Positions update/revoke, Users delete, Grades
    /// bulk); others are NOT YET AUDITED and are real gap-risk (Courses ×9, Activities ×4, Skills,
    /// Progress, Notifications, Pathways goals, Reports) — these were built in the pre-security era.
    /// </summary>
    // BURN-DOWN COMPLETE (Step 10). Every id-bearing mutating endpoint is now either covered by a
    // [CrossTenantGuard] test or justified-exempt. The backlog is empty: the scanner is a pure ratchet
    // — any NEW id-bearing mutating endpoint must arrive with a guard test (or exemption) or the build fails.
    private static readonly IReadOnlySet<string> KnownUncoveredBacklog = new HashSet<string>();

    private static readonly Type[] MutatingVerbs =
    {
        typeof(HttpPostAttribute), typeof(HttpPutAttribute), typeof(HttpPatchAttribute), typeof(HttpDeleteAttribute),
    };

    [Fact]
    public void EveryIdBearingMutatingEndpoint_HasACrossTenantGuardTest()
    {
        var requiresGuard = DiscoverIdBearingMutatingEndpoints();
        var registered = DiscoverRegisteredGuards();

        var uncovered = requiresGuard
            .Where(e => !registered.Contains(e) && !Exempt.Contains(e) && !KnownUncoveredBacklog.Contains(e))
            .OrderBy(e => e)
            .ToList();

        Assert.True(uncovered.Count == 0,
            $"{uncovered.Count} id-bearing mutating endpoint(s) have no [CrossTenantGuard] test and are not exempted/backlogged. " +
            "Add a cross-tenant rejection test (foreign id → 404/403, no row mutated) marked with " +
            "[CrossTenantGuard(typeof(XController), nameof(XController.Action))], or add a justified exemption:\n  " +
            string.Join("\n  ", uncovered));

        // The backlog can only shrink: an entry that no longer maps to a real id-bearing endpoint
        // (renamed/removed/now-covered) is stale and must be cleaned up — keeps the ratchet honest.
        var stale = KnownUncoveredBacklog
            .Where(e => !requiresGuard.Contains(e) || registered.Contains(e))
            .OrderBy(e => e).ToList();
        Assert.True(stale.Count == 0,
            "Stale burn-down backlog entries (no longer an uncovered id-bearing endpoint — remove them):\n  " +
            string.Join("\n  ", stale));
    }

    /// <summary>Diagnostic: the full set the scanner considers, for the Step 10 report.</summary>
    [Fact]
    public void Report_IdBearingMutatingEndpoints()
    {
        var all = DiscoverIdBearingMutatingEndpoints().OrderBy(e => e).ToList();
        var registered = DiscoverRegisteredGuards();
        var lines = all.Select(e =>
            $"{e}  ->  {(registered.Contains(e) ? "GUARDED" : Exempt.Contains(e) ? "EXEMPT" : "UNCOVERED")}");
        // Always passes; surfaces the inventory in test output.
        Assert.True(all.Count >= 0, "Id-bearing mutating endpoints:\n" + string.Join("\n", lines));
    }

    private static List<string> DiscoverIdBearingMutatingEndpoints()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var result = new List<string>();
        foreach (var controller in controllers)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!method.GetCustomAttributes().Any(a => MutatingVerbs.Contains(a.GetType()))) continue;
                if (HasTenantIdInput(method)) result.Add($"{controller.Name}.{method.Name}");
            }
        }
        return result;
    }

    private static bool HasTenantIdInput(MethodInfo method)
    {
        foreach (var p in method.GetParameters())
        {
            // Route/query Guid id (e.g. Guid id, Guid classId).
            if (IsGuid(p.ParameterType)) return true;

            // [FromBody] DTO carrying a Guid "...Id" property — INCLUDING ids nested in collections
            // (the bulk endpoints: BulkXRequest.Items[].ClassId), which a top-level-only scan would
            // miss. Recurse into element types and nested complex types. This is what lets the scanner
            // catch H1's own shape (ClassSubjects bulk-assign).
            if (p.GetCustomAttribute<FromBodyAttribute>() is not null || IsComplex(p.ParameterType))
            {
                if (TypeCarriesTenantId(p.ParameterType, depth: 4, new HashSet<Type>())) return true;
            }
        }
        return false;
    }

    private static bool TypeCarriesTenantId(Type type, int depth, HashSet<Type> visited)
    {
        if (depth <= 0 || !visited.Add(type)) return false;
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var pt = prop.PropertyType;
            if (IsGuid(pt) && prop.Name.EndsWith("Id", StringComparison.Ordinal)) return true;

            var element = GetEnumerableElementType(pt);
            if (element is not null)
            {
                if (IsComplex(element) && TypeCarriesTenantId(element, depth - 1, visited)) return true;
            }
            else if (IsComplex(pt) && TypeCarriesTenantId(pt, depth - 1, visited))
            {
                return true;
            }
        }
        return false;
    }

    private static Type? GetEnumerableElementType(Type t)
    {
        if (t == typeof(string)) return null;
        if (t.IsArray) return t.GetElementType();
        var ie = t.GetInterfaces().Concat(new[] { t })
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return ie?.GetGenericArguments()[0];
    }

    // A complex (non-primitive, non-framework) type — treated as a request body / nested DTO worth
    // recursing into. ASP.NET also binds complex parameters from the body by default.
    private static bool IsComplex(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u.IsPrimitive || u.IsEnum) return false;
        if (u == typeof(string) || u == typeof(Guid) || u == typeof(DateTime) || u == typeof(DateTimeOffset)
            || u == typeof(decimal) || u == typeof(TimeSpan)) return false;
        if (u.Namespace is not null && (u.Namespace.StartsWith("System") || u.Namespace.StartsWith("Microsoft"))) return false;
        return u.IsClass || u.IsValueType;
    }

    private static bool IsGuid(Type t) => t == typeof(Guid) || Nullable.GetUnderlyingType(t) == typeof(Guid);

    private static HashSet<string> DiscoverRegisteredGuards()
    {
        return typeof(CrossTenantGuardScannerTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetCustomAttributes<CrossTenantGuardAttribute>())
            .Select(a => a.Key)
            .ToHashSet();
    }
}
