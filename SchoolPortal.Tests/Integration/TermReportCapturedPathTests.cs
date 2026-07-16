using System.Net;
using System.Text.Json;
using SchoolPortal.Data.Entities;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Sprint 1.5.3 #2 fix — the Term Report reads the CAPTURED marks path (the same source as the
/// at-risk primitive), not the legacy Submission-join path. Pins the two behaviours that proves:
/// a directly-captured mark with NO submission (Sprint 1.5.2.5 decoupled Grade from Submission) is
/// SEEN (the old path missed it entirely), and an absent mark is EXCLUDED. Also covers #3 — CAPS
/// achievement level populates for an FET subject.
/// </summary>
[Collection("SecurityApi")]
public class TermReportCapturedPathTests
{
    private readonly ApiFactory _api;
    public TermReportCapturedPathTests(ApiFactory api) => _api = api;

    [Fact]
    public async Task TermReport_ReadsCapturedMarks_SeesSubmissionlessGrade_ExcludesAbsent_AndSetsCapsLevel()
    {
        var schoolId = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolId, "Staff", "Principal"); // marks.view_all + reporting.view

        var (classId, termId, studentId) = await _api.WithScopeAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var yearId = Guid.NewGuid();
            db.AcademicYears.Add(new AcademicYear { AcademicYearId = yearId, SchoolId = schoolId, Year = now.Year, StartDate = now.AddDays(-60), EndDate = now.AddDays(60), CreatedAt = now });
            var tId = Guid.NewGuid();
            db.Terms.Add(new Term { TermId = tId, AcademicYearId = yearId, SchoolId = schoolId, TermNumber = 3, StartDate = now.AddDays(-30), EndDate = now.AddDays(30), IsCurrent = true, CreatedAt = now });

