using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Attendance;
using Xunit;

namespace SchoolPortal.Tests.Services;

public class AttendanceServiceTests
{
    private static readonly Guid TestSchoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid TestClassId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid TestStudentId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private readonly SchoolPortalDbContext _context;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<ILogger<AttendanceService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotifications;
    private readonly Mock<IScopeService> _mockScope;
    private readonly AttendanceService _service;

    public AttendanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolPortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SchoolPortalDbContext(options);
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

        _service = new AttendanceService(
            _context,
            _mockCurrentUser.Object,
            _mockLogger.Object,
            _mockNotifications.Object,
            _mockScope.Object);

        SeedTestData();
    }

    private void SeedTestData()
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
        _context.SaveChanges();
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
