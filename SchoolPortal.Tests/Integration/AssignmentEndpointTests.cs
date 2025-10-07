using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Assignments;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SchoolPortal.Tests.Integration;

public class AssignmentEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AssignmentEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<SchoolPortalDbContext>));
                
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<SchoolPortalDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });

                // Seed test data
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SchoolPortalDbContext>();
                SeedTestData(db);
            });
        });

        _client = _factory.CreateClient();
    }

    private static void SeedTestData(SchoolPortalDbContext db)
    {
        var school = new School
        {
            SchoolId = 1,
            Name = "Test School",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            UserId = 1,
            SchoolId = 1,
            Email = "test@school.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
            FirstName = "Test",
            LastName = "Teacher",
            Role = "Teacher",
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

        var subject = new Subject
        {
            SubjectId = 1,
            SchoolId = 1,
            Name = "Mathematics",
            Code = "MATH",
            CreatedAt = DateTime.UtcNow
        };

        var classSubject = new ClassSubject
        {
            ClassSubjectId = 1,
            ClassId = 1,
            SubjectId = 1,
            SchoolId = 1,
            CreatedAt = DateTime.UtcNow
        };

        db.Schools.Add(school);
        db.Users.Add(user);
        db.Classes.Add(classEntity);
        db.Subjects.Add(subject);
        db.ClassSubjects.Add(classSubject);
        db.SaveChanges();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new
        {
            email = "test@school.com",
            password = "Password123"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<JsonElement>(content);
        return loginResponse.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task CreateAssignment_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = 1,
            Title = "Integration Test Assignment",
            Description = "Test Description",
            DueAt = DateTime.UtcNow.AddDays(7),
            MaxMarks = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/assignments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var assignment = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.Equal("Integration Test Assignment", assignment.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetAssignments_WithAuth_ReturnsOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/assignments?page=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAssignments_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/assignments");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAssignment_InvalidDueDate_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateAssignmentRequest
        {
            ClassSubjectId = 1,
            Title = "Test Assignment",
            DueAt = DateTime.UtcNow.AddDays(-1), // Past date
            MaxMarks = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/assignments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
