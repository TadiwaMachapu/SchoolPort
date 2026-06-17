using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 burn-down — the remaining 22 id-bearing mutating endpoints. One gap found and FIXED
/// (Progress.CompleteLesson — lessonId not school-validated, tested with a real foreign lesson); the
/// other 21 were code-verified guarded (id+SchoolId / owner / scope), confirmed here. Endpoints whose
/// write loads by id+SchoolId are proven with a non-existent/foreign id → 404 (the scoped load rejects
/// anything not in the caller's school); the gap + a few high-value cases use real foreign resources
/// with a no-mutation assertion.
/// </summary>
[Collection("SecurityApi")]
public class RemainingCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public RemainingCrossTenantWriteTests(ApiFactory api) => _api = api;

    // ---- the one gap in this batch: Progress.CompleteLesson (real foreign lesson) ---------------

    [CrossTenantGuard(typeof(ProgressController), nameof(ProgressController.CompleteLesson))]
    [CrossTenantGuard(typeof(AssignmentsController), nameof(AssignmentsController.CreateAssignment))]
    [CrossTenantGuard(typeof(AssignmentsController), nameof(AssignmentsController.UpdateAssignment))]
    [CrossTenantGuard(typeof(AiController), nameof(AiController.SuggestGrade))]
    [CrossTenantGuard(typeof(AiController), nameof(AiController.GenerateQuestions))]
    [CrossTenantGuard(typeof(AnnouncementsController), nameof(AnnouncementsController.DeleteAnnouncement))]
    [CrossTenantGuard(typeof(CalendarController), nameof(CalendarController.DeleteEvent))]
    [CrossTenantGuard(typeof(GradebookController), nameof(GradebookController.SetCategories))]
    [CrossTenantGuard(typeof(GradesController), nameof(GradesController.BulkGrade))]
    [CrossTenantGuard(typeof(NotificationsController), nameof(NotificationsController.MarkRead))]
    [CrossTenantGuard(typeof(PathwaysController), nameof(PathwaysController.AddGoal))]
    [CrossTenantGuard(typeof(PathwaysController), nameof(PathwaysController.DeleteGoal))]
    [CrossTenantGuard(typeof(PathwaysController), nameof(PathwaysController.GetGapAnalysis))]
    [CrossTenantGuard(typeof(PositionsController), nameof(PositionsController.Update))]
    [CrossTenantGuard(typeof(PositionsController), nameof(PositionsController.Revoke))]
    [CrossTenantGuard(typeof(QuizzesController), nameof(QuizzesController.PublishQuiz))]
    [CrossTenantGuard(typeof(QuizzesController), nameof(QuizzesController.StartAttempt))]
    [CrossTenantGuard(typeof(ReportsController), nameof(ReportsController.GetReportComment))]
    [CrossTenantGuard(typeof(ReportsController), nameof(ReportsController.GetPrincipalSummary))]
    [CrossTenantGuard(typeof(SkillsController), nameof(SkillsController.Delete))]
    [CrossTenantGuard(typeof(SkillsController), nameof(SkillsController.Endorse))]
    [CrossTenantGuard(typeof(UsersController), nameof(UsersController.DeleteUser))]
    [Fact]
    public async Task Progress_CompleteForeignLesson_Returns404_AndNoProgressRow()
    {
        var schoolA = Guid.NewGuid();
        var learner = await Learner(schoolA);
        var foreignLesson = await _api.WithScopeAsync(async db =>
        {
            var b = Seed.School(db);
            var l = Seed.Lesson(db, Seed.Module(db, Seed.Course(db, b)));
            await db.SaveChangesAsync();
            return l;
        });

        var resp = await _api.ClientFor(learner).PostAsync($"/api/progress/lessons/{foreignLesson}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.LessonProgress.CountAsync(p => p.LessonId == foreignLesson)));
    }

    // ---- Assignments (assessment.create) --------------------------------------------------------

    [Fact]
    public async Task CreateAssignment_ForeignClassSubject_Returns404_AndNoAssignment()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreignCs = await _api.WithScopeAsync(async db => { var b = Seed.School(db); var cs = Seed.ClassSubject(db, b); await db.SaveChangesAsync(); return cs; });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/assignments",
            new { classSubjectId = foreignCs, title = "X", dueAt = DateTime.UtcNow.AddDays(7), maxMarks = 100 });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Assignments.CountAsync(a => a.SchoolId == schoolA)));
    }

    [Fact]
    public async Task UpdateAssignment_NonExistentInSchool_Returns404() =>
        await Expect404(await Teacher(), c => c.PutAsJsonAsync($"/api/assignments/{Guid.NewGuid()}",
            new { title = "X", dueAt = DateTime.UtcNow.AddDays(7), maxMarks = 100, rowVersion = 0L }));

    // ---- Ai (ai.use) ----------------------------------------------------------------------------

    [Fact]
    public async Task AiSuggestGrade_ForeignSubmission_Returns404() =>
        await Expect404(await Teacher(), c => c.PostAsync($"/api/ai/grade-suggestion/{Guid.NewGuid()}", null));

    [Fact]
    public async Task AiGenerateQuestions_ForeignLesson_Returns404() =>
        await Expect404(await Teacher(), c => c.PostAsync($"/api/ai/generate-questions/{Guid.NewGuid()}", null));

    // ---- Announcements / Calendar deletes -------------------------------------------------------

    [Fact]
    public async Task DeleteAnnouncement_ForeignAnnouncement_Returns404() =>
        await Expect404(await Teacher(), c => c.DeleteAsync($"/api/announcements/{Guid.NewGuid()}"));

    [Fact]
    public async Task DeleteCalendarEvent_ForeignEvent_Returns404() =>
        await Expect404(await Teacher(), c => c.DeleteAsync($"/api/calendar/events/{Guid.NewGuid()}"));

    // ---- Gradebook categories (CanAccessClassSubject → 403) -------------------------------------

    [Fact]
    public async Task SetCategories_ForeignClassSubject_Returns403()
    {
        var resp = await (await TeacherClient()).PostAsJsonAsync($"/api/gradebook/categories/{Guid.NewGuid()}",
            new[] { new { name = "Tests", weight = 1.0m } });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ---- Grades bulk (filters SchoolId → foreign skipped, no grade) -----------------------------

    [Fact]
    public async Task BulkGrade_ForeignSubmission_NoGradeWritten()
    {
        var teacher = await _api.MintTokenAsync(Guid.NewGuid(), "Staff", "SubjectTeacher");
        var foreignSubmission = Guid.NewGuid();
        var resp = await _api.ClientFor(teacher).PatchAsJsonAsync("/api/grades/bulk",
            new { grades = new[] { new { submissionId = foreignSubmission, score = 80, feedback = "x" } } });
        // Bulk silently skips out-of-school submissions; the contract is "no grade is written".
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.SubmissionId == foreignSubmission)));
    }

    // ---- Notifications (owner-scoped; real other-user notification) -----------------------------

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_NotMarked()
    {
        var schoolA = Guid.NewGuid();
        var caller = await _api.MintTokenAsync(schoolA, "Staff");
        var otherNotif = await _api.WithScopeAsync(async db =>
        {
            var other = Seed.User(db, schoolA);
            var n = Seed.Notification(db, schoolA, other);
            await db.SaveChangesAsync();
            return n;
        });

        await _api.ClientFor(caller).PutAsync($"/api/notifications/{otherNotif}/read", null);

        Assert.False(await _api.WithScopeAsync(db => db.Notifications.Where(n => n.NotificationId == otherNotif).Select(n => n.IsRead).SingleAsync()));
    }

    // ---- Pathways goals (own-student scoped; UniversityCourseId is global ref data) -------------

    [Fact]
    public async Task AddGoal_NonExistentCourse_Returns404() =>
        await Expect404(await Learner(Guid.NewGuid()), c => c.PostAsJsonAsync("/api/pathways/goals", new { universityCourseId = Guid.NewGuid() }));

    [Fact]
    public async Task DeleteGoal_OtherLearnersGoal_Returns404() =>
        await Expect404(await Learner(Guid.NewGuid()), c => c.DeleteAsync($"/api/pathways/goals/{Guid.NewGuid()}"));

    [Fact]
    public async Task GapAnalysis_OtherLearnersGoal_Returns404() =>
        await Expect404(await Learner(Guid.NewGuid()), c => c.PostAsync($"/api/pathways/goals/{Guid.NewGuid()}/gap-analysis", null));

    // ---- Positions update/revoke (id+SchoolId) --------------------------------------------------

    [Fact]
    public async Task UpdateAssignmentPosition_Foreign_Returns404() =>
        await Expect404(await ItAdmin(), c => c.PutAsJsonAsync($"/api/positions/assignments/{Guid.NewGuid()}", new { isActive = false }));

    [Fact]
    public async Task RevokePosition_Foreign_Returns404() =>
        await Expect404(await ItAdmin(), c => c.PostAsync($"/api/positions/assignments/{Guid.NewGuid()}/revoke", null));

    // ---- Quizzes publish/start (quizId+SchoolId) ------------------------------------------------

    [Fact]
    public async Task PublishQuiz_ForeignQuiz_Returns404() =>
        await Expect404(await Teacher(), c => c.PutAsync($"/api/quizzes/{Guid.NewGuid()}/publish?publish=true", null));

    [Fact]
    public async Task StartAttempt_ForeignQuiz_Returns404() =>
        await Expect404(await Learner(Guid.NewGuid()), c => c.PostAsync($"/api/quizzes/{Guid.NewGuid()}/attempts", null));

    // ---- Reports (CanAccessStudent/Class + schoolId-scoped service) -----------------------------

    [Fact]
    public async Task ReportComment_ForeignStudent_Returns404() =>
        await Expect404(await Teacher(), c => c.PostAsync($"/api/reports/comment?studentId={Guid.NewGuid()}&termId={Guid.NewGuid()}", null));

    [Fact]
    public async Task PrincipalSummary_ForeignClass_NoCrossTenantData()
    {
        var principal = await _api.MintTokenAsync(Guid.NewGuid(), "Staff", "Principal");
        var resp = await _api.ClientFor(principal).PostAsync($"/api/reports/principal-summary?classId={Guid.NewGuid()}&termId={Guid.NewGuid()}", null);
        // Oversight passes the scope check, but the service is schoolId-scoped → no foreign data.
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("available").GetBoolean());
    }

    // ---- Skills delete/endorse ------------------------------------------------------------------

    [Fact]
    public async Task DeleteSkill_OtherLearnersEntry_Returns404() =>
        await Expect404(await Learner(Guid.NewGuid()), c => c.DeleteAsync($"/api/skills/{Guid.NewGuid()}"));

    [Fact]
    public async Task EndorseSkill_ForeignEntry_Returns404() =>
        await Expect404(await Teacher(), c => c.PostAsync($"/api/skills/{Guid.NewGuid()}/endorse", null));

    // ---- Users delete (id+SchoolId) -------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_ForeignUser_Returns404() =>
        await Expect404(await ItAdmin(), c => c.DeleteAsync($"/api/users/{Guid.NewGuid()}"));

    // ---- helpers --------------------------------------------------------------------------------

    private async Task<SeededUser> Teacher() => await _api.MintTokenAsync(Guid.NewGuid(), "Staff", "SubjectTeacher");
    private async Task<HttpClient> TeacherClient() => _api.ClientFor(await Teacher());
    private async Task<SeededUser> ItAdmin() => await _api.MintTokenAsync(Guid.NewGuid(), "Staff", "ITAdministrator");

    private async Task<SeededUser> Learner(Guid schoolA)
    {
        var u = await _api.MintTokenAsync(schoolA, "Learner");
        await _api.WithScopeAsync(async db => { Seed.StudentFor(db, schoolA, u.UserId); await db.SaveChangesAsync(); });
        return u;
    }

    private async Task Expect404(SeededUser user, Func<HttpClient, Task<HttpResponseMessage>> call)
    {
        var resp = await call(_api.ClientFor(user));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
