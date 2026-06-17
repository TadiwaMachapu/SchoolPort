using SchoolPortal.Server.Authorization;

namespace SchoolPortal.Server.Seeds;

/// <summary>
/// Sprint 1.5.0 Step 9 — onboarding "school size" presets. Each maps to the DEFAULT set of
/// positions a school of that size typically uses. ADVISORY ONLY: the preset seeds
/// <c>SchoolSettings.EnabledPositionKeys</c> as a starting point — the position UI / staff CSV may
/// still assign ANY catalogue position regardless of preset (D2). External/System positions
/// (Auditor/DistrictOfficial/SystemSupport) are situational and never part of a size preset.
/// </summary>
public static class SizePresets
{
    public const string Compact = "Compact";
    public const string Standard = "Standard";
    public const string Large = "Large";

    public static readonly IReadOnlyList<string> Names = new[] { Compact, Standard, Large };

    // Compact: finance collapsed to one manager, no middle-management tiers.
    private static readonly string[] CompactKeys =
    {
        PositionKeys.Principal, PositionKeys.DeputyPrincipal, PositionKeys.HOD,
        PositionKeys.SubjectTeacher, PositionKeys.ClassTeacher, PositionKeys.LOTeacher,
        PositionKeys.SportCultureMIC, PositionKeys.FinanceManager, PositionKeys.ITAdministrator,
    };

    // Standard: Compact + GradeHead (the full common model).
    private static readonly string[] StandardKeys =
        CompactKeys.Append(PositionKeys.GradeHead).ToArray();

    // Large: Standard + PhaseHead + granular finance roles.
    private static readonly string[] LargeKeys = StandardKeys
        .Concat(new[] { PositionKeys.PhaseHead, PositionKeys.BursarDebtorsClerk, PositionKeys.Cashier })
        .ToArray();

    public static bool IsValid(string preset) => Names.Contains(preset, StringComparer.OrdinalIgnoreCase);

    /// <summary>The default seeded position keys for a preset (empty if unknown).</summary>
    public static IReadOnlyList<string> KeysFor(string preset) =>
        preset?.Trim().ToLowerInvariant() switch
        {
            "compact" => CompactKeys,
            "standard" => StandardKeys,
            "large" => LargeKeys,
            _ => Array.Empty<string>(),
        };
}
