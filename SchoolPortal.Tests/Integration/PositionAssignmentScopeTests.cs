using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Positions;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Sprint 1.5.0 Step 9 — proves the connection between the admin assignment UI and Step 7's
/// security enforcement. An admin assigns "HOD of Mathematics" THROUGH the production PositionService
/// (which writes UserPositionScope); ScopeService must then give that HOD exactly Mathematics data
/// and nothing else (the English class is out of scope).
/// </summary>
[Collection("Postgres")]
public class PositionAssignmentScopeTests
{
    private readonly PostgresFixture _pg;
    public PositionAssignmentScopeTests(PostgresFixture pg) => _pg = pg;

    private static readonly Guid School = Guid.NewGuid();
    private static readonly Guid AdminUser = Guid.NewGuid();   // does the assigning
    private static readonly Guid HodUser = Guid.NewGuid();     // receives HOD of Mathematics
    private static readonly Guid Maths = Guid.NewGuid();
    private static readonly Guid English = Guid.NewGuid();
    private static readonly Guid MathsClass = Guid.NewGuid();   // 10A — offers Mathematics
    private static readonly Guid EnglishClass = Guid.NewGuid(); // 10B — offers English only

    private static ICurrentUserService CurrentUser(Guid userId, string identity = IdentityKeys.Staff, bool viewAll = false)
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(x => x.SchoolId).Returns(School);
        m.Setup(x => x.UserId).Returns(userId);
        m.Setup(x => x.Identity).Returns(identity);
        m.Setup(x => x.HasPermission(PermissionKeys.MarksViewAll)).Returns(viewAll);
        return m.Object;
    }

    private static async Task SeedAsync(SchoolPortalDbContext db)
    {
        await PositionsSeedData.SeedAsync(db, NullLogger.Instance);
        db.Schools.Add(new School { SchoolId = School, Name = "Scope High", IsActive = true, CreatedAt = DateTime.UtcNow });

        User U(Guid id, string email) => new()
        { UserId = id, SchoolId = School, Email = email, PasswordHash = "x", FirstName = "F", LastName = email, Role = "Teacher", Identity = IdentityKeys.Staff, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(U(AdminUser, "admin"), U(HodUser, "hod"));

        db.Subjects.AddRange(
            new Subject { SubjectId = Maths, SchoolId = School, Name = "Mathematics", Code = "MATH", CreatedAt = DateTime.UtcNow },
            new Subject { SubjectId = English, SchoolId = School, Name = "English", Code = "ENG", CreatedAt = DateTime.UtcNow });
        db.Classes.AddRange(
            new Class { ClassId = MathsClass, SchoolId = School, Name = "10A", GradeLevel = 10, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = EnglishClass, SchoolId = School, Name = "10B", GradeLevel = 10, CreatedAt = DateTime.UtcNow });
        // 10A offers Mathematics; 10B offers English only.
        db.ClassSubjects.AddRange(
            new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = MathsClass, SubjectId = Maths, SchoolId = School, CreatedAt = DateTime.UtcNow },
            new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = EnglishClass, SubjectId = English, SchoolId = School, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AssignHodOfMathematics_GivesMathsScope_NotEnglish()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedAsync(db);

            // ── Admin assigns "HOD of Mathematics" via the production service ──
            var positionService = new PositionService(db, CurrentUser(AdminUser));
            var result = await positionService.AssignAsync(new AssignPositionRequest
            {
                UserId = HodUser,
                PositionKey = PositionKeys.HOD,
                Scopes = new List<ScopeInput> { new() { ScopeRefId = Maths } },
            });

            // The assignment wrote a Subject scope pointing at Mathematics.
            Assert.Equal((int)ScopeType.Subject, Assert.Single(result.Scopes).ScopeType);
            Assert.Equal(Maths, result.Scopes[0].ScopeRefId);
            Assert.Equal("Mathematics", result.Scopes[0].Label);

            var scopeRow = await db.UserPositionScopes.AsNoTracking()
                .SingleAsync(s => s.UserPosition.UserId == HodUser);
            Assert.Equal(ScopeType.Subject, scopeRow.ScopeType);
            Assert.Equal(Maths, scopeRow.ScopeRefId);

            // ── Step 7 enforcement: the HOD sees Mathematics, not English ──
            var hodScope = new ScopeService(db, CurrentUser(HodUser));
            var accessible = await hodScope.GetAccessibleClassIdsAsync();

            Assert.NotNull(accessible);
            Assert.Contains(MathsClass, accessible!);          // sees Mathematics marks (10A)
            Assert.DoesNotContain(EnglishClass, accessible!);  // CANNOT see English marks (10B)
            Assert.True(await hodScope.CanAccessClassAsync(MathsClass));
            Assert.False(await hodScope.CanAccessClassAsync(EnglishClass));   // scope boundary holds (IDOR)
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
