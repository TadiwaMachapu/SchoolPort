using System.Net;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Sprint 1.5.3 Smart Reports v1 role views — tenancy guard on the HOD subject endpoint. The
/// subjectId route id must belong to the caller's school; a foreign-school subject dead-ends as
/// 404 (before any scope/at-risk work). The scanner only requires guards on mutating endpoints,
/// so this GET is registered here explicitly per the sprint's security rule for route ids.
/// </summary>
[Collection("SecurityApi")]
public class SmartReportsRoleViewTests
{
    private readonly ApiFactory _api;
    public SmartReportsRoleViewTests(ApiFactory api) => _api = api;

    [CrossTenantGuard(typeof(SmartReportsController), nameof(SmartReportsController.GetSubjectView))]
    [Fact]
    public async Task SubjectView_ForeignSchoolSubject_Returns404()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher"); // holds marks.view_class

        var foreignSubject = await _api.WithScopeAsync(async db =>
        {
            var schoolB = Guid.NewGuid();
            db.Schools.Add(new School { SchoolId = schoolB, Name = "S" + schoolB.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });
            var subjectId = Guid.NewGuid();
            db.Subjects.Add(new Subject { SubjectId = subjectId, SchoolId = schoolB, Name = "Physics", Code = "PHY", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return subjectId;
        });

        var resp = await _api.ClientFor(teacher).GetAsync($"/api/smart-reports/subject/{foreignSubject}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── oversight-position gate (Sprint 1.5.3) ──────────────────────────────────────
    // The role views gate on POSITION, not merely marks.view_class. A plain SubjectTeacher passes the
    // attribute policy but holds no oversight position → 403, not an empty 200. Holders of the relevant
    // oversight position pass the gate.

    [Fact]
    public async Task GradeView_SubjectTeacher_Returns403()
    {
        var schoolId = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolId, "Staff", "SubjectTeacher"); // marks.view_class, no oversight

        var resp = await _api.ClientFor(teacher).GetAsync("/api/smart-reports/grade/12");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GradeView_GradeHeadOfSameGrade_PassesGate()
    {
        var schoolId = Guid.NewGuid();
        var gradeHead = await _api.MintTokenAsync(schoolId, "Staff", "GradeHead");
        await SeedGradeScopeAsync(gradeHead.UserId, "12");

        var resp = await _api.ClientFor(gradeHead).GetAsync("/api/smart-reports/grade/12");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode); // holds the grade; empty roster is a valid 200
    }

    [Fact]
    public async Task GradeView_GradeHeadOfDifferentGrade_Returns403()
    {
        // Holds GradeHead (gate passes) but is scoped to Gr 12 — asking for Gr 11 is out of scope.
        // Same shape as the subject view's wrong-subject 403: "hold the position but not for this
        // thing" is a 403, never an empty 200.
        var schoolId = Guid.NewGuid();
        var gradeHead = await _api.MintTokenAsync(schoolId, "Staff", "GradeHead");
        await SeedGradeScopeAsync(gradeHead.UserId, "12");

        var resp = await _api.ClientFor(gradeHead).GetAsync("/api/smart-reports/grade/11");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // Seeds a Grade scope row on the user's (single) GradeHead appointment. CanAccessGradeAsync reads
    // the DB live, so a scope added after MintTokenAsync's login is still honoured this request.
    private Task SeedGradeScopeAsync(Guid userId, string grade) => _api.WithScopeAsync(async db =>
    {
        var upId = await db.UserPositions.Where(up => up.UserId == userId).Select(up => up.UserPositionId).FirstAsync();
        db.UserPositionScopes.Add(new UserPositionScope
        {
            UserPositionScopeId = Guid.NewGuid(),
            UserPositionId = upId,
            ScopeType = ScopeType.Grade,
            ScopeValue = grade,
        });
        await db.SaveChangesAsync();
    });

    [Fact]
    public async Task SubjectView_SubjectTeacher_Returns403()
    {
        var schoolId = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolId, "Staff", "SubjectTeacher");

        // An in-school subject so the request clears the 404 tenancy check and reaches the position gate.
        var subjectId = await _api.WithScopeAsync(async db =>
        {
            var id = Guid.NewGuid();
            db.Subjects.Add(new Subject { SubjectId = id, SchoolId = schoolId, Name = "Mathematics", Code = "MAT", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return id;
        });

        var resp = await _api.ClientFor(teacher).GetAsync($"/api/smart-reports/subject/{subjectId}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SchoolOverview_HOD_Returns403()
    {
        // HOD holds analytics.view_school (passes the attribute) but the overview is SMT-only.
        var schoolId = Guid.NewGuid();
        var hod = await _api.MintTokenAsync(schoolId, "Staff", "HOD");

        var resp = await _api.ClientFor(hod).GetAsync("/api/smart-reports/school-overview");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SchoolOverview_Principal_PassesGate()
    {
        var schoolId = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolId, "Staff", "Principal");

        var resp = await _api.ClientFor(principal).GetAsync("/api/smart-reports/school-overview");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
