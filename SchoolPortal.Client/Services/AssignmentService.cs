using System.Net.Http.Json;
using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Client.Services;

public class AssignmentService : IAssignmentService
{
    private readonly HttpClient _httpClient;

    public AssignmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedResult<AssignmentDto>?> GetAssignmentsAsync(int? classId = null, int page = 1, int pageSize = 20)
    {
        var query = $"api/assignments?page={page}&pageSize={pageSize}";
        if (classId.HasValue)
        {
            query += $"&classId={classId.Value}";
        }

        var response = await _httpClient.GetAsync(query);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PaginatedResult<AssignmentDto>>();
        }
        return null;
    }

    public async Task<AssignmentDto?> GetAssignmentByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/assignments/{id}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AssignmentDto>();
        }
        return null;
    }

    public async Task<AssignmentDto?> CreateAssignmentAsync(CreateAssignmentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/assignments", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AssignmentDto>();
        }
        return null;
    }

    public async Task<bool> UpdateAssignmentAsync(int id, UpdateAssignmentRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/assignments/{id}", request);
        return response.IsSuccessStatusCode;
    }
}
