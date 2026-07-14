using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Sprint 1.5.2 Week 2 — the risk dashboard + grade overview exercised against the REAL
/// Greendale demo dataset (DevSeedController), over the real HTTP pipeline with real tokens.
///
/// Two demo-data realities this test documents (see the Week 2 report):
/// 1. DevSeed predates the identity/positions model — its users carry legacy Role only
///    (null Identity, no UserPositions). The live DB was backfilled in Sprint 1.5.0; a fresh
///    seed is NOT, so this test applies the same backfill to the demo teacher (Staff +
///    SubjectTeacher) before logging in. Fresh demo environments need DevSeed updated.
/// 2. DevSeed's current term now spans "now" (Sprint 1.5.3), so its assignments (due now − 14d)
///    fall inside it and the term-scoped at-risk average sees them. Missing counts are still 0
///    (every learner is graded in every subject they take) and trends are still no_data (all marks
///    live in the single current term — the previous term carries none), so risk derives from
///    averages only.
/// </summary>
[Collection("SecurityApi")]
public class MatricDemoDataSpotCheckTests
{
    private readonly ApiFactory _api;
    private readonly ITestOutputHelper _output;
    public MatricDemoDataSpotCheckTests(ApiFactory api, ITestOutputHelper output)
    {
        _api = api;
        _output = output;
    }

    [Fact]
    public async Task RiskDashboard_And_GradeOverview_OnGreendaleDemoData()
    {
        // ── Seed the real demo dataset (DevSeed guards on IsDevelopment) ────────────
        var devEnv = new Mock<IWebHostEnvironment>();
        devEnv.SetupGet(e => e.EnvironmentName).Returns("Development");
        await _api.WithScopeAsync(db => new DevSeedController(db, devEnv.Object).Seed());

        var (schoolId, teacherUserId) = await _api.WithScopeAsync(async db =>
        {
            var school = await db.Schools.SingleAsync(s => s.Name == "Greendale High School");
            var teacher = await db.Users.SingleAsync(u => u.Email == "james.dlamini@greendale.edu");
            return (school.SchoolId, teacher.UserId);
        });

        // ── Backfill the demo teacher exactly as Sprint 1.5.0 did on live ───────────
        await _api.WithScopeAsync(async db =>
        {
            var teacher = await db.Users.SingleAsync(u => u.UserId == teacherUserId);
            teacher.Identity = "Staff";
            var positionId = await db.Positions.Where(p => p.Key == "SubjectTeacher")
                .Select(p => p.PositionId).SingleAsync();
            db.UserPositions.Add(new UserPosition
            {
                UserPositionId = Guid.NewGuid(), SchoolId = schoolId, UserId = teacherUserId,
                PositionId = positionId, EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                IsActive = true, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        // ── Teacher (scope: own class-subjects → Grade 12A) → risk dashboard ────────
        using var scope = _api.Services.CreateScope();
        var auth = (IAuthService)scope.ServiceProvider.GetService(typeof(IAuthService))!;
        var login = await auth.LoginAsync(new SchoolPortal.Shared.DTOs.Auth.LoginRequest
        {
            Email = "james.dlamini@greendale.edu",
            Password = "Teacher@1234!",
        });
        var teacherClient = _api.CreateClient();
        teacherClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);

        var dashResp = await teacherClient.GetAsync("/api/matric/risk-dashboard");
        Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);
        var dashJson = await dashResp.Content.ReadAsStringAsync();
        _output.WriteLine("RISK DASHBOARD (james.dlamini, SubjectTeacher):");
        _output.WriteLine(dashJson);

        using var dash = JsonDocument.Parse(dashJson);
        var learners = dash.RootElement.GetProperty("learners").EnumerateArray().ToList();
        Assert.Equal(3, learners.Count); // Lethabo, Amahle, Sipho

        string RiskFor(string firstName) => learners
            .Single(l => l.GetProperty("name").GetString()!.StartsWith(firstName))
            .GetProperty("overallRisk").GetString()!;

        // From DevSeed's grade map: Lethabo all subjects ≥ 50% → green; Amahle 37.5/29/38%
        // in Maths/PhysSci/LifeSci → red; Sipho 21/17/25% in Maths/PhysSci/Acc → red.
        Assert.Equal("green", RiskFor("Lethabo"));
        Assert.Equal("red", RiskFor("Amahle"));
        Assert.Equal("red", RiskFor("Sipho"));

        var summary = dash.RootElement.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("red").GetInt32());
        Assert.Equal(0, summary.GetProperty("amber").GetInt32());
        Assert.Equal(1, summary.GetProperty("green").GetInt32());

        // Red learners sort first, green last.
        Assert.Equal("red", learners.First().GetProperty("overallRisk").GetString());
        Assert.Equal("green", learners.Last().GetProperty("overallRisk").GetString());

        // Missing is 0 for everyone (each learner is graded in every subject they take) and trend is
        // no_data (all marks live in the single current term). Sipho's ungraded Genetics Test sits in
        // Life Sciences, which he doesn't take (no record) → not counted as his missing.
        var allSubjects = learners.SelectMany(l => l.GetProperty("subjects").EnumerateArray()).ToList();
        Assert.All(allSubjects, s => Assert.Equal(0, s.GetProperty("missingAssessments").GetInt32()));
        Assert.All(allSubjects, s => Assert.Equal("no_data", s.GetProperty("trend").GetString()));

        // ── GradeHead (Grade-12 scope) → grade overview ─────────────────────────────
        var gradeHead = await _api.MintTokenAsync(schoolId, "Staff", "GradeHead");
        await _api.WithScopeAsync(async db =>
        {
            var up = await db.UserPositions.SingleAsync(x => x.UserId == gradeHead.UserId);
            db.UserPositionScopes.Add(new UserPositionScope
            {
                UserPositionScopeId = Guid.NewGuid(), UserPositionId = up.UserPositionId,
                ScopeType = ScopeType.Grade, ScopeValue = "12",
            });
            await db.SaveChangesAsync();
        });

        var overviewResp = await _api.ClientFor(gradeHead).GetAsync("/api/matric/grade-overview");
        Assert.Equal(HttpStatusCode.OK, overviewResp.StatusCode);
        var overviewJson = await overviewResp.Content.ReadAsStringAsync();
        _output.WriteLine("GRADE OVERVIEW (minted GradeHead, Grade 12 scope):");
        _output.WriteLine(overviewJson);

        using var overview = JsonDocument.Parse(overviewJson);
        Assert.Equal(3, overview.RootElement.GetProperty("totalLearners").GetInt32());
        var rows = overview.RootElement.GetProperty("learners").EnumerateArray().ToList();

        var amahle = rows.Single(l => l.GetProperty("name").GetString()!.StartsWith("Amahle"));
        Assert.Equal(3, amahle.GetProperty("redSubjects").GetArrayLength()); // Maths, PhysSci, LifeSci
        Assert.Contains("3 subjects at red risk",
            amahle.GetProperty("priorityFlags").EnumerateArray().Select(f => f.GetString()));

        var sipho = rows.Single(l => l.GetProperty("name").GetString()!.StartsWith("Sipho"));
        Assert.Equal(3, sipho.GetProperty("redSubjects").GetArrayLength()); // Maths, PhysSci, Acc

        var lethabo = rows.Single(l => l.GetProperty("name").GetString()!.StartsWith("Lethabo"));
        Assert.Equal("green", lethabo.GetProperty("overallRisk").GetString());
        Assert.Equal(0, lethabo.GetProperty("priorityFlags").GetArrayLength());
    }
}
