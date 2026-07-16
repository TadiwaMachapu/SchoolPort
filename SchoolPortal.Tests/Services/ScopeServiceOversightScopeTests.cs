using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Integration;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.3 — oversight scope for the role views (Grade Head / HOD). Pins that
/// <see cref="IScopeService.GetOversightClassIdsAsync"/> returns the WHOLE grade/subject the
/// caller oversees via user_position_scopes — not merely the classes they teach — is one-to-many
/// aware, and is school-bounded. These prove the existing scope resolution (shared with
/// GetAccessibleClassIdsAsync) does exactly what the role views need; a failure here is a real gap.
/// </summary>
[Collection("Postgres")]
public class ScopeServiceOversightScopeTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private SchoolPortalDbContext _context = null!;
    private NpgsqlDataSource _source = null!;

    private readonly Guid _schoolA = Guid.NewGuid();
    private readonly Guid _schoolB = Guid.NewGuid();

    // School A users. Each also "teaches" a class to prove oversight != taught.
    private readonly Guid _gradeHeadUser = Guid.NewGuid();   // GradeHead of Gr 12; teaches a Gr 10 class
    private readonly Guid _hodMathsUser = Guid.NewGuid();    // HOD of Maths; teaches one Maths class + a History class
    private readonly Guid _hodMultiUser = Guid.NewGuid();    // HOD scoped to BOTH Maths and Physical Sciences

    // School A classes.
    private readonly Guid _c12A = Guid.NewGuid();   // Gr 12, teaches Maths (taught by hodMaths) + History
    private readonly Guid _c12B = Guid.NewGuid();   // Gr 12, teaches Maths (no teacher)
    private readonly Guid _c12C = Guid.NewGuid();   // Gr 12, teaches Maths (no teacher)
    private readonly Guid _c11B = Guid.NewGuid();   // Gr 11, teaches Physical Sciences
    private readonly Guid _c10A = Guid.NewGuid();   // Gr 10, gradeHead's register class + History (operational only)

    // School B — must never leak into School A oversight.
    private readonly Guid _cBMaths = Guid.NewGuid(); // Gr 12 at School B, teaches School B Maths

    public ScopeServiceOversightScopeTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        (_context, _source) = await _pg.CreateIsolatedDatabaseAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _source.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        await PositionsSeedData.SeedAsync(_context, NullLogger.Instance);

        _context.Schools.AddRange(
            new School { SchoolId = _schoolA, Name = "Oversight A", IsActive = true, CreatedAt = DateTime.UtcNow },
            new School { SchoolId = _schoolB, Name = "Oversight B", IsActive = true, CreatedAt = DateTime.UtcNow });

        User U(Guid id, Guid school, string email) => new()
        { UserId = id, SchoolId = school, Email = email, PasswordHash = "x", FirstName = "F", LastName = email, Role = "Teacher", Identity = IdentityKeys.Staff, IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Users.AddRange(
            U(_gradeHeadUser, _schoolA, "gh@a"), U(_hodMathsUser, _schoolA, "hodm@a"), U(_hodMultiUser, _schoolA, "hodx@a"));

        // Teacher records so the "also teaches" links are real (they must be EXCLUDED by oversight).
        var ghTeacher = Guid.NewGuid();
        var hodmTeacher = Guid.NewGuid();
        _context.Teachers.AddRange(
            new Teacher { TeacherId = ghTeacher, UserId = _gradeHeadUser, SchoolId = _schoolA, CreatedAt = DateTime.UtcNow },
            new Teacher { TeacherId = hodmTeacher, UserId = _hodMathsUser, SchoolId = _schoolA, CreatedAt = DateTime.UtcNow });

        // Subjects.
        var maths = Guid.NewGuid();
        var physci = Guid.NewGuid();
        var history = Guid.NewGuid();
        var mathsB = Guid.NewGuid();
        _context.Subjects.AddRange(
            new Subject { SubjectId = maths, SchoolId = _schoolA, Name = "Mathematics", Code = "MAT", CreatedAt = DateTime.UtcNow },
            new Subject { SubjectId = physci, SchoolId = _schoolA, Name = "Physical Sciences", Code = "PHY", CreatedAt = DateTime.UtcNow },
            new Subject { SubjectId = history, SchoolId = _schoolA, Name = "History", Code = "HIS", CreatedAt = DateTime.UtcNow },
            new Subject { SubjectId = mathsB, SchoolId = _schoolB, Name = "Mathematics", Code = "MAT", CreatedAt = DateTime.UtcNow });

        // School A classes. _c10A is the grade head's register class (Class.TeacherId) — operational.
        _context.Classes.AddRange(
            new Class { ClassId = _c12A, SchoolId = _schoolA, Name = "12A", GradeLevel = 12, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = _c12B, SchoolId = _schoolA, Name = "12B", GradeLevel = 12, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = _c12C, SchoolId = _schoolA, Name = "12C", GradeLevel = 12, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = _c11B, SchoolId = _schoolA, Name = "11B", GradeLevel = 11, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = _c10A, SchoolId = _schoolA, Name = "10A", GradeLevel = 10, TeacherId = ghTeacher, CreatedAt = DateTime.UtcNow });
        _context.Classes.Add(
            new Class { ClassId = _cBMaths, SchoolId = _schoolB, Name = "12A", GradeLevel = 12, CreatedAt = DateTime.UtcNow });

        ClassSubject CS(Guid cls, Guid subj, Guid school, Guid? teacher = null) => new()
        { ClassSubjectId = Guid.NewGuid(), ClassId = cls, SubjectId = subj, TeacherId = teacher, SchoolId = school, CreatedAt = DateTime.UtcNow };
        _context.ClassSubjects.AddRange(
            // Maths across three Gr 12 classes — hodMaths teaches only 12A's Maths.
            CS(_c12A, maths, _schoolA, hodmTeacher),
            CS(_c12B, maths, _schoolA),
            CS(_c12C, maths, _schoolA),
            // Physical Sciences in a Gr 11 class (for the multi-subject HOD).
            CS(_c11B, physci, _schoolA),
            // History in 12A and in 10A — hodMaths teaches 10A History (operational, NOT Maths scope).
            CS(_c12A, history, _schoolA),
            CS(_c10A, history, _schoolA, hodmTeacher),
            // School B Maths in its own Gr 12 class — must never surface in School A oversight.
            CS(_cBMaths, mathsB, _schoolB));

        // Positions (all EffectiveFrom in the past, active).
        var hodPos = await _context.Positions.Where(p => p.Key == "HOD").Select(p => p.PositionId).FirstAsync();
        var gradeHeadPos = await _context.Positions.Where(p => p.Key == "GradeHead").Select(p => p.PositionId).FirstAsync();

        UserPosition Appoint(Guid user, Guid position) => new()
        { UserPositionId = Guid.NewGuid(), SchoolId = _schoolA, UserId = user, PositionId = position, EffectiveFrom = DateTime.UtcNow.AddDays(-1), IsActive = true, CreatedAt = DateTime.UtcNow };

        var ghAppt = Appoint(_gradeHeadUser, gradeHeadPos);
        var hodmAppt = Appoint(_hodMathsUser, hodPos);
        var hodxAppt = Appoint(_hodMultiUser, hodPos);
        _context.UserPositions.AddRange(ghAppt, hodmAppt, hodxAppt);

        _context.UserPositionScopes.AddRange(
            // GradeHead → Gr 12 (value scope).
            new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), UserPositionId = ghAppt.UserPositionId, ScopeType = ScopeType.Grade, ScopeValue = "12" },
            // HOD Maths → Maths (entity scope).
            new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), UserPositionId = hodmAppt.UserPositionId, ScopeType = ScopeType.Subject, ScopeRefId = maths },
            // HOD Multi → Maths AND Physical Sciences (one appointment, two scope rows).
            new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), UserPositionId = hodxAppt.UserPositionId, ScopeType = ScopeType.Subject, ScopeRefId = maths },
            new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), UserPositionId = hodxAppt.UserPositionId, ScopeType = ScopeType.Subject, ScopeRefId = physci });

        await _context.SaveChangesAsync();
    }

    private ScopeService ScopeFor(Guid userId)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.Setup(x => x.SchoolId).Returns(_schoolA);
        cu.Setup(x => x.UserId).Returns(userId);
        cu.Setup(x => x.Identity).Returns(IdentityKeys.Staff);
        cu.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(false); // not school-wide oversight
        return new ScopeService(_context, cu.Object);
    }

    [Fact]
    public async Task GradeHead_OversightScope_ReturnsWholeGrade_NotJustTaughtClasses()
    {
        var ids = await ScopeFor(_gradeHeadUser).GetOversightClassIdsAsync();

        Assert.NotNull(ids);
        // The whole grade — including 12B/12C, which the grade head does not teach.
        Assert.Contains(_c12A, ids!);
        Assert.Contains(_c12B, ids!);
        Assert.Contains(_c12C, ids!);
        // NOT their taught register class (Gr 10) — oversight is the grade, not what they teach.
        Assert.DoesNotContain(_c10A, ids!);
        // Nothing from other grades.
        Assert.DoesNotContain(_c11B, ids!);
    }

    [Fact]
    public async Task HOD_OversightScope_ReturnsWholeSubject_NotJustTaughtClasses()
    {
        var ids = await ScopeFor(_hodMathsUser).GetOversightClassIdsAsync();

        Assert.NotNull(ids);
        // Every class teaching Maths — including 12B/12C the HOD does not teach.
        Assert.Contains(_c12A, ids!);
        Assert.Contains(_c12B, ids!);
        Assert.Contains(_c12C, ids!);
        // 10A is taught by this HOD but only teaches History — out of subject scope, excluded.
        Assert.DoesNotContain(_c10A, ids!);
        // Physical Sciences class is another subject — excluded.
        Assert.DoesNotContain(_c11B, ids!);
    }

    [Fact]
    public async Task HOD_OversightScope_MultipleSubjects_ReturnsAllSubjectsClasses()
    {
        var ids = await ScopeFor(_hodMultiUser).GetOversightClassIdsAsync();

        Assert.NotNull(ids);
        // One appointment, two subject scopes → classes of BOTH subjects (one-to-many guard).
        Assert.Contains(_c12A, ids!);  // Maths
        Assert.Contains(_c12B, ids!);  // Maths
        Assert.Contains(_c12C, ids!);  // Maths
        Assert.Contains(_c11B, ids!);  // Physical Sciences
        // History-only class not in either scope.
        Assert.DoesNotContain(_c10A, ids!);
    }

    [Fact]
    public async Task OversightScope_IsSchoolBounded_NoOtherSchoolClasses()
    {
        // School A Maths HOD must never reach School B's classes, even though School B has a
        // like-named Maths subject and a Gr 12 class.
        var ids = await ScopeFor(_hodMathsUser).GetOversightClassIdsAsync();

        Assert.NotNull(ids);
        Assert.DoesNotContain(_cBMaths, ids!);
        Assert.All(ids!, id => Assert.Contains(id, new[] { _c12A, _c12B, _c12C }));
    }
}
