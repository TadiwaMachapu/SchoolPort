using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Integration;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.2 Week 2 — teacher risk dashboard + Grade Head overview. Pins the spec bands
/// (green ≥50% + 0 missing; amber 40–49% OR 1–2 missing; red &lt;40% OR 3+ missing OR
/// declining &lt;60%), the ±5% term-over-term trend, IScopeService class filtering, and the
/// overview's priority flags / red-first ordering.
/// </summary>
[Collection("Postgres")]
public class MatricRiskDashboardTests
{
    private readonly PostgresFixture _pg;
    public MatricRiskDashboardTests(PostgresFixture pg) => _pg = pg;

    private sealed class Fixture
    {
        public SchoolPortalDbContext Db = null!;
        public Guid SchoolId;
        public Guid TeacherUserId;
        public Guid ClassId;
        public Term Current = null!;
        public Term Previous = null!;
    }

    private static async Task<Fixture> SetUpAsync(SchoolPortalDbContext db, bool withTerms = true)
    {
        var f = new Fixture { Db = db, SchoolId = Seed.School(db) };
        f.TeacherUserId = Seed.User(db, f.SchoolId);
        f.ClassId = Guid.NewGuid();
        db.Classes.Add(new Class { ClassId = f.ClassId, SchoolId = f.SchoolId, Name = "12A", GradeLevel = 12, CreatedAt = DateTime.UtcNow });

        if (withTerms)
        {
            var yearId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            db.AcademicYears.Add(new AcademicYear
            {
                AcademicYearId = yearId, SchoolId = f.SchoolId, Year = now.Year,
                StartDate = now.AddDays(-200), EndDate = now.AddDays(100), CreatedAt = now,
            });
            f.Previous = new Term
            {
                TermId = Guid.NewGuid(), AcademicYearId = yearId, SchoolId = f.SchoolId,
                TermNumber = 1, StartDate = now.AddDays(-200), EndDate = now.AddDays(-101), CreatedAt = now,
            };
            f.Current = new Term
            {
                TermId = Guid.NewGuid(), AcademicYearId = yearId, SchoolId = f.SchoolId,
                TermNumber = 2, StartDate = now.AddDays(-100), EndDate = now.AddDays(50), IsCurrent = true, CreatedAt = now,
            };
            db.Terms.AddRange(f.Previous, f.Current);
        }

        await db.SaveChangesAsync();
        return f;
    }

    private static Guid Learner(Fixture f, string first)
    {
        var userId = Seed.User(f.Db, f.SchoolId, "Student", "Learner");
        f.Db.Users.Local.Single(u => u.UserId == userId).FirstName = first;
        var studentId = Seed.StudentFor(f.Db, f.SchoolId, userId);
        f.Db.Enrollments.Add(new Enrollment
        {
            EnrollmentId = Guid.NewGuid(), ClassId = f.ClassId, StudentId = studentId,
            SchoolId = f.SchoolId, EnrolledAt = DateTime.UtcNow, IsActive = true,
        });
        return studentId;
    }

