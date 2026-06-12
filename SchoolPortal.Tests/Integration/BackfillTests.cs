using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Validates the Sprint 1.5.0 identity backfill mapping on real Postgres: dry-run plan
/// correctness, scope inference for teachers, the apply writes, and idempotency. Each test
/// runs in its own isolated database.
/// </summary>
[Collection("Postgres")]
public class BackfillTests
{
    private readonly PostgresFixture _pg;
    public BackfillTests(PostgresFixture pg) => _pg = pg;

    private static readonly Guid School = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static async Task SeedSampleAsync(SchoolPortal.Data.SchoolPortalDbContext db)
    {
        await PositionsSeedData.SeedAsync(db, NullLogger.Instance);

        db.Schools.Add(new School { SchoolId = School, Name = "Test High", IsActive = true, CreatedAt = DateTime.UtcNow });

        var admin   = new User { UserId = Guid.NewGuid(), SchoolId = School, Email = "principal@t.com", PasswordHash = "x", FirstName = "Ann", LastName = "Admin", Role = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow };
        var teacher = new User { UserId = Guid.NewGuid(), SchoolId = School, Email = "teacher@t.com", PasswordHash = "x", FirstName = "Tom", LastName = "Teach", Role = "Teacher", IsActive = true, CreatedAt = DateTime.UtcNow };
        var student = new User { UserId = Guid.NewGuid(), SchoolId = School, Email = "learner@t.com", PasswordHash = "x", FirstName = "Lee", LastName = "Learn", Role = "Student", IsActive = true, CreatedAt = DateTime.UtcNow };
        var parent  = new User { UserId = Guid.NewGuid(), SchoolId = School, Email = "parent@t.com", PasswordHash = "x", FirstName = "Pat", LastName = "Parent", Role = "Parent", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(admin, teacher, student, parent);

        var teacherRec = new Teacher { TeacherId = Guid.NewGuid(), UserId = teacher.UserId, SchoolId = School, CreatedAt = DateTime.UtcNow };
        db.Teachers.Add(teacherRec);

        var subject = new Subject { SubjectId = Guid.NewGuid(), SchoolId = School, Name = "Maths", Code = "MATH", CreatedAt = DateTime.UtcNow };
        var class10b = new Class { ClassId = Guid.NewGuid(), SchoolId = School, Name = "10B", CreatedAt = DateTime.UtcNow };
        db.Subjects.Add(subject);
        db.Classes.Add(class10b);
        db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = class10b.ClassId, SubjectId = subject.SubjectId, TeacherId = teacherRec.TeacherId, SchoolId = School, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DryRun_MapsRolesToIdentitiesAndPositions_WithTeacherScope()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedSampleAsync(db);
            var svc = new IdentityBackfillService(db, NullLogger<IdentityBackfillService>.Instance);

            var plan = await svc.BuildPlanAsync();

            Assert.Equal("Staff",   plan.Users.Single(u => u.Email == "principal@t.com").NewIdentity);
            Assert.Equal("Staff",   plan.Users.Single(u => u.Email == "teacher@t.com").NewIdentity);
            Assert.Equal("Learner", plan.Users.Single(u => u.Email == "learner@t.com").NewIdentity);
            Assert.Equal("Parent",  plan.Users.Single(u => u.Email == "parent@t.com").NewIdentity);

            Assert.Equal("Principal", plan.Users.Single(u => u.Email == "principal@t.com").Positions.Single().PositionKey);

            var teacherPlan = plan.Users.Single(u => u.Email == "teacher@t.com").Positions.Single();
            Assert.Equal("SubjectTeacher", teacherPlan.PositionKey);
            var scope = Assert.Single(teacherPlan.Scopes);
            Assert.Equal(ScopeType.Class, scope.ScopeType);
            Assert.Equal("10B", scope.Label);

            // Learner/Parent get no position (identity-implicit permissions only)
            Assert.Empty(plan.Users.Single(u => u.Email == "learner@t.com").Positions);

            // DRY RUN writes nothing
            Assert.All(await db.Users.ToListAsync(), u => Assert.Null(u.Identity));
            Assert.Equal(0, await db.UserPositions.CountAsync());
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Apply_WritesIdentitiesAndPositions_AndIsIdempotent()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedSampleAsync(db);
            var svc = new IdentityBackfillService(db, NullLogger<IdentityBackfillService>.Instance);

            var first = await svc.ApplyAsync();
            Assert.True(first > 0);

            Assert.Equal("Staff",   (await db.Users.SingleAsync(u => u.Email == "teacher@t.com")).Identity);
            Assert.Equal("Learner", (await db.Users.SingleAsync(u => u.Email == "learner@t.com")).Identity);

            var teacherUser = await db.Users.SingleAsync(u => u.Email == "teacher@t.com");
            var teacherPos = await db.UserPositions.Include(p => p.Scopes)
                .SingleAsync(p => p.UserId == teacherUser.UserId);
            Assert.Equal(ScopeType.Class, Assert.Single(teacherPos.Scopes).ScopeType);

            var positionsAfterFirst = await db.UserPositions.CountAsync();

            // Idempotent: second apply writes nothing new.
            var second = await svc.ApplyAsync();
            Assert.Equal(0, second);
            Assert.Equal(positionsAfterFirst, await db.UserPositions.CountAsync());
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
