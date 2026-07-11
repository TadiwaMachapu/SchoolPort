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
/// Sprint 1.5.2.5 — cross-tenant WRITE protection for the marks-capture surface (the most
/// security-critical write after finance). bulk-capture carries THREE body ids (taskId,
/// classSubjectId, studentId); each direction is proven with REAL foreign-school resources so
/// linkage is blocked, not mere non-existence. Every case asserts BOTH the status AND that no
/// grade/task row was created.
/// </summary>
[Collection("SecurityApi")]
public class MarksCaptureCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public MarksCaptureCrossTenantWriteTests(ApiFactory api) => _api = api;

    [CrossTenantGuard(typeof(GradebookController), nameof(GradebookController.BulkCapture))]
    [CrossTenantGuard(typeof(GradebookController), nameof(GradebookController.CreateTask))]
    [CrossTenantGuard(typeof(GradebookController), nameof(GradebookController.UpdateTask))]
    [Fact]
    public async Task BulkCapture_RejectsClassSubjectFromAnotherSchool()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var (foreignCs, foreignTask, foreignStudent) = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var cs = AddClassSubject(db, schoolB, out var classB);
            var teacherB = AddUser(db, schoolB, "Teacher", "Staff");
            var task = AddAssignment(db, schoolB, cs, teacherB);
            var s = AddStudent(db, schoolB);
            AddEnrollment(db, schoolB, classB, s);
            await db.SaveChangesAsync();
            return (cs, task, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/gradebook/bulk-capture", new
        {
            taskId = foreignTask,
            classSubjectId = foreignCs,
            entries = new[] { new { studentId = foreignStudent, score = 40, isAbsent = false } },
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.AssignmentId == foreignTask)));
    }

    [Fact]
    public async Task BulkCapture_RejectsCrossSchoolStudent()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var (ownCs, ownTask, foreignStudent) = await _api.WithScopeAsync(async db =>
        {
            var teacherId = AddTeacherFor(db, schoolA, teacher.UserId);
            var cs = AddClassSubject(db, schoolA, out var classId, teacherId); // caller's own scoped class
            var task = AddAssignment(db, schoolA, cs, teacher.UserId);
            var schoolB = AddSchool(db);
            var s = AddStudent(db, schoolB);
            await db.SaveChangesAsync();
            return (cs, task, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/gradebook/bulk-capture", new
        {
            taskId = ownTask,
            classSubjectId = ownCs,
            entries = new[] { new { studentId = foreignStudent, score = 40, isAbsent = false } },
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.StudentId == foreignStudent)));
    }

    [Fact]
    public async Task BulkCapture_OutOfScopeClass_SameSchool_Returns403_AndCreatesNoRow()
    {
        // In-school but not the caller's class: a SubjectTeacher may not capture marks for a
        // colleague's class — scope (403), not tenancy (404).
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var (otherCs, otherTask, student) = await _api.WithScopeAsync(async db =>
        {
            var cs = AddClassSubject(db, schoolA, out var classId); // no teacher — out of caller's scope
            var otherTeacher = AddUser(db, schoolA, "Teacher", "Staff");
            var task = AddAssignment(db, schoolA, cs, otherTeacher);
            var s = AddStudent(db, schoolA);
            AddEnrollment(db, schoolA, classId, s);
            await db.SaveChangesAsync();
            return (cs, task, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/gradebook/bulk-capture", new
        {
            taskId = otherTask,
            classSubjectId = otherCs,
            entries = new[] { new { studentId = student, score = 40, isAbsent = false } },
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.AssignmentId == otherTask)));
    }

    [Fact]
    public async Task BulkCapture_RejectsTaskFromAnotherSchool()
    {
        // taskId direction in isolation: own (scoped) class-subject, but the task belongs to
        // another school — the ClassSubjectId+SchoolId match in ResolveTask dead-ends it.
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var (ownCs, foreignTask, ownStudent) = await _api.WithScopeAsync(async db =>
        {
            var teacherId = AddTeacherFor(db, schoolA, teacher.UserId);
            var cs = AddClassSubject(db, schoolA, out var classId, teacherId);
            var s = AddStudent(db, schoolA);
            AddEnrollment(db, schoolA, classId, s);
            var schoolB = AddSchool(db);
            var csB = AddClassSubject(db, schoolB, out _);
            var teacherB = AddUser(db, schoolB, "Teacher", "Staff");
            var task = AddAssignment(db, schoolB, csB, teacherB);
            await db.SaveChangesAsync();
            return (cs, task, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/gradebook/bulk-capture", new
        {
            taskId = foreignTask,
            classSubjectId = ownCs,
            entries = new[] { new { studentId = ownStudent, score = 40, isAbsent = false } },
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.AssignmentId == foreignTask)));
    }

    [Fact]
    public async Task BulkCapture_RejectsStudentNotInClass()
    {
        // SAME school, real learner — but enrolled in a DIFFERENT class. The enrolment HashSet
        // is per class-subject, so this dead-ends exactly like a foreign student (404, no row).
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var (ownCs, ownTask, otherClassStudent) = await _api.WithScopeAsync(async db =>
        {
            var teacherId = AddTeacherFor(db, schoolA, teacher.UserId);
            var cs = AddClassSubject(db, schoolA, out _, teacherId);
            var task = AddAssignment(db, schoolA, cs, teacher.UserId);
            var otherCs = AddClassSubject(db, schoolA, out var otherClassId);
            var s = AddStudent(db, schoolA);
            AddEnrollment(db, schoolA, otherClassId, s);
            await db.SaveChangesAsync();
            return (cs, task, s);
        });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/gradebook/bulk-capture", new
        {
            taskId = ownTask,
            classSubjectId = ownCs,
            entries = new[] { new { studentId = otherClassStudent, score = 40, isAbsent = false } },
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Grades.CountAsync(g => g.StudentId == otherClassStudent)));
    }

    [Fact]
    public async Task CreateTask_ForeignClassSubject_Returns404_AndCreatesNoRow()
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

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/gradebook/tasks", new
        {
            classSubjectId = foreignCs, title = "Sneaky task", taskType = "Test", termNumber = 1, maxMarks = 50, hasRubric = false,
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Assignments.CountAsync(a => a.ClassSubjectId == foreignCs)));
    }

    [Fact]
    public async Task UpdateTask_ForeignTask_Returns404_AndMutatesNothing()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreignTask = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var cs = AddClassSubject(db, schoolB, out _);
            var teacherB = AddUser(db, schoolB, "Teacher", "Staff");
            var task = AddAssignment(db, schoolB, cs, teacherB);
            await db.SaveChangesAsync();
            return task;
        });

        var resp = await _api.ClientFor(teacher).PutAsJsonAsync($"/api/gradebook/tasks/{foreignTask}", new
        {
            title = "Hijacked", taskType = "Test", termNumber = 1, maxMarks = 10,
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var title = await _api.WithScopeAsync(db => db.Assignments.Where(a => a.AssignmentId == foreignTask).Select(a => a.Title).SingleAsync());
        Assert.NotEqual("Hijacked", title);
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
        var studentId = Guid.NewGuid();
        db.Students.Add(new Student { StudentId = studentId, SchoolId = schoolId, UserId = userId, StudentNumber = "N" + userId.ToString("N")[..6], CreatedAt = DateTime.UtcNow });
        return studentId;
    }

    private static void AddEnrollment(SchoolPortalDbContext db, Guid schoolId, Guid classId, Guid studentId)
        => db.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = classId, StudentId = studentId, SchoolId = schoolId, EnrolledAt = DateTime.UtcNow, IsActive = true });

    private static Guid AddClassSubject(SchoolPortalDbContext db, Guid schoolId, out Guid classId, Guid? teacherId = null)
    {
        classId = Guid.NewGuid();
        db.Classes.Add(new Class { ClassId = classId, SchoolId = schoolId, Name = "C" + classId.ToString("N")[..4], TeacherId = teacherId, CreatedAt = DateTime.UtcNow });
        var subjectId = Guid.NewGuid();
        db.Subjects.Add(new Subject { SubjectId = subjectId, SchoolId = schoolId, Name = "Sub" + subjectId.ToString("N")[..4], Code = "S" + subjectId.ToString("N")[..3], CreatedAt = DateTime.UtcNow });
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
