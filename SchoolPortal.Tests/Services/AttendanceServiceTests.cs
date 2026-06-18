using Moq;
using Microsoft.Extensions.Logging;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Attendance;
using SchoolPortal.Tests.Integration;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// AttendanceService unit tests on REAL Postgres. Previously used the EF in-memory provider, which
/// cannot map the jsonb POCO columns (School.Features/Theme/Settings) and so failed at SeedTestData —
/// a pre-existing baseline-red (see [[test-suite-baseline-red]]). Now each test gets a fresh isolated
/// database from the shared <see cref="PostgresFixture"/>. Scope is mocked permissive (all-access),
/// matching the original service-layer unit-test intent.
/// </summary>
[Collection("Postgres")]
public class AttendanceServiceTests : IAsyncLifetime
{
    private static readonly Guid TestSchoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid TestClassId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid TestStudentId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private readonly PostgresFixture _pg;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<ILogger<AttendanceService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotifications;
    private readonly Mock<IScopeService> _mockScope;

    private SchoolPortalDbContext _context = null!;
    private NpgsqlDataSource _source = null!;
    private AttendanceService _service = null!;

    public AttendanceServiceTests(PostgresFixture pg)
    {
        _pg = pg;
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockLogger = new Mock<ILogger<AttendanceService>>();
        _mockNotifications = new Mock<INotificationService>();
        _mockScope = new Mock<IScopeService>();

        _mockCurrentUser.Setup(x => x.SchoolId).Returns(TestSchoolId);
        _mockCurrentUser.Setup(x => x.UserId).Returns(TestUserId);
        // Permissive scope for these legacy unit tests (unrestricted / all-access).
        _mockScope.Setup(x => x.CanAccessClassAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        _mockScope.Setup(x => x.EnsureClassAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _mockScope.Setup(x => x.GetAccessibleClassIdsAsync()).ReturnsAsync((IReadOnlySet<Guid>?)null);
    }

    public async Task InitializeAsync()
    {
        (_context, _source) = await _pg.CreateIsolatedDatabaseAsync();
        _service = new AttendanceService(
            _context,
            _mockCurrentUser.Object,
            _mockLogger.Object,
            _mockNotifications.Object,
            _mockScope.Object);
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _source.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var school = new School
        {
            SchoolId = TestSchoolId,
            Name = "Test School",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var classEntity = new Class
        {
            ClassId = TestClassId,
            SchoolId = TestSchoolId,
            Name = "Grade 10A",
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            UserId = TestUserId,
            SchoolId = TestSchoolId,
            Email = "student@test.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "Student",
            Role = "Student",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var student = new Student
        {
            StudentId = TestStudentId,
            UserId = TestUserId,
            SchoolId = TestSchoolId,
            StudentNumber = "S001",
            CreatedAt = DateTime.UtcNow
        };

        _context.Schools.Add(school);
        _context.Classes.Add(classEntity);
        _context.Users.Add(user);
        _context.Students.Add(student);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAttendance_ValidRequest_ShouldReturnAttendanceRecords()
    {
        // Arrange
        var date = DateTime.UtcNow.Date;
        var attendance = new Attendance
        {
            ClassId = TestClassId,
            StudentId = TestStudentId,
            SchoolId = TestSchoolId,
            Date = date,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAttendanceAsync(TestClassId, date);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].Status);
        Assert.Equal("Test Student", result[0].StudentName);
    }

    [Fact]
    public async Task GetAttendance_NoRecords_ShouldReturnEmptyList()
    {
        // Arrange
        var date = DateTime.UtcNow.Date.AddDays(-30);

        // Act
        var result = await _service.GetAttendanceAsync(TestClassId, date);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(10)]
    public async Task BulkUpsertAttendance_InvalidStatus_ShouldThrowException(int invalidStatus)
    {
        // Arrange
        var request = new BulkAttendanceRequest
        {
            Attendances = new List<AttendanceItem>
            {
                new AttendanceItem
                {
                    ClassId = TestClassId,
                    StudentId = TestStudentId,
                    Date = DateTime.UtcNow.Date,
                    Status = invalidStatus
                }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.BulkUpsertAttendanceAsync(request));
    }
}
