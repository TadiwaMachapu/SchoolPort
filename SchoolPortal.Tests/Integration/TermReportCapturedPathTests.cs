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
}
