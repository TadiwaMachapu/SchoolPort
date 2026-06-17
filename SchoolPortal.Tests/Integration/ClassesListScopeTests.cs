using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Common;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Step 9.5 (Fix #1) — GET /api/classes is self-protecting regardless of the `mine` flag.
/// Verifies the four-case contract end-to-end through the real controller + ClassService +
/// ScopeService against Postgres:
///   1. SubjectTeacher (no academics.manage), mine omitted → scoped list (NOT the whole school).
///   2. Admin/HOD (academics.manage), mine omitted        → full school list.
///   3. Learner / Parent                                  → 403 (neither list).
///   4. Auditor (External identity + marks.view_all)      → full list via the scoped branch
///      (IScopeService returns null for school-wide oversight — oversight is NOT narrowed).
/// </summary>
[Collection("Postgres")]
public class ClassesListScopeTests
{
    private readonly PostgresFixture _pg;
    public ClassesListScopeTests(PostgresFixture pg) => _pg = pg;

    private static readonly Guid School = Guid.NewGuid();
    private static readonly Guid TeacherUser = Guid.NewGuid();
    private static readonly Guid AdminUser = Guid.NewGuid();
    private static readonly Guid LearnerUser = Guid.NewGuid();
    private static readonly Guid ParentUser = Guid.NewGuid();
    private static readonly Guid AuditorUser = Guid.NewGuid();

    private static readonly Guid ClassA = Guid.NewGuid();   // teacher teaches Maths here
    private static readonly Guid ClassB = Guid.NewGuid();   // someone else's class
    private static readonly Guid Maths = Guid.NewGuid();
    private static readonly Guid TeacherRec = Guid.NewGuid();

    private static ICurrentUserService User(Guid userId, string identity, bool viewAll, bool academicsManage = false)
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(x => x.SchoolId).Returns(School);
        m.Setup(x => x.UserId).Returns(userId);
        m.Setup(x => x.Identity).Returns(identity);
        m.Setup(x => x.HasPermission(PermissionKeys.MarksViewAll)).Returns(viewAll);
        m.Setup(x => x.HasPermission(PermissionKeys.AcademicsManage)).Returns(academicsManage);
        return m.Object;
    }

    private static async Task SeedAsync(SchoolPortalDbContext db)
    {
        await PositionsSeedData.SeedAsync(db, NullLogger.Instance);

        db.Schools.Add(new School { SchoolId = School, Name = "List Scope High", IsActive = true, CreatedAt = DateTime.UtcNow });

        User U(Guid id, string email, string role) => new()
        { UserId = id, SchoolId = School, Email = email, PasswordHash = "x", FirstName = "F", LastName = email, Role = role, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(U(TeacherUser, "t", "Teacher"), U(AdminUser, "a", "Admin"),
            U(LearnerUser, "l", "Student"), U(ParentUser, "pa", "Parent"), U(AuditorUser, "au", "Admin"));

        db.Teachers.Add(new Teacher { TeacherId = TeacherRec, UserId = TeacherUser, SchoolId = School, CreatedAt = DateTime.UtcNow });
        db.Subjects.Add(new Subject { SubjectId = Maths, SchoolId = School, Name = "Maths", Code = "MATH", CreatedAt = DateTime.UtcNow });
        db.Classes.AddRange(
            new Class { ClassId = ClassA, SchoolId = School, Name = "10A", GradeLevel = 10, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassB, SchoolId = School, Name = "10B", GradeLevel = 10, CreatedAt = DateTime.UtcNow });
        db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = ClassA, SubjectId = Maths, TeacherId = TeacherRec, SchoolId = School, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();
    }

    private static ClassesController Controller(SchoolPortalDbContext db, ICurrentUserService u)
    {
        var scope = new ScopeService(db, u);
        var svc = new ClassService(db, u, scope);
        return new ClassesController(svc, u, scope);
    }

    private static PaginatedResult<ClassDto> Paged(IActionResult r) =>
        Assert.IsType<PaginatedResult<ClassDto>>(Assert.IsType<OkObjectResult>(r).Value);

    [Fact]
    public async Task GetClasses_EnforcesSelfProtectingScopeContract()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedAsync(db);

            // CASE 1 — SubjectTeacher, no academics.manage, mine OMITTED → scoped list (ClassA only,
            // never the whole school). Proves the endpoint is self-protecting without `mine`.
            var teacher = await Controller(db, User(TeacherUser, IdentityKeys.Staff, viewAll: false))
                .GetClasses(null, null, mine: false, page: 1, pageSize: 50);
            var teacherPaged = Paged(teacher);
            Assert.Equal(1, teacherPaged.Total);
            Assert.Equal(ClassA, Assert.Single(teacherPaged.Items).ClassId);

            // CASE 2 — Admin/HOD (academics.manage), mine OMITTED → full school list (both classes).
            var admin = await Controller(db, User(AdminUser, IdentityKeys.Staff, viewAll: false, academicsManage: true))
                .GetClasses(null, null, mine: false, page: 1, pageSize: 50);
            Assert.Equal(2, Paged(admin).Total);

            // CASE 3 — Learner and Parent → 403 (neither the full nor the scoped list).
            var learner = await Controller(db, User(LearnerUser, IdentityKeys.Learner, viewAll: false))
                .GetClasses(null, null, mine: false, page: 1, pageSize: 50);
            Assert.IsType<ForbidResult>(learner);

            var parent = await Controller(db, User(ParentUser, IdentityKeys.Parent, viewAll: false))
                .GetClasses(null, null, mine: false, page: 1, pageSize: 50);
            Assert.IsType<ForbidResult>(parent);

            // CASE 4 — Auditor (External identity + marks.view_all), no academics.manage → still sees
            // ALL classes. Routed through the scoped branch, but IScopeService returns null for
            // school-wide oversight, so oversight is NOT narrowed by the tightening.
            var auditor = await Controller(db, User(AuditorUser, IdentityKeys.External, viewAll: true))
                .GetClasses(null, null, mine: false, page: 1, pageSize: 50);
            Assert.Equal(2, Paged(auditor).Total);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task GetStudents_EnforcesClassScope_RosterIdorClosed()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedAsync(db); // TeacherUser teaches ClassA (via ClassSubject); ClassB is not theirs.

            var teacher = Controller(db, User(TeacherUser, IdentityKeys.Staff, viewAll: false));
            Assert.IsType<OkObjectResult>(await teacher.GetStudents(ClassA));   // own class → roster
            Assert.IsType<NotFoundResult>(await teacher.GetStudents(ClassB));   // H2: not their class → 404

            // Oversight (External + marks.view_all) reads any roster.
            var auditor = Controller(db, User(AuditorUser, IdentityKeys.External, viewAll: true));
            Assert.IsType<OkObjectResult>(await auditor.GetStudents(ClassB));
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
