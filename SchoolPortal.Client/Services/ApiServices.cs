using System.Net.Http.Json;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Subjects;
using SchoolPortal.Shared.DTOs.Submissions;
using SchoolPortal.Shared.DTOs.Grades;
using SchoolPortal.Shared.DTOs.Attendance;
using SchoolPortal.Shared.DTOs.Announcements;
using SchoolPortal.Shared.DTOs.Users;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Client.Services;

// Class Service
public interface IClassService
{
    Task<PaginatedResult<ClassDto>?> GetClassesAsync(int page = 1, int pageSize = 20);
    Task<ClassDetailsDto?> GetClassByIdAsync(int id);
    Task<ClassDto?> CreateClassAsync(CreateClassRequest request);
}

public class ClassService : IClassService
{
    private readonly HttpClient _httpClient;

    public ClassService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedResult<ClassDto>?> GetClassesAsync(int page = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"api/classes?page={page}&pageSize={pageSize}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PaginatedResult<ClassDto>>();
        }
        return null;
    }

    public async Task<ClassDetailsDto?> GetClassByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/classes/{id}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ClassDetailsDto>();
        }
        return null;
    }

    public async Task<ClassDto?> CreateClassAsync(CreateClassRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/classes", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ClassDto>();
        }
        return null;
    }
}

// Subject Service
public interface ISubjectService
{
    Task<List<SubjectDto>?> GetSubjectsAsync();
    Task<SubjectDto?> CreateSubjectAsync(CreateSubjectRequest request);
}

public class SubjectService : ISubjectService
{
    private readonly HttpClient _httpClient;

    public SubjectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SubjectDto>?> GetSubjectsAsync()
    {
        var response = await _httpClient.GetAsync("api/subjects");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<SubjectDto>>();
        }
        return null;
    }

    public async Task<SubjectDto?> CreateSubjectAsync(CreateSubjectRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/subjects", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SubjectDto>();
        }
        return null;
    }
}

// Submission Service
public interface ISubmissionService
{
    Task<bool> CreateSubmissionAsync(CreateSubmissionRequest request);
    Task<List<SubmissionDto>?> GetSubmissionsByAssignmentAsync(int assignmentId);
}

public class SubmissionService : ISubmissionService
{
    private readonly HttpClient _httpClient;

    public SubmissionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CreateSubmissionAsync(CreateSubmissionRequest request)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(request.AssignmentId.ToString()), "assignmentId");
        if (!string.IsNullOrEmpty(request.Comments))
        {
            content.Add(new StringContent(request.Comments), "comments");
        }

        var response = await _httpClient.PostAsync("api/submissions", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SubmissionDto>?> GetSubmissionsByAssignmentAsync(int assignmentId)
    {
        var response = await _httpClient.GetAsync($"api/submissions/by-assignment/{assignmentId}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<SubmissionDto>>();
        }
        return null;
    }
}

// Grade Service
public interface IGradeService
{
    Task<bool> CreateGradeAsync(CreateGradeRequest request);
    Task<bool> BulkGradeAsync(BulkGradeRequest request);
}

public class GradeService : IGradeService
{
    private readonly HttpClient _httpClient;

    public GradeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CreateGradeAsync(CreateGradeRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/grades", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BulkGradeAsync(BulkGradeRequest request)
    {
        var response = await _httpClient.PatchAsJsonAsync("api/grades/bulk", request);
        return response.IsSuccessStatusCode;
    }
}

// Attendance Service
public interface IAttendanceService
{
    Task<List<AttendanceDto>?> GetAttendanceAsync(int classId, DateTime date);
    Task<bool> BulkUpsertAttendanceAsync(BulkAttendanceRequest request);
}

public class AttendanceService : IAttendanceService
{
    private readonly HttpClient _httpClient;

    public AttendanceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AttendanceDto>?> GetAttendanceAsync(int classId, DateTime date)
    {
        var response = await _httpClient.GetAsync($"api/attendance?classId={classId}&date={date:yyyy-MM-dd}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<AttendanceDto>>();
        }
        return null;
    }

    public async Task<bool> BulkUpsertAttendanceAsync(BulkAttendanceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/attendance/bulk", request);
        return response.IsSuccessStatusCode;
    }
}

// Announcement Service
public interface IAnnouncementService
{
    Task<PaginatedResult<AnnouncementDto>?> GetAnnouncementsAsync(int page = 1, int pageSize = 20);
    Task<AnnouncementDto?> CreateAnnouncementAsync(CreateAnnouncementRequest request);
}

public class AnnouncementService : IAnnouncementService
{
    private readonly HttpClient _httpClient;

    public AnnouncementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedResult<AnnouncementDto>?> GetAnnouncementsAsync(int page = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"api/announcements?page={page}&pageSize={pageSize}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PaginatedResult<AnnouncementDto>>();
        }
        return null;
    }

    public async Task<AnnouncementDto?> CreateAnnouncementAsync(CreateAnnouncementRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/announcements", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AnnouncementDto>();
        }
        return null;
    }
}

// User Service
public interface IUserService
{
    Task<PaginatedResult<UserDto>?> GetUsersAsync(string? role = null, int page = 1, int pageSize = 20);
    Task<UserDto?> CreateUserAsync(CreateUserRequest request);
    Task<MeResponse?> GetCurrentUserProfileAsync();
}

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;

    public UserService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedResult<UserDto>?> GetUsersAsync(string? role = null, int page = 1, int pageSize = 20)
    {
        var query = $"api/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(role))
        {
            query += $"&role={role}";
        }

        var response = await _httpClient.GetAsync(query);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PaginatedResult<UserDto>>();
        }
        return null;
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UserDto>();
        }
        return null;
    }

    public async Task<MeResponse?> GetCurrentUserProfileAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/me");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MeResponse>();
            }
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"[Auth Error] GetCurrentUserProfileAsync failed with status {response.StatusCode}: {responseBody}");
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth Error] GetCurrentUserProfileAsync exception: {ex}");
            return null;
        }
    }
}
