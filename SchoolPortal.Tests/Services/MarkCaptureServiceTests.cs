using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Grades;
using SchoolPortal.Tests.Integration;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.2.5 — MarkCaptureService on real Postgres (isolated DB per test, scope mocked
/// unrestricted; the HTTP pipeline including 403/404 guards is exercised separately by
/// MarksCaptureCrossTenantWriteTests). Pins the Henco-markbook invariants: absent ≠ zero
/// (IsAbsent ⇒ null score, service-enforced), rubric totals are server-derived, and mark
/// CHANGES (not first entries) hit the audit log.
/// </summary>
[Collection("Postgres")]
public class MarkCaptureServiceTests : IAsyncLifetime
{
    private static readonly Guid SchoolId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TeacherUserId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid ClassId = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid SubjectId = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid ClassSubjectId = Guid.Parse("00000000-0000-0000-0000-0000000000a5");
    private static readonly Guid Student1 = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Student2 = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private readonly PostgresFixture _pg;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IScopeService> _scope = new();

    private SchoolPortalDbContext _context = null!;
    private NpgsqlDataSource _source = null!;
    private MarkCaptureService _service = null!;

    public MarkCaptureServiceTests(PostgresFixture pg)
    {
        _pg = pg;
        _currentUser.Setup(x => x.SchoolId).Returns(SchoolId);
        _currentUser.Setup(x => x.UserId).Returns(TeacherUserId);
        _scope.Setup(x => x.EnsureClassAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        // The real GetEnrolledStudentIdsAsync (active-only, school-pinned) is pinned by
        // ScopeServiceEnrolmentTests + the HTTP-pipeline cross-tenant tests; here it's stubbed
        // to the seeded roster so the service's in-memory HashSet check is what's under test.
        _scope.Setup(x => x.GetEnrolledStudentIdsAsync(ClassSubjectId, SchoolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { Student1, Student2 });
    }

    public async Task InitializeAsync()
    {
        (_context, _source) = await _pg.CreateIsolatedDatabaseAsync();
        _service = new MarkCaptureService(_context, _currentUser.Object, _scope.Object);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _source.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        _context.Schools.Add(new School { SchoolId = SchoolId, Name = "Capture Test School", IsActive = true, CreatedAt = DateTime.UtcNow });
        _context.Users.Add(new User { UserId = TeacherUserId, SchoolId = SchoolId, Email = "t@capture.test", PasswordHash = "x", FirstName = "Thandi", LastName = "Dlamini", Role = "Teacher", Identity = "Staff", IsActive = true, CreatedAt = DateTime.UtcNow });
        _context.Classes.Add(new Class { ClassId = ClassId, SchoolId = SchoolId, Name = "12D", CreatedAt = DateTime.UtcNow });
        _context.Subjects.Add(new Subject { SubjectId = SubjectId, SchoolId = SchoolId, Name = "Design", Code = "DSN", CreatedAt = DateTime.UtcNow });
        _context.ClassSubjects.Add(new ClassSubject { ClassSubjectId = ClassSubjectId, ClassId = ClassId, SubjectId = SubjectId, SchoolId = SchoolId, CreatedAt = DateTime.UtcNow });

        AddLearner(Student1, "Amahle", "Zulu", "STU-001");
        AddLearner(Student2, "Bongani", "Mokoena", "STU-002");
        await _context.SaveChangesAsync();
    }

    private void AddLearner(Guid studentId, string first, string last, string number)
    {
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { UserId = userId, SchoolId = SchoolId, Email = $"{number}@capture.test", PasswordHash = "x", FirstName = first, LastName = last, Role = "Student", Identity = "Learner", IsActive = true, CreatedAt = DateTime.UtcNow });
        _context.Students.Add(new Student { StudentId = studentId, SchoolId = SchoolId, UserId = userId, StudentNumber = number, CreatedAt = DateTime.UtcNow });
        _context.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = ClassId, StudentId = studentId, SchoolId = SchoolId, EnrolledAt = DateTime.UtcNow, IsActive = true });
    }

    private async Task<TaskSummaryDto> CreateSimpleTaskAsync(decimal maxMarks = 50)
        => await _service.CreateTaskAsync(new CreateTaskRequest
        {
            ClassSubjectId = ClassSubjectId, Title = "Theory test", TaskType = "Test",
            TermNumber = 3, MaxMarks = maxMarks, HasRubric = false,
        });

    private async Task<TaskSummaryDto> CreateRubricTaskAsync()
        => await _service.CreateTaskAsync(new CreateTaskRequest
        {
            ClassSubjectId = ClassSubjectId, Title = "Design task", TaskType = "PAT",
            TermNumber = 3, HasRubric = true,
            Criteria = new List<TaskCriteriaInput>
            {
                new() { Name = "Expression of intention and rationale", MaxMark = 6 },
                new() { Name = "Evidence of research and experimentation", MaxMark = 4 },
            },
        });

    // ── D2: absent ≠ zero, service-enforced ─────────────────────────────────────

    [Fact]
    public async Task Grade_IsAbsent_True_MustHave_Null_Score()
    {
        var task = await CreateSimpleTaskAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry> { new() { StudentId = Student1, IsAbsent = true, Score = 30 } },
        }));

        Assert.Contains("absent", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await _context.Grades.CountAsync()); // nothing written
    }

    [Fact]
    public async Task AbsentLearner_IsStored_WithNullScore_NotZero()
    {
        var task = await CreateSimpleTaskAsync();

        await _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new() { StudentId = Student1, IsAbsent = true },
                new() { StudentId = Student2, Score = 0 }, // present, scored nothing — a DIFFERENT state
            },
        });

        var absent = await _context.Grades.SingleAsync(g => g.StudentId == Student1);
        var zero = await _context.Grades.SingleAsync(g => g.StudentId == Student2);
        Assert.True(absent.IsAbsent); Assert.Null(absent.Score);
        Assert.False(zero.IsAbsent); Assert.Equal(0m, zero.Score);
    }

    // ── Capture-grid grades have no submission ───────────────────────────────────

    [Fact]
    public async Task BulkCapture_CreatesGrades_WithoutSubmissions_AndOpensDraftApproval()
    {
        var task = await CreateSimpleTaskAsync();

        var result = await _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new() { StudentId = Student1, Score = 41 },
                new() { StudentId = Student2, Score = 27 },
            },
        });

        Assert.Equal(2, result.Saved);
        Assert.Equal(0, result.Changed); // first-time entries are not "changes"

        var grades = await _context.Grades.OrderBy(g => g.Score).ToListAsync();
        Assert.All(grades, g => Assert.Null(g.SubmissionId));
        Assert.Equal(new[] { 27m, 41m }, grades.Select(g => g.Score!.Value));

        var approval = await _context.ApprovalRecords.SingleAsync(r => r.AssignmentId == task.AssignmentId);
        Assert.Equal(ApprovalStatus.Draft, approval.Status);
        Assert.Null(approval.SubmittedAt); // draft — submit-for-review is Week 3
    }

    // ── Rubric totals are server-derived ─────────────────────────────────────────

    [Fact]
    public async Task Rubric_Total_IsSumOfCriteria_ClientScoreIgnored()
    {
        var task = await CreateRubricTaskAsync();
        Assert.Equal(10m, task.MaxMarks); // 6 + 4, derived — not client-supplied

        var criteria = await _context.AssessmentCriteria.OrderBy(c => c.DisplayOrder).ToListAsync();
        await _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new()
                {
                    StudentId = Student1,
                    Score = 999, // must be ignored for rubric tasks
                    CriteriaScores = new List<LearnerCriteriaScoreDto>
                    {
                        new() { CriteriaId = criteria[0].CriteriaId, Score = 5 },
                        new() { CriteriaId = criteria[1].CriteriaId, Score = 3 },
                    },
                },
            },
        });

        var grade = await _context.Grades.Include(g => g.CriteriaScores).SingleAsync();
        Assert.Equal(8m, grade.Score);
        Assert.Equal(2, grade.CriteriaScores.Count);
    }

    [Fact]
    public async Task Rubric_PartialEntry_SumsEnteredCriteria_NullMeansPending()
    {
        var task = await CreateRubricTaskAsync();
        var criteria = await _context.AssessmentCriteria.OrderBy(c => c.DisplayOrder).ToListAsync();

        await _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new()
                {
                    StudentId = Student1,
                    CriteriaScores = new List<LearnerCriteriaScoreDto>
                    {
                        new() { CriteriaId = criteria[0].CriteriaId, Score = 4 },
                        new() { CriteriaId = criteria[1].CriteriaId, Score = null }, // pending ≠ 0
                    },
                },
            },
        });

        var grade = await _context.Grades.Include(g => g.CriteriaScores).SingleAsync();
        Assert.Equal(4m, grade.Score); // sum of ENTERED criteria only
        Assert.Null(grade.CriteriaScores.Single(s => s.CriteriaId == criteria[1].CriteriaId).Score);
    }

    // ── Validation ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkCapture_IsAbsentTrue_ScoreMustBeNull()
    {
        // Rubric variant of the D2 rule: absent with lingering CRITERIA scores is just as
        // invalid as absent with a simple score.
        var task = await CreateRubricTaskAsync();
        var criteria = await _context.AssessmentCriteria.OrderBy(c => c.DisplayOrder).ToListAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new()
                {
                    StudentId = Student1, IsAbsent = true,
                    CriteriaScores = new List<LearnerCriteriaScoreDto> { new() { CriteriaId = criteria[0].CriteriaId, Score = 3 } },
                },
            },
        }));
        Assert.Equal(0, await _context.Grades.CountAsync());
    }

    [Fact]
    public async Task BulkCapture_ScoreExceedsMaxMark_Rejected()
    {
        var task = await CreateSimpleTaskAsync(maxMarks: 50);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry> { new() { StudentId = Student1, Score = 51 } },
        }));
        Assert.Equal(0, await _context.Grades.CountAsync());
    }

    [Fact]
    public async Task CriteriaScore_AboveCriterionMax_Throws()
    {
        var task = await CreateRubricTaskAsync();
        var criteria = await _context.AssessmentCriteria.OrderBy(c => c.DisplayOrder).ToListAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new()
                {
                    StudentId = Student1,
                    CriteriaScores = new List<LearnerCriteriaScoreDto> { new() { CriteriaId = criteria[0].CriteriaId, Score = 7 } }, // max 6
                },
            },
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task CreateTask_TermNumber_OutOfRange_Throws(int term)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(new CreateTaskRequest
        {
            ClassSubjectId = ClassSubjectId, Title = "T", TaskType = "Test", TermNumber = term, MaxMarks = 10,
        }));
    }

    [Fact]
    public async Task CreateTask_SbaWeight_Above100_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTaskAsync(new CreateTaskRequest
        {
            ClassSubjectId = ClassSubjectId, Title = "T", TaskType = "Test", TermNumber = 1, MaxMarks = 10, SbaWeight = 101,
        }));
    }

    [Fact]
    public async Task CreateTask_PAT_IsAValidTaskType()
    {
        var task = await CreateRubricTaskAsync();
        var stored = await _context.Assignments.SingleAsync(a => a.AssignmentId == task.AssignmentId);
        Assert.Equal(TaskType.PAT, stored.TaskType);
    }

    // ── Audit: changes only, never first entries ─────────────────────────────────

    [Fact]
    public async Task Audit_FillingAPendingNullMark_IsInitialCapture_NotAChange()
    {
        // A row can exist with Score=null (pending): here, a rubric saved before any criterion
        // was entered. Filling it in later is INITIAL CAPTURE — no audit row. Only a subsequent
        // correction of the now-captured mark audits.
        var task = await CreateRubricTaskAsync();
        var criteria = await _context.AssessmentCriteria.OrderBy(c => c.DisplayOrder).ToListAsync();
        var capture = (decimal? c1) => _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId,
            Entries = new List<BulkCaptureEntry>
            {
                new()
                {
                    StudentId = Student1,
                    CriteriaScores = new List<LearnerCriteriaScoreDto> { new() { CriteriaId = criteria[0].CriteriaId, Score = c1 } },
                },
            },
        });

        await capture(null); // pending row created — Score null
        Assert.Null((await _context.Grades.SingleAsync()).Score);

        await capture(5);    // was null, now has value → initial capture, NO audit
        Assert.Equal(0, await _context.MarkCaptureAuditLogs.CountAsync());

        await capture(6);    // had 5, now 6 → correction, audit
        var audit = await _context.MarkCaptureAuditLogs.SingleAsync();
        Assert.Equal(5m, audit.PreviousScore);
        Assert.Equal(6m, audit.NewScore);
    }

    [Fact]
    public async Task BulkCapture_ValidEntry_CreatesAuditLogOnChange()
    {
        var task = await CreateSimpleTaskAsync();
        var capture = (decimal? score, bool absent) => _service.BulkCaptureAsync(new BulkCaptureRequest
        {
            TaskId = task.AssignmentId, ClassSubjectId = ClassSubjectId, ChangeReason = "remark after moderation",
            Entries = new List<BulkCaptureEntry> { new() { StudentId = Student1, Score = score, IsAbsent = absent } },
        });

        await capture(30, false);
        Assert.Equal(0, await _context.MarkCaptureAuditLogs.CountAsync()); // first entry — no audit

        await capture(30, false);
        Assert.Equal(0, await _context.MarkCaptureAuditLogs.CountAsync()); // unchanged resave — no audit

        await capture(35, false);
        var audit = await _context.MarkCaptureAuditLogs.SingleAsync();
        Assert.Equal(30m, audit.PreviousScore);
        Assert.Equal(35m, audit.NewScore);
        Assert.Equal("remark after moderation", audit.ChangeReason);

        await capture(null, true); // mark → absent is a change; absent audits a NULL new score
        var absentAudit = await _context.MarkCaptureAuditLogs.OrderBy(a => a.ChangedAt).LastAsync();
        Assert.Equal(35m, absentAudit.PreviousScore);
        Assert.Null(absentAudit.NewScore);
        Assert.True(absentAudit.NewIsAbsent);
    }
}