    private static Guid ClassSubjectFor(Fixture f, string subjectName)
    {
        var subjectId = Guid.NewGuid();
        f.Db.Subjects.Add(new Subject { SubjectId = subjectId, SchoolId = f.SchoolId, Name = subjectName, Code = subjectName[..3].ToUpperInvariant(), CreatedAt = DateTime.UtcNow });
        var csId = Guid.NewGuid();
        f.Db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = csId, ClassId = f.ClassId, SubjectId = subjectId, SchoolId = f.SchoolId, CreatedAt = DateTime.UtcNow });
        return csId;
    }

    /// <summary>One class-wide past-due assessment; per learner, score null = not submitted
    /// (missing for that learner). Assignments are CLASS-wide — a shared subject must use one
    /// assessment for the cohort, or classmates' work shows as everyone else's missing.</summary>
    private static void Assessment(Fixture f, Guid classSubjectId, DateTime dueAt,
        params (Guid StudentId, decimal? Score)[] entries)
    {
        var aId = Guid.NewGuid();
        f.Db.Assignments.Add(new Assignment
        {
            AssignmentId = aId, ClassSubjectId = classSubjectId, SchoolId = f.SchoolId,
            Title = "A" + aId.ToString("N")[..4], DueAt = dueAt, MaxMarks = 100,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = f.TeacherUserId,
        });
        foreach (var (studentId, score) in entries)
        {
            if (score == null) continue;
            var subId = Guid.NewGuid();
            f.Db.Submissions.Add(new Submission { SubmissionId = subId, AssignmentId = aId, StudentId = studentId, SchoolId = f.SchoolId, SubmittedAt = dueAt });
            f.Db.Grades.Add(new Grade { GradeId = Guid.NewGuid(), SubmissionId = subId, StudentId = studentId, AssignmentId = aId, SchoolId = f.SchoolId, Score = score.Value, GradedByUserId = f.TeacherUserId, GradedAt = dueAt });
        }
    }

    private static void Mark(Fixture f, Guid classSubjectId, Guid studentId, decimal? score, DateTime dueAt)
        => Assessment(f, classSubjectId, dueAt, (studentId, score));

    [Fact]
    public async Task RiskBands_FollowTheSpec()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var f = await SetUpAsync(db);
            var inTerm = f.Current.StartDate.AddDays(5);

            var green = Learner(f, "Green");   // 55% + 60%, nothing missing → green
            var amberAvg = Learner(f, "AmberAvg"); // 45% → amber (band 40–49)
            var amberMissing = Learner(f, "AmberMissing"); // 70% but 1 missing → amber
            var redAvg = Learner(f, "RedAvg"); // 35% → red
            var redMissing = Learner(f, "RedMissing"); // strong marks, 3 missing → red
            await db.SaveChangesAsync();

            var csGreen = ClassSubjectFor(f, "Mathematics");
            Mark(f, csGreen, green, 55, inTerm);
            Mark(f, csGreen, green, 60, inTerm);

            var csAmberAvg = ClassSubjectFor(f, "Accounting");
            Mark(f, csAmberAvg, amberAvg, 45, inTerm);

            var csAmberMissing = ClassSubjectFor(f, "Geography");
            Mark(f, csAmberMissing, amberMissing, 70, inTerm);
            Mark(f, csAmberMissing, amberMissing, null, inTerm); // 1 missing

            var csRedAvg = ClassSubjectFor(f, "History");
            Mark(f, csRedAvg, redAvg, 35, inTerm);

            var csRedMissing = ClassSubjectFor(f, "Economics");
            Mark(f, csRedMissing, redMissing, 80, inTerm);
            Mark(f, csRedMissing, redMissing, null, inTerm);
            Mark(f, csRedMissing, redMissing, null, inTerm);
            Mark(f, csRedMissing, redMissing, null, inTerm); // 3 missing
            await db.SaveChangesAsync();

            var dash = await new MatricHubService(db).GetRiskDashboardAsync(f.SchoolId, null, null);

            string RiskOf(Guid id, string subject) => dash.Learners
                .Single(l => l.StudentId == id).Subjects.Single(s => s.SubjectName == subject).Risk;

            Assert.Equal("green", RiskOf(green, "Mathematics"));
            Assert.Equal("amber", RiskOf(amberAvg, "Accounting"));
            Assert.Equal("amber", RiskOf(amberMissing, "Geography"));
            Assert.Equal("red", RiskOf(redAvg, "History"));
            Assert.Equal("red", RiskOf(redMissing, "Economics"));

            // Sorted red-first; summary counts match.
            Assert.Equal(new RiskSummaryDto(2, 2, 1, 0), dash.Summary);
            Assert.Equal("red", dash.Learners.First().OverallRisk);
            Assert.Equal("green", dash.Learners.Last().OverallRisk);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Trend_ComparesTermAverages_AndDecliningUnder60IsRed()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var f = await SetUpAsync(db);
            var inPrev = f.Previous.StartDate.AddDays(5);
            var inCur = f.Current.StartDate.AddDays(5);

            var declining = Learner(f, "Declining"); // 70 → 45: declining, overall avg 57.5 < 60 → red
            var improving = Learner(f, "Improving"); // 50 → 62: improving → amber? avg 56 ≥ 50, 0 missing → green
            var stable = Learner(f, "Stable");       // 55 → 57: within ±5 → stable, green
            var oneTerm = Learner(f, "OneTerm");     // current term only → no_data
            await db.SaveChangesAsync();

            var cs1 = ClassSubjectFor(f, "Mathematics");
            // One shared assessment per term — oneTerm skipped the previous-term one (out of
            // the current window, so it may not count as missing).
            Assessment(f, cs1, inPrev, (declining, 70), (improving, 50), (stable, 55), (oneTerm, null));
            Assessment(f, cs1, inCur, (declining, 45), (improving, 62), (stable, 57), (oneTerm, 66));
            await db.SaveChangesAsync();

            var dash = await new MatricHubService(db).GetRiskDashboardAsync(f.SchoolId, null, null);
            SubjectRiskDto Of(Guid id) => dash.Learners.Single(l => l.StudentId == id).Subjects.Single();

            Assert.Equal("declining", Of(declining).Trend);
            Assert.Equal("red", Of(declining).Risk); // declining with average < 60

            Assert.Equal("improving", Of(improving).Trend);
            Assert.Equal("green", Of(improving).Risk);

            Assert.Equal("stable", Of(stable).Trend);
            Assert.Equal("no_data", Of(oneTerm).Trend);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task ScopeFilter_RestrictsToAccessibleClasses()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var f = await SetUpAsync(db);
            var otherClassId = Guid.NewGuid();
            db.Classes.Add(new Class { ClassId = otherClassId, SchoolId = f.SchoolId, Name = "12B", GradeLevel = 12, CreatedAt = DateTime.UtcNow });
            var learner = Learner(f, "Mine"); // enrolled in 12A
            await db.SaveChangesAsync();

            var svc = new MatricHubService(db);

            // Caller scoped to 12B only → 12A's learner is invisible.
            var scoped = await svc.GetRiskDashboardAsync(f.SchoolId, new HashSet<Guid> { otherClassId }, null);
            Assert.Single(scoped.Classes);
            Assert.Empty(scoped.Learners);

            // Unrestricted (oversight) → sees 12A + 12B and the learner.
            var all = await svc.GetRiskDashboardAsync(f.SchoolId, null, null);
            Assert.Equal(2, all.Classes.Count);
            Assert.Contains(all.Learners, l => l.StudentId == learner);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task MissingWork_OnlyCountsSubjectsTheLearnerTakes()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var f = await SetUpAsync(db);
            var inCur = f.Current.StartDate.AddDays(5);
            var learner = Learner(f, "Chooser");
            await db.SaveChangesAsync();

            // Enrolled (LearnerSubjects) in Mathematics ONLY.
            var maths = ClassSubjectFor(f, "Mathematics");
            var acc = ClassSubjectFor(f, "Accounting");
            await db.SaveChangesAsync();
            var mathsSubjectId = db.ClassSubjects.AsNoTracking()
                .Where(cs => cs.ClassSubjectId == maths).Select(cs => cs.SubjectId).Single();
            var yearId = db.AcademicYears.AsNoTracking().Select(y => y.AcademicYearId).Single();
            db.LearnerSubjects.Add(new LearnerSubject
            {
                LearnerSubjectId = Guid.NewGuid(), StudentId = learner, SubjectId = mathsSubjectId,
                AcademicYearId = yearId, SchoolId = f.SchoolId, EnrolledAt = DateTime.UtcNow,
            });

            Mark(f, maths, learner, null, inCur);
            Mark(f, maths, learner, null, inCur);  // 2 missing in the taken subject → amber
            Mark(f, acc, learner, null, inCur);    // Accounting not taken → must NOT count
            Mark(f, acc, learner, null, inCur);
            Mark(f, acc, learner, null, inCur);
            await db.SaveChangesAsync();

            var dash = await new MatricHubService(db).GetRiskDashboardAsync(f.SchoolId, null, null);
            var row = dash.Learners.Single(l => l.StudentId == learner);

            var subject = Assert.Single(row.Subjects); // Accounting produced no row at all
            Assert.Equal("Mathematics", subject.SubjectName);
            Assert.Equal(2, subject.MissingAssessments);
            Assert.Equal("amber", subject.Risk); // 1–2 missing, no marks yet
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task GradeOverview_FlagsPriorities_AndSortsRedFirst()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var f = await SetUpAsync(db);
            var inPrev = f.Previous.StartDate.AddDays(5);
            var inCur = f.Current.StartDate.AddDays(5);

            var crisis = Learner(f, "Crisis"); // 2 red subjects + declining + 3 missing
            var fine = Learner(f, "Fine");
            await db.SaveChangesAsync();

            var maths = ClassSubjectFor(f, "Mathematics");
            Assessment(f, maths, inPrev, (crisis, 70), (fine, 70));
            Assessment(f, maths, inCur, (crisis, 45), (fine, 75)); // crisis declining, avg < 60 → red; fine within ±5 → stable

            var acc = ClassSubjectFor(f, "Accounting");
            Assessment(f, acc, inCur, (crisis, 30), (fine, 82));   // crisis red on average
            Assessment(f, acc, inCur, (crisis, null), (fine, 80)); // crisis misses 3 the class did
            Assessment(f, acc, inCur, (crisis, null), (fine, 80));
            Assessment(f, acc, inCur, (crisis, null), (fine, 80));
            await db.SaveChangesAsync();

            var overview = await new MatricHubService(db).GetGradeOverviewAsync(f.SchoolId, null);

            Assert.Equal(2, overview.TotalLearners);
            Assert.Equal("Crisis", overview.Learners[0].Name.Split(' ')[0]); // red first
            var c = overview.Learners[0];
            Assert.Equal("red", c.OverallRisk);
            Assert.Equal(2, c.RedSubjects.Count);
            Assert.Equal(3, c.MissingAssessments);
            Assert.Contains("2 subjects at red risk", c.PriorityFlags);
            Assert.Contains("Declining in Mathematics", c.PriorityFlags);
            Assert.Contains("3 missing assessments", c.PriorityFlags);
            Assert.Equal("green", overview.Learners[1].OverallRisk);
            Assert.Empty(overview.Learners[1].PriorityFlags);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
