using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Integration;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.1 Gap 2 — GetLearnerApsAsync must be a CURRENT-academic-year calculation.
/// Pins: (1) prior-year grades are excluded from the current APS; (2) a prior-year enrolment
/// does not duplicate a subject row (LearnerSubjects is per-year); (3) with no IsCurrent term
/// the latest year by Year is used; (4) a school with no academic years fails closed (0, 0, []).
/// Real Postgres via the shared fixture — one isolated database per test.
/// </summary>
[Collection("Postgres")]
public class PathwaysServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private SchoolPortalDbContext _context = null!;
    private NpgsqlDataSource _source = null!;
    private PathwaysService _service = null!;

    private readonly Guid _schoolId = Guid.NewGuid();
    private Guid _studentId;
    private Guid _subjectId;

    public PathwaysServiceTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        (_context, _source) = await _pg.CreateIsolatedDatabaseAsync();
        _service = new PathwaysService(_context, NullLogger<PathwaysService>.Instance);

        _context.Schools.Add(new School { SchoolId = _schoolId, Name = "APS Test School", IsActive = true, CreatedAt = DateTime.UtcNow });

        var userId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            UserId = userId, SchoolId = _schoolId, Email = $"u_{userId:N}@test.local", PasswordHash = "x",
            FirstName = "Aps", LastName = "Learner", Role = "Student", Identity = "Learner", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        _studentId = Guid.NewGuid();
        _context.Students.Add(new Student { StudentId = _studentId, SchoolId = _schoolId, UserId = userId, StudentNumber = "N001", GradeLevel = 12, CreatedAt = DateTime.UtcNow });

        _subjectId = Guid.NewGuid();
        _context.Subjects.Add(new Subject { SubjectId = _subjectId, SchoolId = _schoolId, Name = "Mathematics", Code = "MATH", CreatedAt = DateTime.UtcNow });

        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _source.DisposeAsync();
    }

    // ── Seed helpers ────────────────────────────────────────────────────────────

    private async Task<Guid> AddYearAsync(int year, bool currentTerm)
    {
        var yearId = Guid.NewGuid();
        _context.AcademicYears.Add(new AcademicYear
        {
            AcademicYearId = yearId, SchoolId = _schoolId, Year = year,
            StartDate = new DateTime(year, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(year, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        });
        _context.Terms.Add(new Term
        {
            TermId = Guid.NewGuid(), AcademicYearId = yearId, SchoolId = _schoolId, TermNumber = 1,
            StartDate = new DateTime(year, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(year, 3, 28, 0, 0, 0, DateTimeKind.Utc),
            IsCurrent = currentTerm, CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        return yearId;
    }

    private async Task EnrolAsync(Guid yearId, Guid? subjectId = null)
    {
        _context.LearnerSubjects.Add(new LearnerSubject
        {
            LearnerSubjectId = Guid.NewGuid(), StudentId = _studentId, SubjectId = subjectId ?? _subjectId,
            AcademicYearId = yearId, SchoolId = _schoolId, EnrolledAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    /// <summary>One graded assignment for the subject, due at the given date, scoring percent%.</summary>
    private async Task AddGradedMarkAsync(DateTime dueAt, decimal percent, Guid? subjectId = null)
    {
        var graderId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            UserId = graderId, SchoolId = _schoolId, Email = $"t_{graderId:N}@test.local", PasswordHash = "x",
            FirstName = "T", LastName = "G", Role = "Teacher", Identity = "Staff", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        var classId = Guid.NewGuid();
        _context.Classes.Add(new Class { ClassId = classId, SchoolId = _schoolId, Name = "12A-" + classId.ToString("N")[..4], GradeLevel = 12, CreatedAt = DateTime.UtcNow });
        var classSubjectId = Guid.NewGuid();
        _context.ClassSubjects.Add(new ClassSubject { ClassSubjectId = classSubjectId, ClassId = classId, SubjectId = subjectId ?? _subjectId, SchoolId = _schoolId, CreatedAt = DateTime.UtcNow });

        var assignmentId = Guid.NewGuid();
        _context.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId, ClassSubjectId = classSubjectId, SchoolId = _schoolId,
            Title = "A" + assignmentId.ToString("N")[..4], DueAt = dueAt, MaxMarks = 100,
            CreatedByUserId = graderId, CreatedAt = DateTime.UtcNow,
        });
        var submissionId = Guid.NewGuid();
        _context.Submissions.Add(new Submission
        {
            SubmissionId = submissionId, AssignmentId = assignmentId, StudentId = _studentId,
            SchoolId = _schoolId, SubmittedAt = dueAt,
        });
        _context.Grades.Add(new Grade
        {
            GradeId = Guid.NewGuid(), SubmissionId = submissionId, SchoolId = _schoolId,
            StudentId = _studentId, AssignmentId = assignmentId,
            Score = percent, GradedByUserId = graderId, GradedAt = dueAt,
        });
        await _context.SaveChangesAsync();
    }

    // ── Tests ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PriorYearGrades_AreExcluded_FromCurrentAps()
    {
        var year2025 = await AddYearAsync(2025, currentTerm: false);
        var year2026 = await AddYearAsync(2026, currentTerm: true);
        await EnrolAsync(year2025);
        await EnrolAsync(year2026);

        // 2025: 90% (would be 7 APS points). 2026: 55% (4 points).
        await AddGradedMarkAsync(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), 90m);
        await AddGradedMarkAsync(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 55m);

        var result = await _service.GetLearnerApsAsync(_studentId, _schoolId);

        var maths = Assert.Single(result.SubjectScores);
        Assert.Equal(55, maths.AveragePercent!.Value, precision: 5); // 90% from 2025 excluded
        Assert.Equal(4, maths.ApsPoints);
        Assert.Equal(4, result.StandardAps);
    }

    [Fact]
    public async Task PriorYearEnrolment_DoesNotDuplicate_SubjectRows()
    {
        var year2025 = await AddYearAsync(2025, currentTerm: false);
        var year2026 = await AddYearAsync(2026, currentTerm: true);
        await EnrolAsync(year2025); // same subject, prior year
        await EnrolAsync(year2026);

        var result = await _service.GetLearnerApsAsync(_studentId, _schoolId);

        Assert.Single(result.SubjectScores); // one Mathematics row, not two
    }

    [Fact]
    public async Task NoCurrentTerm_FallsBack_ToLatestYear()
    {
        var year2025 = await AddYearAsync(2025, currentTerm: false);
        var year2026 = await AddYearAsync(2026, currentTerm: false); // nothing flagged current
        await EnrolAsync(year2025);
        await EnrolAsync(year2026);

        await AddGradedMarkAsync(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), 90m);
        await AddGradedMarkAsync(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 65m);

        var result = await _service.GetLearnerApsAsync(_studentId, _schoolId);

        // Latest year (2026) wins: 65% → 5 points; the 2025 90% mark is out of scope.
        var maths = Assert.Single(result.SubjectScores);
        Assert.Equal(65, maths.AveragePercent!.Value, precision: 5);
        Assert.Equal(5, result.StandardAps);
    }

    [Fact]
    public async Task NoAcademicYears_FailsClosed_WithEmptyResult()
    {
        var result = await _service.GetLearnerApsAsync(_studentId, _schoolId);

        Assert.Equal(0, result.StandardAps);
        Assert.Equal(0, result.TotalAps);
        Assert.Empty(result.SubjectScores);
    }

    // ── Gap 3: CAPS-aware subject-name matching in goal tracking ────────────────

    [Fact]
    public async Task GoalTracking_MatchesRenamedSchoolSubject_ViaAlias()
    {
        // School renamed the subject "Maths"; the seeded requirement says "Mathematics".
        var year = await AddYearAsync(2026, currentTerm: true);
        var mathsId = Guid.NewGuid();
        _context.Subjects.Add(new Subject { SubjectId = mathsId, SchoolId = _schoolId, Name = "Maths", Code = "MAT", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        await EnrolAsync(year, mathsId);
        await AddGradedMarkAsync(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 72m, mathsId);

        var universityId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _context.Universities.Add(new University { UniversityId = universityId, Name = "Test University", Abbreviation = "TU", Province = "Gauteng" });
        _context.UniversityCourses.Add(new UniversityCourse { UniversityCourseId = courseId, UniversityId = universityId, Name = "BSc Testing", MinimumAps = 30 });
        _context.CourseSubjectRequirements.Add(new CourseSubjectRequirement
        {
            CourseSubjectRequirementId = Guid.NewGuid(), UniversityCourseId = courseId,
            SubjectName = "Mathematics", MinimumPercent = 60, IsRequired = true,
        });
        var goalId = Guid.NewGuid();
        _context.LearnerCareerGoals.Add(new LearnerCareerGoal
        {
            LearnerCareerGoalId = goalId, StudentId = _studentId, SchoolId = _schoolId,
            UniversityCourseId = courseId, Priority = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var tracking = await _service.GetGoalTrackingAsync(goalId, _studentId, _schoolId);

        // Before Gap 3 this was a silent miss: "Maths" != "Mathematics" → CurrentPercent null → Red.
        var mathsGap = Assert.Single(tracking.SubjectGaps);
        Assert.Equal(72, mathsGap.CurrentPercent!.Value, precision: 5);
        Assert.True(mathsGap.Met); // 72% >= 60%
    }
}
