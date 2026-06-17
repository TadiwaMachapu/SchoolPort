using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Subjects;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Step 9.5 (Build #6b + H1) — class-subject teacher assignment. Verifies the H1 tenant-isolation
/// fix (cross-school classId/subjectId/teacherId are rejected as 404, never mutated/linked) and that
/// the teacher roster is scoped to the caller's school. Real Postgres, two seeded schools.
/// </summary>
[Collection("Postgres")]
public class ClassSubjectAssignmentTests
{
    private readonly PostgresFixture _pg;
    public ClassSubjectAssignmentTests(PostgresFixture pg) => _pg = pg;

    private static readonly Guid SchoolA = Guid.NewGuid();
    private static readonly Guid SchoolB = Guid.NewGuid();
    private static readonly Guid ClassA = Guid.NewGuid();
    private static readonly Guid ClassB = Guid.NewGuid();
    private static readonly Guid SubjectA = Guid.NewGuid();
    private static readonly Guid SubjectB = Guid.NewGuid();
    private static readonly Guid TeacherA = Guid.NewGuid();
    private static readonly Guid TeacherB = Guid.NewGuid();
    private static readonly Guid TeacherUserA = Guid.NewGuid();
    private static readonly Guid TeacherUserB = Guid.NewGuid();

    private static ICurrentUserService UserInSchool(Guid schoolId)
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(x => x.SchoolId).Returns(schoolId);
        return m.Object;
    }

    private static async Task SeedTwoSchoolsAsync(SchoolPortalDbContext db)
    {
        await PositionsSeedData.SeedAsync(db, NullLogger.Instance);
        db.Schools.AddRange(
            new School { SchoolId = SchoolA, Name = "School A", IsActive = true, CreatedAt = DateTime.UtcNow },
            new School { SchoolId = SchoolB, Name = "School B", IsActive = true, CreatedAt = DateTime.UtcNow });

        User U(Guid id, Guid school, string email) => new()
        { UserId = id, SchoolId = school, Email = email, PasswordHash = "x", FirstName = "T", LastName = email, Role = "Teacher", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(U(TeacherUserA, SchoolA, "ta"), U(TeacherUserB, SchoolB, "tb"));
        db.Teachers.AddRange(
            new Teacher { TeacherId = TeacherA, UserId = TeacherUserA, SchoolId = SchoolA, CreatedAt = DateTime.UtcNow },
            new Teacher { TeacherId = TeacherB, UserId = TeacherUserB, SchoolId = SchoolB, CreatedAt = DateTime.UtcNow });
        db.Subjects.AddRange(
            new Subject { SubjectId = SubjectA, SchoolId = SchoolA, Name = "Maths A", Code = "MA", CreatedAt = DateTime.UtcNow },
            new Subject { SubjectId = SubjectB, SchoolId = SchoolB, Name = "Maths B", Code = "MB", CreatedAt = DateTime.UtcNow });
        db.Classes.AddRange(
            new Class { ClassId = ClassA, SchoolId = SchoolA, Name = "10A", CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassB, SchoolId = SchoolB, Name = "10B", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static BulkClassSubjectRequest Req(Guid classId, Guid subjectId, Guid? teacherId) =>
        new() { ClassSubjects = { new ClassSubjectItem { ClassId = classId, SubjectId = subjectId, TeacherId = teacherId } } };

    [Fact]
    public async Task BulkAssign_EnforcesTenantIsolation_AndAssignsInSchool()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedTwoSchoolsAsync(db);
            var svc = new SubjectService(db, UserInSchool(SchoolA)); // caller is in School A

            // Foreign CLASS (School B) → 404.
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.BulkAssignClassSubjectsAsync(Req(ClassB, SubjectA, TeacherA)));
            // Foreign SUBJECT (School B) → 404.
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.BulkAssignClassSubjectsAsync(Req(ClassA, SubjectB, TeacherA)));
            // Foreign TEACHER (School B) → 404.
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.BulkAssignClassSubjectsAsync(Req(ClassA, SubjectA, TeacherB)));

            // Nothing was written by the rejected attempts.
            Assert.Empty(await db.ClassSubjects.AsNoTracking().ToListAsync());

            // Fully in-school assignment succeeds and links the in-school teacher.
            await svc.BulkAssignClassSubjectsAsync(Req(ClassA, SubjectA, TeacherA));
            var cs = await db.ClassSubjects.AsNoTracking()
                .SingleAsync(x => x.ClassId == ClassA && x.SubjectId == SubjectA);
            Assert.Equal(TeacherA, cs.TeacherId);
            Assert.Equal(SchoolA, cs.SchoolId);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task GetTeachers_IsScopedToCallerSchool()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedTwoSchoolsAsync(db);
            var svc = new SubjectService(db, UserInSchool(SchoolA));
            var teachers = await svc.GetTeachersAsync();
            Assert.Equal(TeacherA, Assert.Single(teachers).TeacherId); // never School B's teacher
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    // Structural guarantee behind "401/403 for non-academics.manage": the whole controller (so both
    // /bulk and /teachers) carries a class-level academics.manage requirement. Reflection, no DB.
    [Fact]
    public void ClassSubjectsController_RequiresAcademicsManage()
    {
        var attr = typeof(ClassSubjectsController)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .SingleOrDefault();
        Assert.NotNull(attr);
        Assert.Equal(PermissionKeys.AcademicsManage, attr!.PermissionKey);
    }
}
