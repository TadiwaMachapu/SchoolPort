using System.Net;
using System.Text.Json;
using SchoolPortal.Data.Entities;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security;

/// <summary>
/// Sprint 1.5.1 Gap 3 — GET /api/pathways/subject-match-report holder contract
/// (academics.diagnostics = Principal/Deputy/HOD + ITAdministrator) and report shape:
/// both mismatch directions (unrecognised school subject; seed requirement no school
/// subject satisfies) and the healthy flag.
/// </summary>
[Collection("SecurityApi")]
public class SubjectMatchReportTests
{
    private const string Url = "/api/pathways/subject-match-report";

    private readonly ApiFactory _factory;
    public SubjectMatchReportTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("HOD")]
    [InlineData("ITAdministrator")]
    public async Task Holder_Gets200(string positionKey)
    {
        var user = await _factory.MintTokenAsync(Guid.NewGuid(), "Staff", positionKey);
        using var client = _factory.ClientFor(user);

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonHolder_Learner_Gets403()
    {
        var user = await _factory.MintTokenAsync(Guid.NewGuid(), "Learner");
        using var client = _factory.ClientFor(user);

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Gets401()
    {
        using var client = _factory.AnonymousClient();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Report_FlagsBothMismatchDirections()
    {
        var schoolId = Guid.NewGuid();
        var user = await _factory.MintTokenAsync(schoolId, "Staff", "ITAdministrator");

        await _factory.WithScopeAsync(async db =>
        {
            // A school subject no CAPS name matches, and a healthy (aliased) one.
            db.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), SchoolId = schoolId, Name = "Robotics", Code = "ROB", CreatedAt = DateTime.UtcNow });
            db.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), SchoolId = schoolId, Name = "Maths", Code = "MAT", CreatedAt = DateTime.UtcNow });

            // A seeded requirement name ("Physical Sciences") that NO subject in this school matches.
            var universityId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            db.Universities.Add(new University { UniversityId = universityId, Name = "U " + universityId.ToString("N")[..6], Abbreviation = "U", Province = "Gauteng" });
            db.UniversityCourses.Add(new UniversityCourse { UniversityCourseId = courseId, UniversityId = universityId, Name = "BSc", MinimumAps = 30 });
            db.CourseSubjectRequirements.Add(new CourseSubjectRequirement
            {
                CourseSubjectRequirementId = Guid.NewGuid(), UniversityCourseId = courseId,
                SubjectName = "Physical Sciences", MinimumPercent = 60, IsRequired = true,
            });
            await db.SaveChangesAsync();
        });

        using var client = _factory.ClientFor(user);
        var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.False(root.GetProperty("healthy").GetBoolean());

        // Direction 1: "Robotics" is unrecognised; "Maths" resolves via alias so is NOT flagged.
        var unmatchedNames = root.GetProperty("unmatchedSchoolSubjects").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Contains("Robotics", unmatchedNames);
        Assert.DoesNotContain("Maths", unmatchedNames);

        // Direction 2: "Physical Sciences" is required by seed data but unmatched in this school.
        var unresolved = root.GetProperty("unresolvedRequirementNames").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Contains("Physical Sciences", unresolved);
        // "Mathematics" must NOT be unresolved — "Maths" matches it via the alias table.
        Assert.DoesNotContain("Mathematics", unresolved);
    }
}
