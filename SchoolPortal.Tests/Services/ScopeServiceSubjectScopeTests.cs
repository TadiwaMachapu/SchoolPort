using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Integration;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.3 — subject-level scope for the HOD subject view. Pins the security property that
/// class-level scope alone is insufficient: a teacher of one subject must not reach a DIFFERENT
/// subject taught in a class they can otherwise access.
/// </summary>
[Collection("Postgres")]
public class ScopeServiceSubjectScopeTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private SchoolPortalDbContext _context = null!;
    private NpgsqlDataSource _source = null!;

    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly Guid _teacherUserId = Guid.NewGuid();
    private Guid _maths;
    private Guid _history;

    public ScopeServiceSubjectScopeTests(PostgresFixture pg) => _pg = pg;

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
        _context.Schools.Add(new School { SchoolId = _schoolId, Name = "Scope Subj School", IsActive = true, CreatedAt = DateTime.UtcNow });
        _context.Users.Add(new User { UserId = _teacherUserId, SchoolId = _schoolId, Email = "t@subj.test", PasswordHash = "x", FirstName = "T", LastName = "One", Role = "Teacher", Identity = "Staff", IsActive = true, CreatedAt = DateTime.UtcNow });
        var teacherId = Guid.NewGuid();
        _context.Teachers.Add(new Teacher { TeacherId = teacherId, UserId = _teacherUserId, SchoolId = _schoolId, CreatedAt = DateTime.UtcNow });

        var classId = Guid.NewGuid();
        _context.Classes.Add(new Class { ClassId = classId, SchoolId = _schoolId, Name = "10A", GradeLevel = 10, CreatedAt = DateTime.UtcNow });

        _maths = Guid.NewGuid();
        _history = Guid.NewGuid();
        _context.Subjects.Add(new Subject { SubjectId = _maths, SchoolId = _schoolId, Name = "Mathematics", Code = "MAT", CreatedAt = DateTime.UtcNow });
        _context.Subjects.Add(new Subject { SubjectId = _history, SchoolId = _schoolId, Name = "History", Code = "HIS", CreatedAt = DateTime.UtcNow });

        // Same class teaches BOTH — but the teacher only teaches Maths. History is taught by
        // someone else (left unassigned here; the point is our teacher does NOT teach it).
        _context.ClassSubjects.Add(new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = classId, SubjectId = _maths, TeacherId = teacherId, SchoolId = _schoolId, CreatedAt = DateTime.UtcNow });
        _context.ClassSubjects.Add(new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = classId, SubjectId = _history, TeacherId = null, SchoolId = _schoolId, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
    }

    private ScopeService ScopeFor()
    {
        var cu = new Mock<ICurrentUserService>();
        cu.Setup(x => x.SchoolId).Returns(_schoolId);
        cu.Setup(x => x.UserId).Returns(_teacherUserId);
        cu.Setup(x => x.Identity).Returns("Staff");
        cu.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(false); // not school-wide oversight
        return new ScopeService(_context, cu.Object);
    }

    [Fact]
    public async Task CanAccessSubject_TeachesMathsOnly_SeesMathsNotHistory()
    {
        var scope = ScopeFor();

        // The Maths class is class-level accessible (the teacher teaches Maths there)…
        Assert.True(await scope.CanAccessSubjectAsync(_maths));
        // …but History, taught in that SAME class by someone else, is NOT in subject scope.
        Assert.False(await scope.CanAccessSubjectAsync(_history));
    }
}
