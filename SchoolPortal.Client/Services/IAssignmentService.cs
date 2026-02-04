using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Client.Services;

public interface IAssignmentService
{
    Task<PaginatedResult<AssignmentDto>?> GetAssignmentsAsync(int? classId = null, int page = 1, int pageSize = 20);
    Task<AssignmentDto?> GetAssignmentByIdAsync(int id);
    Task<AssignmentDto?> CreateAssignmentAsync(CreateAssignmentRequest request);
    Task<bool> UpdateAssignmentAsync(int id, UpdateAssignmentRequest request);
}
