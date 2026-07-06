namespace SchoolPortal.Server.Services;

/// <summary>
/// Sprint 1.5.1 Gap 3 — single source of truth for CAPS standard subject names.
/// SubjectService seeds school subject lists from <see cref="All"/>, and every place that
/// matches a school subject name against a CAPS name from seed data (course subject
/// requirements, Senior Phase prerequisites) goes through <see cref="Matches"/> so a school
/// renaming "Mathematics" to "Maths" no longer silently breaks prerequisite matching.
///
/// Matching is deliberately NOT fuzzy: normalisation (case/whitespace/"&amp;"→"and"/HL-FAL
/// expansion), then an explicit, auditable alias table, then subject-code match (e.g. a school
/// subject literally named "EGD" matches "Engineering Graphics and Design"). A name that fails
/// all three is a genuine mismatch and must surface via the subject-match report — never guess.
/// </summary>
public static class CapsSubjects
{
    /// <summary>Canonical CAPS subject list (Name, Code, CapsPhase; null phase = both/N-A).</summary>
    public static readonly IReadOnlyList<(string Name, string? Code, string? CapsPhase)> All = Build();

    // Normalised alias → canonical name. Explicit and auditable; extend deliberately.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["maths"] = "Mathematics",
        ["math"] = "Mathematics",
        ["math lit"] = "Mathematical Literacy",
        ["maths lit"] = "Mathematical Literacy",
        ["math literacy"] = "Mathematical Literacy",
        ["maths literacy"] = "Mathematical Literacy",
        ["physical science"] = "Physical Sciences",
        ["life science"] = "Life Sciences",
        ["natural science"] = "Natural Sciences",
        ["social science"] = "Social Sciences",
        ["agricultural science"] = "Agricultural Sciences",
    };

    // Normalised canonical name → canonical name, and normalised code → canonical name.
    private static readonly Dictionary<string, string> CanonicalByNormalisedName;
    private static readonly Dictionary<string, string> CanonicalByCode;

    static CapsSubjects()
    {
        CanonicalByNormalisedName = new Dictionary<string, string>(StringComparer.Ordinal);
        CanonicalByCode = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, code, _) in All)
        {
            CanonicalByNormalisedName[Normalise(name)] = name;
            if (code != null)
                CanonicalByCode[Normalise(code)] = name; // MATH/LO repeat across phases with the same name — last write is identical
        }
    }

    /// <summary>Lowercase, trim, "&amp;"→"and", collapse whitespace, expand HL/FAL tokens.</summary>
    public static string Normalise(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace("&", " and ");
        s = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        // Token expansion for the common language-subject abbreviations.
        s = ReplaceToken(s, "hl", "home language");
        s = ReplaceToken(s, "fal", "first additional language");
        return s;
    }

    private static string ReplaceToken(string normalised, string token, string replacement)
    {
        var parts = normalised.Split(' ');
        for (var i = 0; i < parts.Length; i++)
            if (parts[i] == token)
                parts[i] = replacement;
        return string.Join(' ', parts);
    }

    /// <summary>Resolves a subject name to its canonical CAPS name via normalisation, the alias
    /// table, then subject code. Null when nothing matches (a genuine mismatch).</summary>
    public static string? FindCanonical(string name)
    {
        var n = Normalise(name);
        if (CanonicalByNormalisedName.TryGetValue(n, out var canonical)) return canonical;
        if (Aliases.TryGetValue(n, out var viaAlias)) return viaAlias;
        if (CanonicalByCode.TryGetValue(n, out var viaCode)) return viaCode;
        return null;
    }

    /// <summary>True when the two names refer to the same subject: each side resolves to its
    /// canonical CAPS name where possible (falling back to its normalised form) and the results
    /// are compared. Handles renames covered by aliases/codes and formatting drift ("&amp;" vs
    /// "and", case, whitespace) — never typo-guessing.</summary>
    public static bool Matches(string a, string b)
    {
        var ca = FindCanonical(a) ?? Normalise(a);
        var cb = FindCanonical(b) ?? Normalise(b);
        return string.Equals(ca, cb, StringComparison.OrdinalIgnoreCase);
    }

    private static List<(string Name, string? Code, string? CapsPhase)> Build()
    {
        var languages = new[]
        {
            "Afrikaans", "English", "IsiZulu", "IsiXhosa", "Sesotho", "Setswana",
            "Sepedi", "SiSwati", "IsiNdebele", "Tshivenda", "Xitsonga"
        };

        var subjects = new List<(string Name, string? Code, string? CapsPhase)>();

        foreach (var lang in languages)
        {
            subjects.Add(($"{lang} Home Language", null, null));
            subjects.Add(($"{lang} First Additional Language", null, null));
        }

        subjects.AddRange(new (string Name, string? Code, string? CapsPhase)[]
        {
            // Senior Phase (Gr 7–9)
            ("Mathematics", "MATH", "SeniorPhase"),
            ("Natural Sciences", "NSC", "SeniorPhase"),
            ("Social Sciences", "SS", "SeniorPhase"),
            ("Technology", "TECH", "SeniorPhase"),
            ("Economic and Management Sciences", "EMS", "SeniorPhase"),
            ("Life Orientation", "LO", "SeniorPhase"),
            ("Creative Arts", "CA", "SeniorPhase"),
            ("Physical Education", "PE", "SeniorPhase"),

            // FET Phase (Gr 10–12)
            ("Mathematics", "MATH", "FET"),
            ("Mathematical Literacy", "ML", "FET"),
            ("Life Sciences", "LS", "FET"),
            ("Physical Sciences", "PS", "FET"),
            ("Geography", "GEO", "FET"),
            ("History", "HIST", "FET"),
            ("Accounting", "ACC", "FET"),
            ("Business Studies", "BS", "FET"),
            ("Economics", "ECO", "FET"),
            ("Life Orientation", "LO", "FET"),
            ("Visual Arts", "VA", "FET"),
            ("Dramatic Arts", "DA", "FET"),
            ("Music", "MUS", "FET"),
            ("Consumer Studies", "CS", "FET"),
            ("Hospitality Studies", "HS", "FET"),
            ("Tourism", "TRM", "FET"),
            ("Agricultural Sciences", "AGR", "FET"),
            ("Computer Applications Technology", "CAT", "FET"),
            ("Information Technology", "IT", "FET"),
            ("Engineering Graphics and Design", "EGD", "FET"),
            ("Electrical Technology", "ELT", "FET"),
            ("Mechanical Technology", "MT", "FET"),
            ("Civil Technology", "CT", "FET"),
        });

        return subjects;
    }
}