            var cId = Guid.NewGuid();
            db.Classes.Add(new Class { ClassId = cId, SchoolId = schoolId, Name = "12A", GradeLevel = 12, CreatedAt = now });
            var subjId = Guid.NewGuid();
            db.Subjects.Add(new Subject { SubjectId = subjId, SchoolId = schoolId, Name = "Physical Sciences", Code = "PHY", CapsPhase = "FET", CreatedAt = now });
            var csId = Guid.NewGuid();
            db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = csId, ClassId = cId, SubjectId = subjId, SchoolId = schoolId, CreatedAt = now });

            var stuUserId = Guid.NewGuid();
            db.Users.Add(new User { UserId = stuUserId, SchoolId = schoolId, Email = $"stu_{stuUserId:N}@t.local", PasswordHash = "x", FirstName = "Lethabo", LastName = "Test", Role = "Student", Identity = "Learner", IsActive = true, CreatedAt = now });
            var sId = Guid.NewGuid();
            db.Students.Add(new Student { StudentId = sId, SchoolId = schoolId, UserId = stuUserId, StudentNumber = "S1", CreatedAt = now });
            db.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = cId, StudentId = sId, SchoolId = schoolId, IsActive = true, EnrolledAt = now });

            // A1: directly-captured mark, NO submission — invisible to the old Submission-join path,
            // visible to the captured path. 60% → CAPS L5.
            var a1 = Guid.NewGuid();
            db.Assignments.Add(new Assignment { AssignmentId = a1, ClassSubjectId = csId, SchoolId = schoolId, Title = "T1", DueAt = now.AddDays(-5), MaxMarks = 100, CreatedAt = now, CreatedByUserId = principal.UserId });
            db.Grades.Add(new Grade { GradeId = Guid.NewGuid(), StudentId = sId, AssignmentId = a1, SubmissionId = null, SchoolId = schoolId, Score = 60, IsAbsent = false, GradedByUserId = principal.UserId, GradedAt = now });

            // A2: absent mark — must be EXCLUDED (else it would drag the average down).
            var a2 = Guid.NewGuid();
            db.Assignments.Add(new Assignment { AssignmentId = a2, ClassSubjectId = csId, SchoolId = schoolId, Title = "T2", DueAt = now.AddDays(-3), MaxMarks = 100, CreatedAt = now, CreatedByUserId = principal.UserId });
            db.Grades.Add(new Grade { GradeId = Guid.NewGuid(), StudentId = sId, AssignmentId = a2, SubmissionId = null, SchoolId = schoolId, Score = null, IsAbsent = true, GradedByUserId = principal.UserId, GradedAt = now });

            await db.SaveChangesAsync();
            return (cId, tId, sId);
        });

        var resp = await _api.ClientFor(principal).GetAsync($"/api/reports/term-report/{classId}/{termId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var students = doc.RootElement.GetProperty("students");
        Assert.Equal(1, students.GetArrayLength());
        var s = students[0];
        Assert.Equal(studentId, Guid.Parse(s.GetProperty("studentId").GetString()!));

        var subjects = s.GetProperty("subjectResults");
        Assert.Equal(1, subjects.GetArrayLength());                        // the captured subject shows up
        Assert.Equal("Physical Sciences", subjects[0].GetProperty("subjectName").GetString());
        Assert.Equal(60.0, subjects[0].GetProperty("average").GetDouble()); // absent excluded → 60, not ~30
        Assert.Equal(5, subjects[0].GetProperty("capsLevel").GetInt32());   // #3 — CAPS L5 for an FET subject
        Assert.Equal(60.0, s.GetProperty("overallAverage").GetDouble());
    }

    /// <summary>
    /// Sprint 1.5.3 — the Term Report (learner card) and the at-risk tab show the SAME overall
    /// average, computed the SAME way: term-scoped avg-of-subject-averages. A prior-term mark must
    /// not shift either number.
    /// </summary>
    [Fact]
    public async Task OverallAverage_ConsistentAcrossSurfaces_TermScoped()
    {
        var schoolId = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolId, "Staff", "Principal");

        var (classId, termId) = await _api.WithScopeAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var yearId = Guid.NewGuid();
            db.AcademicYears.Add(new AcademicYear { AcademicYearId = yearId, SchoolId = schoolId, Year = now.Year, StartDate = now.AddDays(-200), EndDate = now.AddDays(60), CreatedAt = now });
            var prevId = Guid.NewGuid();
            var curId = Guid.NewGuid();
            db.Terms.Add(new Term { TermId = prevId, AcademicYearId = yearId, SchoolId = schoolId, TermNumber = 2, StartDate = now.AddDays(-200), EndDate = now.AddDays(-101), CreatedAt = now });
            db.Terms.Add(new Term { TermId = curId, AcademicYearId = yearId, SchoolId = schoolId, TermNumber = 3, StartDate = now.AddDays(-30), EndDate = now.AddDays(30), IsCurrent = true, CreatedAt = now });

            var cId = Guid.NewGuid();
            db.Classes.Add(new Class { ClassId = cId, SchoolId = schoolId, Name = "12A", GradeLevel = 12, CreatedAt = now });

            var stuUserId = Guid.NewGuid();
            db.Users.Add(new User { UserId = stuUserId, SchoolId = schoolId, Email = $"stu_{stuUserId:N}@t.local", PasswordHash = "x", FirstName = "Lethabo", LastName = "Test", Role = "Student", Identity = "Learner", IsActive = true, CreatedAt = now });
            var sId = Guid.NewGuid();
            db.Students.Add(new Student { StudentId = sId, SchoolId = schoolId, UserId = stuUserId, StudentNumber = "S1", CreatedAt = now });
            db.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = cId, StudentId = sId, SchoolId = schoolId, IsActive = true, EnrolledAt = now });

            Guid Subject(string name, string code)
            {
                var id = Guid.NewGuid();
                db.Subjects.Add(new Subject { SubjectId = id, SchoolId = schoolId, Name = name, Code = code, CapsPhase = "FET", CreatedAt = now });
                var cs = Guid.NewGuid();
                db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = cs, ClassId = cId, SubjectId = id, SchoolId = schoolId, CreatedAt = now });
                return cs;
            }
            void Mark(Guid csId, DateTime dueAt, decimal score)
            {
                var aId = Guid.NewGuid();
                db.Assignments.Add(new Assignment { AssignmentId = aId, ClassSubjectId = csId, SchoolId = schoolId, Title = "A", DueAt = dueAt, MaxMarks = 100, CreatedAt = now, CreatedByUserId = principal.UserId });
                db.Grades.Add(new Grade { GradeId = Guid.NewGuid(), StudentId = sId, AssignmentId = aId, SubmissionId = null, SchoolId = schoolId, Score = score, IsAbsent = false, GradedByUserId = principal.UserId, GradedAt = now });
            }

            var maths = Subject("Mathematics", "MAT");
            Mark(maths, now.AddDays(-150), 80);  // previous term — must NOT count
            Mark(maths, now.AddDays(-5), 40);    // this term
            var eng = Subject("English", "ENG");
            Mark(eng, now.AddDays(-5), 60);      // this term

            await db.SaveChangesAsync();
            return (cId, curId);
        });

        var termReport = await _api.ClientFor(principal).GetAsync($"/api/reports/term-report/{classId}/{termId}");
        var atRisk = await _api.ClientFor(principal).GetAsync($"/api/reports/at-risk?classId={classId}&termId={termId}");
        Assert.Equal(HttpStatusCode.OK, termReport.StatusCode);
        Assert.Equal(HttpStatusCode.OK, atRisk.StatusCode);

        using var trDoc = JsonDocument.Parse(await termReport.Content.ReadAsStringAsync());
        using var arDoc = JsonDocument.Parse(await atRisk.Content.ReadAsStringAsync());

        var trOverall = trDoc.RootElement.GetProperty("students")[0].GetProperty("overallAverage").GetDouble();
        var arOverall = arDoc.RootElement[0].GetProperty("overallAverage").GetDouble();

        // Independent term-scoped calc: Maths = 40 (this term; the 80 last term is excluded),
        // English = 60 → avg-of-subject-averages = (40 + 60)/2 = 50.0. Both surfaces must agree.
        Assert.Equal(50.0, trOverall);
        Assert.Equal(50.0, arOverall);
        Assert.Equal(trOverall, arOverall);
    }
}
