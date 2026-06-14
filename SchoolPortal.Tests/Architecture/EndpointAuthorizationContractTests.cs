using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SchoolPortal.Server.Authorization;
using Xunit;

namespace SchoolPortal.Tests.Architecture;

/// <summary>
/// STEP 4 governance: deny-by-default, enforced at build time. Every controller action must
/// make an explicit authorization decision — either it requires a permission
/// (<see cref="RequirePermissionAttribute"/>) or it is deliberately public
/// (<c>[AllowAnonymous]</c> WITH a non-empty <see cref="AnonymousJustificationAttribute"/>).
///
/// This is a reflection scan over the whole Server assembly, so it covers controllers added
/// in any future sprint automatically — there is no hand-maintained list of endpoints to keep
/// in sync. It depends on no database, so it runs in the fast CI job (Category=Architecture)
/// on every PR; a failure is a failing test = non-zero <c>dotnet test</c> = red build, never a
/// warning.
///
/// Migration ratchet: legacy <c>[Authorize(...)]</c> is tolerated ONLY on the controllers in
/// <see cref="LegacyAuthorizeControllers"/> — the exact set that predates the permission model.
/// Step 6 migrates those to <see cref="RequirePermissionAttribute"/> and deletes each entry
/// from the set. A NEW controller is not in the set, so it must use the permission model from
/// day one; and ANY action with no authorization metadata at all fails regardless of the set.
/// </summary>
public class EndpointAuthorizationContractTests
{
    private static readonly Assembly ServerAssembly = typeof(RequirePermissionAttribute).Assembly;

    /// <summary>
    /// Controllers still on the pre-1.5.0 <c>[Authorize]</c> model, tolerated transitionally
    /// until Step 6 migrates them to <c>[RequirePermission]</c>. SHRINKS to empty over Step 6 —
    /// do not add new entries. New controllers must use <c>[RequirePermission]</c> /
    /// justified <c>[AllowAnonymous]</c> instead.
    /// </summary>
    private static readonly IReadOnlySet<string> LegacyAuthorizeControllers = new HashSet<string>
    {
        "ActivitiesController", "AiController",
        "BillingController",
        "ClassSubjectsController", "ClassesController",
        "FeesController", "MatricController",
        "MeController", "ParentController",
        "PathwaysController", "PluginsController", "ProgressController",
        "SchoolsController",
        "SkillsController", "SubjectsController", "SuperAdminController",
        "TermsController", "UsersController",
    };

    private static List<Type> DiscoverControllers() => ServerAssembly.GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic)
        .OrderBy(t => t.Name)
        .ToList();

    /// <summary>Actions are the public, declared, non-special, non-[NonAction] instance methods
    /// carrying an HTTP verb attribute. Every endpoint in this codebase routes via an explicit
    /// [Http*] attribute (the [ApiController] convention requires attribute routing), so verb
    /// presence is the reliable action filter and it excludes private helpers.</summary>
    private static IEnumerable<MethodInfo> DiscoverActions(Type controller) => controller
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName
                 && m.GetCustomAttribute<NonActionAttribute>() is null
                 && m.GetCustomAttributes(inherit: true).OfType<IActionHttpMethodProvider>().Any());

    private static bool Has<T>(MethodInfo action, Type controller) where T : Attribute =>
        action.GetCustomAttributes<T>(inherit: true).Any()
        || controller.GetCustomAttributes<T>(inherit: true).Any();

    private static bool HasNonEmptyJustification(MethodInfo action, Type controller) =>
        action.GetCustomAttributes<AnonymousJustificationAttribute>(inherit: true)
            .Concat(controller.GetCustomAttributes<AnonymousJustificationAttribute>(inherit: true))
            .Any(a => !string.IsNullOrWhiteSpace(a.Reason));

    /// <summary>Plain [Authorize] = an AuthorizeAttribute that is not our RequirePermission
    /// subclass (RequirePermissionAttribute derives from AuthorizeAttribute).</summary>
    private static bool HasPlainAuthorize(MethodInfo action, Type controller) =>
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Any(a => a is not RequirePermissionAttribute);

    [Fact]
    [Trait("Category", "Architecture")]
    public void EveryEndpoint_RequiresPermissionOrJustifiedAnonymous()
    {
        var controllers = DiscoverControllers();

        // Guard the scan itself: a reflection/filter regression must not let the test pass on an
        // empty set (which would silently stop enforcing the contract). Requirement 1.
        Assert.True(controllers.Count >= 30,
            $"Controller discovery found only {controllers.Count}; the assembly scan looks broken.");

        var offenders = new List<string>();
        var totalActions = 0;

        foreach (var controller in controllers)
        {
            var isLegacy = LegacyAuthorizeControllers.Contains(controller.Name);

            foreach (var action in DiscoverActions(controller))
            {
                totalActions++;
                var name = $"{controller.Name}.{action.Name}";

                if (Has<AllowAnonymousAttribute>(action, controller))
                {
                    // Deliberately public: must say WHY, in a machine-readable attribute.
                    if (!HasNonEmptyJustification(action, controller))
                        offenders.Add($"{name} — [AllowAnonymous] without a non-empty [AnonymousJustification].");
                    continue;
                }

                if (Has<RequirePermissionAttribute>(action, controller))
                    continue; // permission model — compliant

                if (HasPlainAuthorize(action, controller))
                {
                    if (!isLegacy)
                        offenders.Add($"{name} — uses legacy [Authorize]; new controllers must use [RequirePermission].");
                    continue; // legacy controller, tolerated until Step 6
                }

                // No authorization metadata at all. Requirement 4: this is a hard failure.
                offenders.Add($"{name} — NO authorization attribute (deny-by-default violation): " +
                              "add [RequirePermission(...)] or [AllowAnonymous]+[AnonymousJustification].");
            }
        }

        Assert.True(totalActions > 0, "No controller actions were discovered — the action filter looks broken.");

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} endpoint(s) violate the STEP 4 authorization contract:\n  - "
            + string.Join("\n  - ", offenders));
    }

    /// <summary>Keeps the ratchet honest: a legacy entry whose controller no longer exists (or was
    /// renamed/migrated away) is stale and must be removed, so the set only ever shrinks toward
    /// empty as Step 6 progresses.</summary>
    [Fact]
    [Trait("Category", "Architecture")]
    public void LegacyAuthorizeAllowlist_HasNoStaleEntries()
    {
        var existing = DiscoverControllers().Select(t => t.Name).ToHashSet();
        var stale = LegacyAuthorizeControllers.Where(name => !existing.Contains(name)).ToList();

        Assert.True(stale.Count == 0,
            "Stale LegacyAuthorizeControllers entries (controller no longer exists — remove them):\n  - "
            + string.Join("\n  - ", stale));
    }
}
