using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.1 Gap 3 — CAPS subject-name matcher. Pins the normalisation rules, the explicit
/// alias table, code-based matching, and (critically) that unknown names do NOT fuzzy-match.
/// </summary>
public class CapsSubjectsTests
{
    // ── Normalisation ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("  Mathematics  ", "Mathematics")]
    [InlineData("mathematics", "Mathematics")]
    [InlineData("Engineering Graphics & Design", "Engineering Graphics and Design")]
    [InlineData("Engineering   Graphics   and   Design", "Engineering Graphics and Design")]
    [InlineData("English HL", "English Home Language")]
    [InlineData("English FAL", "English First Additional Language")]
    [InlineData("isizulu home language", "IsiZulu Home Language")]
    public void FindCanonical_ResolvesFormattingDrift(string input, string expectedCanonical)
    {
        Assert.Equal(expectedCanonical, CapsSubjects.FindCanonical(input));
    }

    // ── Alias table (the approved minimum set) ───────────────────────────────────

    [Theory]
    [InlineData("Maths", "Mathematics")]
    [InlineData("Math", "Mathematics")]
    [InlineData("Math Lit", "Mathematical Literacy")]
    [InlineData("Maths Lit", "Mathematical Literacy")]
    [InlineData("Math Literacy", "Mathematical Literacy")]
    [InlineData("Maths Literacy", "Mathematical Literacy")]
    [InlineData("Physical Science", "Physical Sciences")]
    [InlineData("Life Science", "Life Sciences")]
    [InlineData("Natural Science", "Natural Sciences")]
    [InlineData("Social Science", "Social Sciences")]
    [InlineData("Agricultural Science", "Agricultural Sciences")]
    public void FindCanonical_ResolvesAliases(string alias, string expectedCanonical)
    {
        Assert.Equal(expectedCanonical, CapsSubjects.FindCanonical(alias));
    }

    // ── Code matching (covers "CAT" and "EGD" from the approval) ─────────────────

    [Theory]
    [InlineData("CAT", "Computer Applications Technology")]
    [InlineData("EGD", "Engineering Graphics and Design")]
    [InlineData("egd", "Engineering Graphics and Design")]
    [InlineData("EMS", "Economic and Management Sciences")]
    [InlineData("LO", "Life Orientation")]
    [InlineData("IT", "Information Technology")]
    public void FindCanonical_ResolvesSubjectCodes(string code, string expectedCanonical)
    {
        Assert.Equal(expectedCanonical, CapsSubjects.FindCanonical(code));
    }

    // ── Matches: both sides resolved before comparison ───────────────────────────

    [Theory]
    [InlineData("Maths", "Mathematics")]
    [InlineData("EGD", "Engineering Graphics & Design")] // code vs "&" spelling — both resolve
    [InlineData("Physical Science", "physical sciences")]
    [InlineData("English HL", "English Home Language")]
    public void Matches_IsTrue_WhenBothResolveToSameCanonical(string a, string b)
    {
        Assert.True(CapsSubjects.Matches(a, b));
        Assert.True(CapsSubjects.Matches(b, a)); // symmetric
    }

    [Theory]
    [InlineData("Mathematics", "Mathematical Literacy")]  // related but distinct subjects
    [InlineData("Advanced Programme Maths", "Mathematics")] // AP Maths is NOT CAPS Mathematics
    [InlineData("Physics", "Physical Sciences")]           // deliberate: no fuzzy matching
    [InlineData("Robotics", "Information Technology")]
    public void Matches_IsFalse_ForDistinctOrUnknownNames(string a, string b)
    {
        Assert.False(CapsSubjects.Matches(a, b));
    }

    [Fact]
    public void FindCanonical_ReturnsNull_ForNonCapsNames()
    {
        Assert.Null(CapsSubjects.FindCanonical("Advanced Programme Maths"));
        Assert.Null(CapsSubjects.FindCanonical("Robotics"));
    }

    [Fact]
    public void Catalogue_MatchesSeededSubjectListShape()
    {
        // 11 languages × 2 + 8 Senior Phase + 23 FET = 53 rows (the SubjectService seed contract).
        Assert.Equal(53, CapsSubjects.All.Count);
        Assert.Contains(CapsSubjects.All, s => s is { Name: "Life Orientation", CapsPhase: "FET" });
        Assert.Contains(CapsSubjects.All, s => s is { Name: "Life Orientation", CapsPhase: "SeniorPhase" });
    }
}
