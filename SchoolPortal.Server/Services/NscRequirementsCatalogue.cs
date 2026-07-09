namespace SchoolPortal.Server.Services;

/// <summary>
/// Sprint 1.5.2 Step 2 — static NSC certificate-requirements catalogue.
/// National DBE policy (stable since 2014), so this is in-code reference data — no DB table,
/// no per-school variation. Served read-only via GET /api/matric/nsc-requirements.
/// Sources: DBE "National Senior Certificate: a qualification at Level 4 on the NQF" policy;
/// minimum admission requirements for Higher Certificate / Diploma / Bachelor's study
/// (Government Gazette 31231). Wording uses SA English and "learner".
/// </summary>
public static class NscRequirementsCatalogue
{
    public record NscSubjectRule(string Subject, string Requirement, string Credits, string Notes);
    public record NscPassLevel(string Level, string Description, string[] Requirements);
    public record NscAchievementLevel(int Level, string Descriptor, string PercentBand);

    /// <summary>The 7-subject NSC composition — what counts toward the certificate.</summary>
    public static readonly NscSubjectRule[] SubjectRules =
    {
        new("Home Language",
            "Compulsory — one official language at Home Language level",
            "20 credits",
            "Must be passed at 40% for the NSC. This is the language that anchors the certificate."),
        new("First Additional Language",
            "Compulsory — a second official language at least at First Additional level",
            "20 credits",
            "One of the two languages must be the language of learning and teaching (English or Afrikaans at most schools)."),
        new("Mathematics or Mathematical Literacy",
            "Compulsory — one of the two (Technical Mathematics at technical schools)",
            "20 credits",
            "Universities distinguish sharply between the two for programme admission — check Pathways course requirements."),
        new("Life Orientation",
            "Compulsory",
            "10 credits",
            "The only 10-credit subject. Excluded from the standard APS best-6 and from the four-subject Diploma/Bachelor's counts."),
        new("Three elective subjects",
            "Chosen from the approved NSC subject list (e.g. Physical Sciences, Accounting, Geography, History)",
            "20 credits each",
            "Electives determine which university programmes are reachable — see the subject requirements per course in Pathways."),
    };

    /// <summary>Pass levels, lowest to highest. Each level also satisfies the ones below it.</summary>
    public static readonly NscPassLevel[] PassLevels =
    {
        new("NSC pass (Higher Certificate admission)",
            "The minimum to be awarded the National Senior Certificate.",
            new[]
            {
                "40% or more in your Home Language",
                "40% or more in two other subjects",
                "30% or more in three further subjects",
                "You may fail one subject (below 30%) only if the full school-based assessment for it was submitted",
            }),
        new("Diploma admission",
            "The NSC pass plus stronger performance in four subjects.",
            new[]
            {
                "Meet all NSC pass requirements",
                "40% or more in four 20-credit subjects (Life Orientation does not count)",
            }),
        new("Bachelor's admission",
            "The highest pass level — required for degree study at a university.",
            new[]
            {
                "Meet all NSC pass requirements",
                "50% or more in four 20-credit subjects (Life Orientation does not count)",
                "Universities add their own APS and per-subject requirements on top — track these against your goals in Pathways",
            }),
    };

    /// <summary>The 7-point achievement scale — identical to the APS points used in Pathways.</summary>
    public static readonly NscAchievementLevel[] AchievementLevels =
    {
        new(7, "Outstanding achievement", "80–100%"),
        new(6, "Meritorious achievement", "70–79%"),
        new(5, "Substantial achievement", "60–69%"),
        new(4, "Adequate achievement", "50–59%"),
        new(3, "Moderate achievement", "40–49%"),
        new(2, "Elementary achievement", "30–39%"),
        new(1, "Not achieved", "0–29%"),
    };
}
