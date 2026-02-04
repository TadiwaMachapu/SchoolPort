using SchoolPortal.Shared.DTOs.Announcements;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Subjects;
using SchoolPortal.Shared.DTOs.Submissions;
using SchoolPortal.Shared.DTOs.Grades;
using SchoolPortal.Shared.DTOs.Attendance;
using SchoolPortal.Shared.DTOs.Users;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Auth;

namespace SchoolPortal.Client.Services;

public class MockAnnouncementService : IAnnouncementService
{
    private readonly List<AnnouncementDto> _announcements = new()
    {
        new AnnouncementDto
        {
            AnnouncementId = Guid.NewGuid(),
            Title = "Welcome to the New School Year!",
            Content = "We're excited to welcome all students back for another amazing year of learning and growth. Please review the updated school calendar and policies.",
            Audience = "All",
            CreatedByName = "Principal Johnson",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            IsActive = true
        },
        new AnnouncementDto
        {
            AnnouncementId = Guid.NewGuid(),
            Title = "Parent-Teacher Conferences Next Week",
            Content = "Parent-teacher conferences will be held next week from Monday to Thursday. Please schedule your appointments through the parent portal.",
            Audience = "All",
            CreatedByName = "Admin Office",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            IsActive = true
        },
        new AnnouncementDto
        {
            AnnouncementId = Guid.NewGuid(),
            Title = "Grade 10 Field Trip - Science Museum",
            Content = "All Grade 10 students are invited to join us for a field trip to the Science Museum on Friday. Permission slips must be submitted by Wednesday.",
            Audience = "Grade",
            AudienceValue = "10",
            CreatedByName = "Ms. Anderson",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            IsActive = true
        },
        new AnnouncementDto
        {
            AnnouncementId = Guid.NewGuid(),
            Title = "Math Competition Registration Open",
            Content = "Students interested in participating in the regional math competition should register with Mr. Thompson by the end of this week.",
            Audience = "All",
            CreatedByName = "Mr. Thompson",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        },
        new AnnouncementDto
        {
            AnnouncementId = Guid.NewGuid(),
            Title = "Library Hours Extended",
            Content = "The school library will now be open until 6 PM on weekdays to accommodate students who need extra study time.",
            Audience = "All",
            CreatedByName = "Librarian Smith",
            CreatedAt = DateTime.UtcNow.AddHours(-12),
            IsActive = true
        }
    };

    public Task<PaginatedResult<AnnouncementDto>?> GetAnnouncementsAsync(int page = 1, int pageSize = 20)
    {
        var items = _announcements
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PaginatedResult<AnnouncementDto>
        {
            Items = items,
            Total = _announcements.Count,
            Page = page,
            PageSize = pageSize
        };

        return Task.FromResult<PaginatedResult<AnnouncementDto>?>(result);
    }

    public Task<AnnouncementDto?> CreateAnnouncementAsync(CreateAnnouncementRequest request)
    {
        var announcement = new AnnouncementDto
        {
            AnnouncementId = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Audience = request.Audience,
            AudienceValue = request.AudienceValue,
            CreatedByName = "Current User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _announcements.Insert(0, announcement);
        return Task.FromResult<AnnouncementDto?>(announcement);
    }
}

public class MockClassService : IClassService
{
    private readonly List<ClassDto> _classes = new()
    {
        new ClassDto
        {
            ClassId = Guid.NewGuid(),
            Name = "Grade 10A - Mathematics",
            GradeLevel = 10,
            AcademicYear = 2024,
            TeacherName = "Mr. Thompson",
            StudentCount = 28,
            MaxCapacity = 30
        },
        new ClassDto
        {
            ClassId = Guid.NewGuid(),
            Name = "Grade 11B - Physics",
            GradeLevel = 11,
            AcademicYear = 2024,
            TeacherName = "Ms. Anderson",
            StudentCount = 25,
            MaxCapacity = 30
        },
        new ClassDto
        {
            ClassId = Guid.NewGuid(),
            Name = "Grade 9C - English Literature",
            GradeLevel = 9,
            AcademicYear = 2024,
            TeacherName = "Mrs. Williams",
            StudentCount = 30,
            MaxCapacity = 30
        }
    };

    public Task<PaginatedResult<ClassDto>?> GetClassesAsync(int page = 1, int pageSize = 20)
    {
        var items = _classes
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PaginatedResult<ClassDto>
        {
            Items = items,
            Total = _classes.Count,
            Page = page,
            PageSize = pageSize
        };

        return Task.FromResult<PaginatedResult<ClassDto>?>(result);
    }

    public Task<ClassDetailsDto?> GetClassByIdAsync(int id)
    {
        var classDto = _classes.FirstOrDefault();
        if (classDto == null) return Task.FromResult<ClassDetailsDto?>(null);

        var details = new ClassDetailsDto
        {
            ClassId = classDto.ClassId,
            Name = classDto.Name,
            GradeLevel = classDto.GradeLevel,
            AcademicYear = classDto.AcademicYear,
            TeacherName = classDto.TeacherName,
            StudentCount = classDto.StudentCount,
            MaxCapacity = classDto.MaxCapacity
        };

        return Task.FromResult<ClassDetailsDto?>(details);
    }

