using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 Inventory-B — Teaching cluster. Cross-tenant WRITE protection across the teaching
/// surface. Every case asserts BOTH the response status AND that no row was mutated. Gap tests
/// (Enrolments, Attendance studentId, Submissions assignmentId, Quiz ClassSubjectId, Quiz attempt
/// IDOR) use REAL foreign-school resources to prove cross-tenant linkage is blocked, not mere
/// non-existence. Guarded-confirmation tests lock the protection that was already present.
/// </summary>
[Collection("SecurityApi")]
public class TeachingCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public TeachingCrossTenantWriteTests(ApiFactory api) => _api = api;

    // ---- Enrolments POST /bulk — both directions (was UNGUARDED) --------------------------------

    [CrossTenantGuard(typeof(EnrolmentsController), nameof(EnrolmentsController.BulkEnroll))]
    [CrossTenantGuard(typeof(AttendanceController), nameof(AttendanceController.BulkUpsertAttendance))]
    [CrossTenantGuard(typeof(SubmissionsController), nameof(SubmissionsController.CreateSubmission))]
    [CrossTenantGuard(typeof(QuizzesController), nameof(QuizzesController.CreateQuiz))]
    [CrossTenantGuard(typeof(QuizzesController), nameof(QuizzesController.SubmitAttempt))]
    [CrossTenantGuard(typeof(QuizzesController), nameof(QuizzesController.DeleteQuiz))]
    [CrossTenantGuard(typeof(GradesController), nameof(GradesController.CreateGrade))]
    [Fact]
    public async Task Enrol_ForeignStudentIntoLocalClass_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var itAdmin = await _api.MintTokenAsync(schoolA, "Staff", "ITAdministrator");
        var (localClass, foreignStudent) = await _api.WithScopeAsync(async db =>
        {
            var c = AddClass(db, schoolA);
            var schoolB = AddSchool(db);
            var s = AddStudent(db, schoolB);
            await db.SaveChangesAsync();
            return (c, s);
        });

        var resp = await _api.ClientFor(itAdmin).PostAsJsonAsync("/api/enrolments/bulk",
            new { enrollments = new[] { new { classId = localClass, studentId = foreignStudent } } });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Enrollments.CountAsync(e => e.SchoolId == schoolA)));
    }

    [Fact]
    public async Task Enrol_LocalStudentIntoForeignClass_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var itAdmin = await _api.MintTokenAsync(schoolA, "Staff", "ITAdministrator");
        var (localStudent, foreignClass) = await _api.WithScopeAsync(async db =>
        {
            var s = AddStudent(db, schoolA);
            var schoolB = AddSchool(db);
            var c = AddClass(db, schoolB);
            await db.SaveChangesAsync();
            return (s, c);
        });

        var resp = await _api.ClientFor(itAdmin).PostAsJsonAsync("/api/enrolments/bulk",
            new { enrollments = new[] { new { classId = foreignClass, studentId = localStudent } } });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        // No enrolment anywhere referencing either id.
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Enrollments.CountAsync(e => e.ClassId == foreignClass || e.StudentId == localStudent)));
    }

    // ---- Attendance POST /bulk — studentId body id (was UNGUARDED) ------------------------------

    [Fact]
    public async Task Attendance_ForeignStudentIntoOwnClass_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        // Make the teacher own a class in A (so the class scope check passes — isolating the studentId guard).
        var (ownClass, foreignStudent) = await _api.WithScopeAsync(async db =>
        {
            var teacherId = AddTeacherFor(db, schoolA, teacher.UserId);
            var c = AddClass(db, schoolA, teacherId: teacherId);
            var schoolB = AddSchool(db);
            var s = AddStudent(db, schoolB);
            await db.SaveChangesAsync();
            return (c, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/attendance/bulk",
            new { attendances = new[] { new { classId = ownClass, studentId = foreignStudent, date = DateTime.UtcNow, status = 1 } } });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Attendances.CountAsync(a => a.StudentId == foreignStudent)));
    }

    [Fact]
    public async Task Attendance_ForeignClass_Returns403_AndCreatesNoRow()
    {
        // Class-direction: a class the caller doesn't own (here, another school's) is out of scope → 403.
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var (foreignClass, foreignStudent) = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var c = AddClass(db, schoolB);
            var s = AddStudent(db, schoolB);
            await db.SaveChangesAsync();
            return (c, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/attendance/bulk",
            new { attendances = new[] { new { classId = foreignClass, studentId = foreignStudent, date = DateTime.UtcNow, status = 1 } } });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Attendances.CountAsync(a => a.ClassId == foreignClass)));
    }

    // ---- Submissions POST — assignmentId body id (was UNGUARDED) --------------------------------

    [Fact]
    public async Task Submit_ForeignAssignment_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var learner = await _api.MintTokenAsync(schoolA, "Learner");
        var foreignAssignment = await _api.WithScopeAsync(async db =>
        {
            AddStudentFor(db, schoolA, learner.UserId);   // caller needs a Student record
            var schoolB = AddSchool(db);
            var cs = AddClassSubject(db, schoolB, out _);
            var creator = AddUser(db, schoolB, "Teacher", "Staff");
            var a = AddAssignment(db, schoolB, cs, creator);
            await db.SaveChangesAsync();
            return a;
        });

        using var form = new MultipartFormDataContent
        {
            { new StringContent(foreignAssignment.ToString()), "assignmentId" },
            { new StringContent("hi"), "comments" },
        };
        var resp = await _api.ClientFor(learner).PostAsync("/api/submissions", form);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Submissions.CountAsync(s => s.AssignmentId == foreignAssignment)));
    }

    // ---- Quizzes ---------------------------------------------------------------------------------

    [Fact]
    public async Task QuizCreate_ForeignClassSubject_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreignCs = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var cs = AddClassSubject(db, schoolB, out _);
            await db.SaveChangesAsync();
            return cs;
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/quizzes",
            new { classSubjectId = foreignCs, title = "Hijack Quiz", questions = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Quizzes.CountAsync(q => q.ClassSubjectId == foreignCs)));
    }

    [Fact]
    public async Task QuizSubmitAttempt_OtherStudentsAttempt_Returns404_AndAttemptUntouched()
    {
        var schoolA = Guid.NewGuid();
        var learner = await _api.MintTokenAsync(schoolA, "Learner");
        var foreignAttempt = await _api.WithScopeAsync(async db =>
        {
            AddStudentFor(db, schoolA, learner.UserId);   // caller is a real, different learner
            var schoolB = AddSchool(db);
            var creator = AddUser(db, schoolB, "Teacher", "Staff");
            var quizId = Guid.NewGuid();
            db.Quizzes.Add(new Quiz { QuizId = quizId, SchoolId = schoolB, Title = "B-Quiz", CreatedByUserId = creator, CreatedAt = DateTime.UtcNow, IsPublished = true });
            var studentB = AddStudent(db, schoolB);
            var attemptId = Guid.NewGuid();
            db.QuizAttempts.Add(new QuizAttempt { AttemptId = attemptId, QuizId = quizId, StudentId = studentB, SchoolId = schoolB, StartedAt = DateTime.UtcNow, IsCompleted = false });
            await db.SaveChangesAsync();
            return attemptId;
        });

        var resp = await _api.ClientFor(learner).PostAsJsonAsync($"/api/quizzes/attempts/{foreignAttempt}/submit",
            new { answers = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var stillOpen = await _api.WithScopeAsync(db => db.QuizAttempts.Where(a => a.AttemptId == foreignAttempt).Select(a => a.IsCompleted).SingleAsync());
        Assert.False(stillOpen); // not scored/completed by the IDOR attempt
    }

    [Fact]
    public async Task QuizDelete_ForeignQuiz_Returns404_AndStillExists()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreignQuiz = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var creator = AddUser(db, schoolB, "Teacher", "Staff");
            var quizId = Guid.NewGuid();
            db.Quizzes.Add(new Quiz { QuizId = quizId, SchoolId = schoolB, Title = "B-Quiz", CreatedByUserId = creator, CreatedAt = DateTime.UtcNow, IsPublished = true });
            await db.SaveChangesAsync();
            return quizId;
        });

        var resp = await _api.ClientFor(teacher).DeleteAsync($"/api/quizzes/{foreignQuiz}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Quizzes.AnyAsync(q => q.QuizId == foreignQuiz)));
    }

    // ---- Grades POST — guarded confirmation (CreateGrade loads submission by id+SchoolId) --------

    [Fact]
    public async Task Grade_ForeignSubmission_Returns404_AndCreatesNoGrade()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreignSubmission = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var cs = AddClassSubject(db, schoolB, out _);
            var creator = AddUser(db, schoolB, "Teacher", "Staff");
            var assignment = AddAssignment(db, schoolB, cs, creator);
            var studentB = AddStudent(db, schoolB);
            var subId = Guid.NewGuid();
            db.Submissions.Add(new Submission { SubmissionId = subId, AssignmentId = assignment, StudentId = studentB, SchoolId = schoolB, SubmittedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return subId;
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/grades",
            new { submissionId = foreignSubmission, score = 90, feedback = "x" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.SubmissionId == foreignSubmission)));
    }

    // ---- seed helpers (add-only; caller saves) --------------------------------------------------

    private static Guid AddSchool(SchoolPortalDbContext db)
    {
        var id = Guid.NewGuid();
        db.Schools.Add(new School { SchoolId = id, Name = "S" + id.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddUser(SchoolPortalDbContext db, Guid schoolId, string role, string identity)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { UserId = id, SchoolId = schoolId, Email = $"u_{id:N}@test.local", PasswordHash = "x", FirstName = "U", LastName = "X", Role = role, Identity = identity, IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddTeacherFor(SchoolPortalDbContext db, Guid schoolId, Guid userId)
    {
        var teacherId = Guid.NewGuid();
        db.Teachers.Add(new Teacher { TeacherId = teacherId, UserId = userId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
        return teacherId;
    }

    private static Guid AddStudent(SchoolPortalDbContext db, Guid schoolId)
    {
        var userId = AddUser(db, schoolId, "Student", "Learner");
        return AddStudentFor(db, schoolId, userId);
    }

    private static Guid AddStudentFor(SchoolPortalDbContext db, Guid schoolId, Guid userId)
    {
        var studentId = Guid.NewGuid();
        db.Students.Add(new Student { StudentId = studentId, SchoolId = schoolId, UserId = userId, StudentNumber = "N" + userId.ToString("N")[..6], CreatedAt = DateTime.UtcNow });
        return studentId;
    }

    private static Guid AddSubject(SchoolPortalDbContext db, Guid schoolId)
    {
        var id = Guid.NewGuid();
        db.Subjects.Add(new Subject { SubjectId = id, SchoolId = schoolId, Name = "Sub" + id.ToString("N")[..4], Code = "S" + id.ToString("N")[..3], CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddClass(SchoolPortalDbContext db, Guid schoolId, Guid? teacherId = null)
    {
        var id = Guid.NewGuid();
        db.Classes.Add(new Class { ClassId = id, SchoolId = schoolId, Name = "C" + id.ToString("N")[..4], TeacherId = teacherId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddClassSubject(SchoolPortalDbContext db, Guid schoolId, out Guid classId)
    {
        classId = AddClass(db, schoolId);
        var subjectId = AddSubject(db, schoolId);
        var id = Guid.NewGuid();
        db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = id, ClassId = classId, SubjectId = subjectId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddAssignment(SchoolPortalDbContext db, Guid schoolId, Guid classSubjectId, Guid createdByUserId)
    {
        var id = Guid.NewGuid();
        db.Assignments.Add(new Assignment { AssignmentId = id, ClassSubjectId = classSubjectId, SchoolId = schoolId, Title = "A" + id.ToString("N")[..4], DueAt = DateTime.UtcNow.AddDays(7), MaxMarks = 100, CreatedByUserId = createdByUserId, CreatedAt = DateTime.UtcNow });
        return id;
    }
}
