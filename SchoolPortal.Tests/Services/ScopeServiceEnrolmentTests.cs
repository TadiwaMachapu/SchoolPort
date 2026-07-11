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
/// Sprint 1.5.2.5 — the REAL ScopeService enrolment helpers that back bulk-capture's batched
/// studentId validation (the service tests stub these; this pins the implementation itself).
/// </summary>
[Collection("Postgres")]
public class ScopeServiceEnrolmentTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private SchoolPortalDbContext _context = null!;
    private NpgsqlDataSource _source = null!;
    private ScopeService _scope = null!;

    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly Guid _classSubjectId = Guid.NewGuid();
    private Guid _activeStudent;
    private Guid _droppedStudent;
    private Guid _otherClassStudent;

    public ScopeServiceEnrolmentTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        (_context, _source) = await _pg.CreateIsolatedDatabaseAsync();
        // The enrolment helpers take schoolId explicitly and never read the current user.
        _scope = new ScopeService(_context, new Mock<ICurrentUserService>().Object);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _source.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        _context.Schools.Add(new School { SchoolId = _schoolId, Name = "Scope Test School", IsActive = true, CreatedAt = DateTime.UtcNow });
        var classId = Guid.NewGuid();
        _context.Classes.Add(new Class { ClassId = classId, SchoolId = _schoolId, Name = "11B", CreatedAt = DateTime.UtcNow });
        var subjectId = Guid.NewGuid();
        _context.Subjects.Add(new Subject { SubjectId = subjectId, SchoolId = _schoolId, Name = "History", Code = "HIS", CreatedAt = DateTime.UtcNow });
        _context.ClassSubjects.Add(new ClassSubject { ClassSubjectId = _classSubjectId, ClassId = classId, SubjectId = subjectId, SchoolId = _schoolId, CreatedAt = DateTime.UtcNow });

        var otherClassId = Guid.NewGuid();
        _context.Classes.Add(new Class { ClassId = otherClassId, SchoolId = _schoolId, Name = "11C", CreatedAt = DateTime.UtcNow });

        _activeStudent = AddStudent("active");
        _droppedStudent = AddStudent("dropped");
        _otherClassStudent = AddStudent("otherclass");
        _context.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = classId, StudentId = _activeStudent, SchoolId = _schoolId, EnrolledAt = DateTime.UtcNow, IsActive = true });
        _context.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = classId, StudentId = _droppedStudent, SchoolId = _schoolId, EnrolledAt = DateTime.UtcNow, DroppedAt = DateTime.UtcNow, IsActive = false });
        _context.Enrollments.Add(new Enrollment { EnrollmentId = Guid.NewGuid(), ClassId = otherClassId, StudentId = _otherClassStudent, SchoolId = _schoolId, EnrolledAt = DateTime.UtcNow, IsActive = true });
        await _context.SaveChangesAsync();
    }

    private Guid AddStudent(string tag)
    {
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { UserId = userId, SchoolId = _schoolId, Email = $"{tag}_{userId:N}@scope.test", PasswordHash = "x", FirstName = "S", LastName = tag, Role = "Student", Identity = "Learner", IsActive = true, CreatedAt = DateTime.UtcNow });
        var studentId = Guid.NewGuid();
        _context.Students.Add(new Student { StudentId = studentId, SchoolId = _schoolId, UserId = userId, StudentNumber = "N" + userId.ToString("N")[..6], CreatedAt = DateTime.UtcNow });
        return studentId;
    }

    [Fact]
    public async Task GetEnrolledStudentIds_OnlyReturnsActiveEnrolments()
    {
        var ids = await _scope.GetEnrolledStudentIdsAsync(_classSubjectId, _schoolId);

        Assert.Contains(_activeStudent, ids);
        Assert.DoesNotContain(_droppedStudent, ids);   // dropped (IsActive=false) excluded
        Assert.DoesNotContain(_otherClassStudent, ids); // other class excluded
        Assert.Single(ids);
    }

    [Fact]
    public async Task GetEnrolledStudentIds_WrongSchool_ReturnsEmpty()
    {
        // Correct classSubjectId but a different schoolId pin → empty set, never a leak.
        Assert.Empty(await _scope.GetEnrolledStudentIdsAsync(_classSubjectId, Guid.NewGuid()));
    }

    [Fact]
    public async Task IsStudentInClass_MatchesEnrolmentState()
    {
        Assert.True(await _scope.IsStudentInClassAsync(_activeStudent, _classSubjectId, _schoolId));
        Assert.False(await _scope.IsStudentInClassAsync(_droppedStudent, _classSubjectId, _schoolId));
        Assert.False(await _scope.IsStudentInClassAsync(_otherClassStudent, _classSubjectId, _schoolId));
        Assert.False(await _scope.IsStudentInClassAsync(_activeStudent, _classSubjectId, Guid.NewGuid())); // wrong school
    }
}
