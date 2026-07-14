using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Integration;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.2 Step 4 — study planner. Pins: non-Grade-12 learners get an empty plan,
/// the countdown is never negative, subjects order weakest-first, and the session/status
/// mapping follows the averages (same 40/30 thresholds as the NSC status views).
/// </summary>
[Collection("Postgres")]
public class MatricStudyPlanTests
{
    private readonly PostgresFixture _pg;
    public MatricStudyPlanTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task NonGrade12Learner_GetsEmptyPlan_WithValidCountdown()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = Seed.School(db);
            var studentId = Seed.Student(db, schoolId);
            await db.SaveChangesAsync();

            var plan = await new MatricHubService(db, new AtRiskService(db)).GetStudyPlanAsync(studentId, schoolId);

            Assert.False(plan.IsGrade12);
            Assert.Empty(plan.Subjects);
            Assert.Equal(0, plan.SuggestedWeeklySessions);
            Assert.True(plan.DaysToExams >= 0);
            Assert.Equal((plan.DaysToExams + 6) / 7, plan.WeeksToExams);
            Assert.Equal(10, plan.ExamStart.Month);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Grade12Plan_OrdersWeakestFirst_AndMapsSessionsToAverages()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = Seed.School(db);
            var studentId = Seed.Student(db, schoolId);
            var teacherUserId = Seed.User(db, schoolId);

            var classId = Guid.NewGuid();
            db.Classes.Add(new Class { ClassId = classId, SchoolId = schoolId, Name = "12A", GradeLevel = 12, CreatedAt = DateTime.UtcNow });
            db.Enrollments.Add(new Enrollment
            {
                EnrollmentId = Guid.NewGuid(), ClassId = classId, StudentId = studentId,
                SchoolId = schoolId, EnrolledAt = DateTime.UtcNow, IsActive = true,
            });

            void SubjectWithMark(string name, decimal score)
            {
                var subjectId = Guid.NewGuid();
                db.Subjects.Add(new Subject { SubjectId = subjectId, SchoolId = schoolId, Name = name, Code = name[..3].ToUpperInvariant(), CreatedAt = DateTime.UtcNow });
                var csId = Guid.NewGuid();
                db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = csId, ClassId = classId, SubjectId = subjectId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
                var aId = Guid.NewGuid();
                db.Assignments.Add(new Assignment
                {
                    AssignmentId = aId, ClassSubjectId = csId, SchoolId = schoolId, Title = $"{name} test",
                    DueAt = DateTime.UtcNow, MaxMarks = 100, CreatedAt = DateTime.UtcNow, CreatedByUserId = teacherUserId,
                });
                var subId = Guid.NewGuid();
                db.Submissions.Add(new Submission { SubmissionId = subId, AssignmentId = aId, StudentId = studentId, SchoolId = schoolId, SubmittedAt = DateTime.UtcNow });
                db.Grades.Add(new Grade { GradeId = Guid.NewGuid(), SubmissionId = subId, StudentId = studentId, AssignmentId = aId, SchoolId = schoolId, Score = score, GradedByUserId = teacherUserId, GradedAt = DateTime.UtcNow });
            }

            SubjectWithMark("Mathematics", 35);       // AtRisk band (30–39) → 4 sessions
            SubjectWithMark("Accounting", 45);        // Pass but weak → 3 sessions
            SubjectWithMark("Physical Sciences", 85); // Strong → 1 maintenance session
            await db.SaveChangesAsync();

            var plan = await new MatricHubService(db, new AtRiskService(db)).GetStudyPlanAsync(studentId, schoolId);

            Assert.True(plan.IsGrade12);
            Assert.Equal(new[] { "Mathematics", "Accounting", "Physical Sciences" },
                plan.Subjects.Select(s => s.SubjectName).ToArray()); // weakest first

            var maths = plan.Subjects[0];
            Assert.Equal("AtRisk", maths.Status);
            Assert.Equal(4, maths.WeeklySessions);

            var accounting = plan.Subjects[1];
            Assert.Equal("Pass", accounting.Status);
            Assert.Equal(3, accounting.WeeklySessions);

            var physics = plan.Subjects[2];
            Assert.Equal("Pass", physics.Status);
            Assert.Equal(1, physics.WeeklySessions);

            Assert.Equal(8, plan.SuggestedWeeklySessions);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