    public Task<ClassDto?> CreateClassAsync(CreateClassRequest request)
    {
        var classDto = new ClassDto
        {
            ClassId = Guid.NewGuid(),
            Name = request.Name,
            GradeLevel = request.GradeLevel,
            AcademicYear = request.AcademicYear,
            StudentCount = 0,
            MaxCapacity = request.MaxCapacity
        };

        _classes.Add(classDto);
        return Task.FromResult<ClassDto?>(classDto);
    }
}

public class MockSubjectService : ISubjectService
{
    private readonly List<SubjectDto> _subjects = new()
    {
        new SubjectDto { SubjectId = Guid.NewGuid(), Name = "Mathematics", Code = "MATH101", Description = "Advanced Mathematics" },
        new SubjectDto { SubjectId = Guid.NewGuid(), Name = "Physics", Code = "PHYS101", Description = "Introduction to Physics" },
        new SubjectDto { SubjectId = Guid.NewGuid(), Name = "Chemistry", Code = "CHEM101", Description = "General Chemistry" },
        new SubjectDto { SubjectId = Guid.NewGuid(), Name = "English Literature", Code = "ENG101", Description = "Classic and Modern Literature" },
        new SubjectDto { SubjectId = Guid.NewGuid(), Name = "History", Code = "HIST101", Description = "World History" }
    };

    public Task<List<SubjectDto>?> GetSubjectsAsync()
    {
        return Task.FromResult<List<SubjectDto>?>(_subjects.ToList());
    }

    public Task<SubjectDto?> CreateSubjectAsync(CreateSubjectRequest request)
    {
        var subject = new SubjectDto
        {
            SubjectId = Guid.NewGuid(),
            Name = request.Name,
            Code = request.Code,
            Description = request.Description
        };

        _subjects.Add(subject);
        return Task.FromResult<SubjectDto?>(subject);
    }
}

public class MockSubmissionService : ISubmissionService
{
    public Task<bool> CreateSubmissionAsync(CreateSubmissionRequest request)
    {
        return Task.FromResult(true);
    }

    public Task<List<SubmissionDto>?> GetSubmissionsByAssignmentAsync(int assignmentId)
    {
        var submissions = new List<SubmissionDto>
        {
            new SubmissionDto
            {
                SubmissionId = Guid.NewGuid(),
                AssignmentId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                StudentName = "John Doe",
                StudentNumber = "S12345",
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                Comments = "Completed assignment on time"
            }
        };

        return Task.FromResult<List<SubmissionDto>?>(submissions);
    }
}

public class MockGradeService : IGradeService
{
    public Task<bool> CreateGradeAsync(CreateGradeRequest request)
    {
        return Task.FromResult(true);
    }

    public Task<bool> BulkGradeAsync(BulkGradeRequest request)
    {
        return Task.FromResult(true);
    }
}

public class MockAttendanceService : IAttendanceService
{
    public Task<List<AttendanceDto>?> GetAttendanceAsync(int classId, DateTime date)
    {
        var attendance = new List<AttendanceDto>
        {
            new AttendanceDto
            {
                AttendanceId = Guid.NewGuid(),
                ClassId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                StudentName = "John Doe",
                Date = date,
                Status = 1
            }
        };

        return Task.FromResult<List<AttendanceDto>?>(attendance);
    }

    public Task<bool> BulkUpsertAttendanceAsync(BulkAttendanceRequest request)
    {
        return Task.FromResult(true);
    }
}

public class MockUserService : IUserService
{
    private readonly List<UserDto> _users = new()
    {
        new UserDto
        {
            UserId = Guid.NewGuid(),
            SchoolId = Guid.NewGuid(),
            Email = "admin@school.com",
            FirstName = "Admin",
            LastName = "User",
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        },
        new UserDto
        {
            UserId = Guid.NewGuid(),
            SchoolId = Guid.NewGuid(),
            Email = "teacher@school.com",
            FirstName = "Jane",
            LastName = "Teacher",
            Role = "Teacher",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-3)
        }
    };

    public Task<PaginatedResult<UserDto>?> GetUsersAsync(string? role = null, int page = 1, int pageSize = 20)
    {
        var query = _users.AsEnumerable();
        
        if (!string.IsNullOrEmpty(role))
        {
            query = query.Where(u => u.Role == role);
        }

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PaginatedResult<UserDto>
        {
            Items = items,
            Total = query.Count(),
            Page = page,
            PageSize = pageSize
        };

        return Task.FromResult<PaginatedResult<UserDto>?>(result);
    }

    public Task<UserDto?> CreateUserAsync(CreateUserRequest request)
    {
        var user = new UserDto
        {
            UserId = Guid.NewGuid(),
            SchoolId = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _users.Add(user);
        return Task.FromResult<UserDto?>(user);
    }

    public Task<MeResponse?> GetCurrentUserProfileAsync()
    {
        var response = new MeResponse
        {
            User = new UserProfile
            {
                UserId = Guid.NewGuid(),
                Email = "demo@school.com",
                FirstName = "Demo",
                LastName = "User",
                Role = "Admin"
            },
            School = new SchoolInfo
            {
                SchoolId = Guid.NewGuid(),
                Name = "Demo School",
                LogoUrl = null,
                PrimaryColor = "#1976d2"
            }
        };

        return Task.FromResult<MeResponse?>(response);
    }
}

public class MockAuthService : IAuthService
{
    public Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = new LoginResponse
        {
            AccessToken = "mock-jwt-token",
            RefreshToken = "mock-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            User = new UserInfo
            {
                UserId = Guid.NewGuid(),
                SchoolId = Guid.NewGuid(),
                Email = request.Email,
                FirstName = "Demo",
                LastName = "User",
                Role = "Admin"
            }
        };

        return Task.FromResult<LoginResponse?>(response);
    }

    public Task LogoutAsync()
    {
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync()
    {
        return Task.FromResult<string?>("mock-jwt-token");
    }

    public Task<UserInfo?> GetCurrentUserAsync()
    {
        var user = new UserInfo
        {
            UserId = Guid.NewGuid(),
            SchoolId = Guid.NewGuid(),
            Email = "demo@school.com",
            FirstName = "Demo",
            LastName = "User",
            Role = "Admin"
        };

        return Task.FromResult<UserInfo?>(user);
    }
}
