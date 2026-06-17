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
/// Step 10 Inventory-B — Academics cluster. Cross-tenant WRITE protection: a Principal
/// (academics.manage) in school A must not be able to mutate/link another school's resources by
/// supplying a foreign id. Every case asserts BOTH the response status AND that no row was mutated
/// (a 404 with a silent write would be the worst outcome). Runs on the real HTTP pipeline via
/// <see cref="ApiFactory"/> with a real LoginAsync-issued token.
///
/// Reference: ClassSubjects bulk-assign is covered by ClassSubjectAssignmentTests (H1).
/// </summary>
[Collection("SecurityApi")]
public class AcademicsCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public AcademicsCrossTenantWriteTests(ApiFactory api) => _api = api;

    // ---- Classes --------------------------------------------------------------------------------

    // [CrossTenantGuard] registrations for the Academics cluster (scanner ratchet). ClassSubjects
    // bulk-assign (H1) is exercised by Integration/ClassSubjectAssignmentTests; registered here as
    // its logical cluster.
    [CrossTenantGuard(typeof(ClassSubjectsController), nameof(ClassSubjectsController.BulkAssign))]
    [CrossTenantGuard(typeof(ClassesController), nameof(ClassesController.CreateClass))]
    [CrossTenantGuard(typeof(ClassesController), nameof(ClassesController.UpdateClass))]
    [CrossTenantGuard(typeof(ClassesController), nameof(ClassesController.DeleteClass))]
    [CrossTenantGuard(typeof(SubjectsController), nameof(SubjectsController.UpdateSubject))]
    [CrossTenantGuard(typeof(SubjectsController), nameof(SubjectsController.DeleteSubject))]
    [CrossTenantGuard(typeof(PathwaysController), nameof(PathwaysController.Enrol))]
    [CrossTenantGuard(typeof(PathwaysController), nameof(PathwaysController.Withdraw))]
    [Fact]
    public async Task CreateClass_ForeignTeacherInBody_Returns404_AndCreatesNoClass()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignTeacher = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var t = AddTeacher(db, schoolB);
            await db.SaveChangesAsync();
            return t;
        });

        var resp = await _api.ClientFor(principal).PostAsJsonAsync("/api/classes",
            new { name = "10A", gradeLevel = 10, teacherId = foreignTeacher });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var count = await _api.WithScopeAsync(db => db.Classes.CountAsync(c => c.SchoolId == schoolA));
        Assert.Equal(0, count); // no row written
    }

    [Fact]
    public async Task UpdateClass_ForeignClassId_Returns404_AndLeavesItUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignClass = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var c = AddClass(db, schoolB, name: "B-Original");
            await db.SaveChangesAsync();
            return c;
        });

        var resp = await _api.ClientFor(principal).PutAsJsonAsync($"/api/classes/{foreignClass}",
            new { name = "Hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var name = await _api.WithScopeAsync(db => db.Classes.Where(c => c.ClassId == foreignClass).Select(c => c.Name).SingleAsync());
        Assert.Equal("B-Original", name); // untouched
    }

    [Fact]
    public async Task UpdateClass_ForeignTeacherInBody_Returns404_AndLeavesClassUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var (localClass, foreignTeacher) = await _api.WithScopeAsync(async db =>
        {
            var c = AddClass(db, schoolA, name: "A-Class");      // legit class in caller's school, no teacher
            var schoolB = AddSchool(db);
            var t = AddTeacher(db, schoolB);
            await db.SaveChangesAsync();
            return (c, t);
        });

        var resp = await _api.ClientFor(principal).PutAsJsonAsync($"/api/classes/{localClass}",
            new { name = "A-Class", teacherId = foreignTeacher });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var teacherId = await _api.WithScopeAsync(db => db.Classes.Where(c => c.ClassId == localClass).Select(c => c.TeacherId).SingleAsync());
        Assert.Null(teacherId); // foreign teacher was NOT linked
    }

    [Fact]
    public async Task DeleteClass_ForeignClassId_Returns404_AndClassStillExists()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignClass = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var c = AddClass(db, schoolB, name: "B-Class");
            await db.SaveChangesAsync();
            return c;
        });

        var resp = await _api.ClientFor(principal).DeleteAsync($"/api/classes/{foreignClass}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Classes.AnyAsync(c => c.ClassId == foreignClass))); // still there
    }

    // ---- Subjects -------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateSubject_ForeignSubjectId_Returns404_AndUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignSubject = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var s = AddSubject(db, schoolB, name: "B-Maths");
            await db.SaveChangesAsync();
            return s;
        });

        var resp = await _api.ClientFor(principal).PutAsJsonAsync($"/api/subjects/{foreignSubject}",
            new { name = "Hijacked", code = "HIJ" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var name = await _api.WithScopeAsync(db => db.Subjects.Where(s => s.SubjectId == foreignSubject).Select(s => s.Name).SingleAsync());
        Assert.Equal("B-Maths", name);
    }

    [Fact]
    public async Task DeleteSubject_ForeignSubjectId_Returns404_AndStillExists()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignSubject = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var s = AddSubject(db, schoolB, name: "B-Science");
            await db.SaveChangesAsync();
            return s;
        });

        var resp = await _api.ClientFor(principal).DeleteAsync($"/api/subjects/{foreignSubject}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Subjects.AnyAsync(s => s.SubjectId == foreignSubject)));
    }

    // ---- Pathways enrol / withdraw --------------------------------------------------------------

    [Fact]
    public async Task Enrol_ForeignStudent_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var (foreignStudent, localSubject) = await _api.WithScopeAsync(async db =>
        {
            var localSub = AddSubject(db, schoolA, name: "A-Maths");
            var schoolB = AddSchool(db);
            var stu = AddStudent(db, schoolB);
            await db.SaveChangesAsync();
            return (stu, localSub);
        });

        var resp = await _api.ClientFor(principal).PostAsJsonAsync("/api/pathways/enrol",
            new { studentId = foreignStudent, subjectId = localSubject, academicYearId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.LearnerSubjects.CountAsync(l => l.SchoolId == schoolA)));
    }

    [Fact]
    public async Task Enrol_ForeignSubject_Returns404_AndCreatesNoRow()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var (localStudent, foreignSubject) = await _api.WithScopeAsync(async db =>
        {
            var stu = AddStudent(db, schoolA);
            var schoolB = AddSchool(db);
            var sub = AddSubject(db, schoolB, name: "B-Maths");
            await db.SaveChangesAsync();
            return (stu, sub);
        });

        var resp = await _api.ClientFor(principal).PostAsJsonAsync("/api/pathways/enrol",
            new { studentId = localStudent, subjectId = foreignSubject, academicYearId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.LearnerSubjects.CountAsync(l => l.SchoolId == schoolA)));
    }

    [Fact]
    public async Task Withdraw_ForeignLearnerSubject_Returns404_AndStillExists()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignLs = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var stu = AddStudent(db, schoolB);
            var sub = AddSubject(db, schoolB, name: "B-Maths");
            var yr = AddAcademicYear(db, schoolB);
            var ls = new LearnerSubject
            {
                LearnerSubjectId = Guid.NewGuid(), StudentId = stu, SubjectId = sub,
                AcademicYearId = yr, SchoolId = schoolB, EnrolledAt = DateTime.UtcNow,
            };
            db.LearnerSubjects.Add(ls);
            await db.SaveChangesAsync();
            return ls.LearnerSubjectId;
        });

        var resp = await _api.ClientFor(principal).DeleteAsync($"/api/pathways/{foreignLs}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.LearnerSubjects.AnyAsync(l => l.LearnerSubjectId == foreignLs)));
    }

    // ---- seed helpers (add-only; caller saves) --------------------------------------------------

    private static Guid AddSchool(SchoolPortalDbContext db)
    {
        var id = Guid.NewGuid();
        db.Schools.Add(new School { SchoolId = id, Name = "S" + id.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddTeacher(SchoolPortalDbContext db, Guid schoolId)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User { UserId = userId, SchoolId = schoolId, Email = $"t_{userId:N}@test.local", PasswordHash = "x", FirstName = "T", LastName = "X", Role = "Teacher", Identity = "Staff", IsActive = true, CreatedAt = DateTime.UtcNow });
        var teacherId = Guid.NewGuid();
        db.Teachers.Add(new Teacher { TeacherId = teacherId, UserId = userId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
        return teacherId;
    }

    private static Guid AddSubject(SchoolPortalDbContext db, Guid schoolId, string name)
    {
        var id = Guid.NewGuid();
        db.Subjects.Add(new Subject { SubjectId = id, SchoolId = schoolId, Name = name, Code = name[..Math.Min(4, name.Length)], CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddClass(SchoolPortalDbContext db, Guid schoolId, string name, Guid? teacherId = null)
    {
        var id = Guid.NewGuid();
        db.Classes.Add(new Class { ClassId = id, SchoolId = schoolId, Name = name, TeacherId = teacherId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddStudent(SchoolPortalDbContext db, Guid schoolId)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User { UserId = userId, SchoolId = schoolId, Email = $"s_{userId:N}@test.local", PasswordHash = "x", FirstName = "S", LastName = "X", Role = "Student", Identity = "Learner", IsActive = true, CreatedAt = DateTime.UtcNow });
        var studentId = Guid.NewGuid();
        db.Students.Add(new Student { StudentId = studentId, SchoolId = schoolId, UserId = userId, StudentNumber = "N" + userId.ToString("N")[..6], CreatedAt = DateTime.UtcNow });
        return studentId;
    }

    private static Guid AddAcademicYear(SchoolPortalDbContext db, Guid schoolId)
    {
        var id = Guid.NewGuid();
        db.AcademicYears.Add(new AcademicYear { AcademicYearId = id, SchoolId = schoolId, Year = 2026, StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow });
        return id;
    }
}
