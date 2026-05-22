using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Assignments;
using Xunit;

namespace SchoolPortal.Tests.Services;

public class AssignmentServiceTests
{
    private static readonly Guid TestSchoolId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid TestClassId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid TestSubjectId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid TestClassSubjectId = Guid.Parse("00000000-0000-0000-0000-000000000005");

    private readonly SchoolPortalDbContext _context;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly Mock<ILogger<AssignmentService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotifications;
    private readonly AssignmentService _service;

    public AssignmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolPortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new SchoolPortalDbContext(options);
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _mockLogger = new Mock<ILogger<AssignmentService>>();
        _mockNotifications = new Mock<INotificationService>();
        _service = new AssignmentService(_context, _mockCurrentUser.Object, _mockLogger.Object, _mockNotifications.Object);

        _mockCurrentUser.Setup(x => x.SchoolId).Returns(TestSchoolId);
        _mockCurrentUser.Setup(x => x.UserId).Returns(TestUserId);
        _mockCurrentUser.Setup(x => x.Role).Returns("Teacher");

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

        var user = new User
        {
            UserId = TestUserId,
            SchoolId = TestSchoolId,
            Email = "teacher@test.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "Teacher",
            Role = "Teacher",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var classEntity = new Class
        {
            ClassId = TestClassId,
            SchoolId = TestSchoolId,
            Name = "Grade 10A",
            GradeLevel = 10,
            CreatedAt = DateTime.UtcNow
        };

        var subject = new Subject
        {
            SubjectId = TestSubjectId,
            SchoolId = TestSchoolId,
            Name = "Mathematics",
            Code = "MATH",
            CreatedAt = DateTime.UtcNow
        };

        var classSubject = new ClassSubject
        {
            ClassSubjectId = TestClassSubjectId,
            ClassId = TestClassId,
            SubjectId = TestSubjectId,
            SchoolId = TestSchoolId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Schools.Add(school);
        _context.Users.Add(user);
        _context.Classes.Add(classEntity);
        _context.Subjects.Add(subject);
        _context.ClassSubjects.Add(classSubject);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAssignment_ValidRequest_ShouldCreateAssignment()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = TestClassSubjectId,
            Title = "Test Assignment",
            Description = "Test Description",
            DueAt = DateTime.UtcNow.AddDays(7),
            MaxMarks = 100
        };

        // Act
        var result = await _service.CreateAssignmentAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Assignment", result.Title);
        Assert.Equal(100, result.MaxMarks);
        Assert.Equal("Mathematics", result.SubjectName);
    }

    [Fact]
    public async Task CreateAssignment_DueDateInPast_ShouldThrowException()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = TestClassSubjectId,
            Title = "Test Assignment",
            DueAt = DateTime.UtcNow.AddDays(-1),
            MaxMarks = 100
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAssignmentAsync(request));
    }

    [Fact]
    public async Task CreateAssignment_InvalidMaxMarks_ShouldThrowException()
    {
        // Arrange
        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = TestClassSubjectId,
            Title = "Test Assignment",
            DueAt = DateTime.UtcNow.AddDays(7),
            MaxMarks = 0
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAssignmentAsync(request));
    }

    [Fact]
    public async Task GetAssignments_ShouldReturnPaginatedResults()
    {
        // Arrange
        var assignment = new Assignment
        {
            ClassSubjectId = TestClassSubjectId,
            SchoolId = TestSchoolId,
            Title = "Test Assignment",
            DueAt = DateTime.UtcNow.AddDays(7),
            MaxMarks = 100,
            CreatedByUserId = TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAssignmentsAsync(null, null, null, null, 1, 20);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("Test Assignment", result.Items[0].Title);
    }

    [Fact]
    public async Task GetAssignmentById_ExistingId_ShouldReturnAssignment()
    {
        // Arrange
        var assignment = new Assignment
        {
            ClassSubjectId = TestClassSubjectId,
            SchoolId = TestSchoolId,
            Title = "Test Assignment",
            DueAt = DateTime.UtcNow.AddDays(7),
            MaxMarks = 100,
            CreatedByUserId = TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAssignmentByIdAsync(assignment.AssignmentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Assignment", result.Title);
    }

    [Fact]
    public async Task GetAssignmentById_NonExistingId_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetAssignmentByIdAsync(Guid.NewGuid()));
    }
}
