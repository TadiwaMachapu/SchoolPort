using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Assignment HTTP endpoint tests on the REAL pipeline. Previously a bespoke WebApplicationFactory over
/// the EF in-memory provider, which couldn't map the jsonb POCO columns and so failed at SeedTestData —
/// a pre-existing baseline-red (see [[test-suite-baseline-red]]). Rebuilt on the Step 10
/// <see cref="ApiFactory"/> harness: real Postgres + full auth/permission pipeline + production token
/// issuing. Under the permission model a bare authenticated user no longer suffices — POST
/// /api/assignments requires <c>assessment.create</c> (a SubjectTeacher position), GET requires
/// <c>platform.access</c> (any identity) — so the caller is minted as Staff + SubjectTeacher.
/// </summary>
[Collection("SecurityApi")]
public class AssignmentEndpointTests
{
    private readonly ApiFactory _api;
    public AssignmentEndpointTests(ApiFactory api) => _api = api;

    // A Staff + SubjectTeacher client (holds assessment.create + platform.access) plus a class-subject
    // in the same school for the assignment to attach to.
    private async Task<(HttpClient Client, Guid ClassSubjectId)> AuthedTeacherAsync()
    {
        var schoolId = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolId, "Staff", "SubjectTeacher");

        var classSubjectId = Guid.Empty;
        await _api.WithScopeAsync(async db =>
        {
            classSubjectId = Seed.ClassSubject(db, schoolId);
            await db.SaveChangesAsync();
        });

        return (_api.ClientFor(teacher), classSubjectId);
    }

    [Fact]
    public async Task CreateAssignment_ValidRequest_ReturnsCreated()
    {
        var (client, classSubjectId) = await AuthedTeacherAsync();

        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = classSubjectId,
            Title = "Integration Test Assignment",
            Description = "Test Description",
            DueAt = DateTime.UtcNow.AddDays(7),
            MaxMarks = 100
        };

        var response = await client.PostAsJsonAsync("/api/assignments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var assignment = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("Integration Test Assignment", assignment.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetAssignments_WithAuth_ReturnsOk()
    {
        var (client, _) = await AuthedTeacherAsync();

        var response = await client.GetAsync("/api/assignments?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAssignments_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _api.AnonymousClient().GetAsync("/api/assignments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssignment_InvalidDueDate_ReturnsBadRequest()
    {
        var (client, classSubjectId) = await AuthedTeacherAsync();

        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = classSubjectId,
            Title = "Test Assignment",
            DueAt = DateTime.UtcNow.AddDays(-1),
            MaxMarks = 100
        };

        var response = await client.PostAsJsonAsync("/api/assignments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
