using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Attendance;
using Xunit;

namespace SchoolPortal.Tests.Services;

public class AttendanceServiceTests
{
    private readonly SchoolPortalDbContext _context;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<AttendanceService>> _mockLogger;
    private readonly AttendanceService _service;

    public AttendanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolPortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SchoolPortalDbContext(options);
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AttendanceService>>();

        // Setup mocks
        _mockCurrentUser.Setup(x => x.SchoolId).Returns(1);
        _mockCurrentUser.Setup(x => x.UserId).Returns(1);
        _mockConfiguration.Setup(x => x.GetConnectionString("DefaultConnection"))
            .Returns("Server=localhost;Database=Test;");

        _service = new AttendanceService(
            _context, 
            _mockCurrentUser.Object, 
            _mockConfiguration.Object,
            _mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var school = new School
        {
            SchoolId = 1,
            Name = "Test School",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var classEntity = new Class
        {
            ClassId = 1,
            SchoolId = 1,
            Name = "Grade 10A",
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            UserId = 1,
            SchoolId = 1,
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
            StudentId = 1,
            UserId = 1,
            SchoolId = 1,
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
            ClassId = 1,
            StudentId = 1,
            SchoolId = 1,
            Date = date,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAttendanceAsync(1, date);

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
        var result = await _service.GetAttendanceAsync(1, date);

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
                    ClassId = 1,
                    StudentId = 1,
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
