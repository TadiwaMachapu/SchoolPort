using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Services;

public interface IAssignmentService
{
    Task<PaginatedResult<AssignmentDto>> GetAssignmentsAsync(int? classId, DateTime? dueFrom, DateTime? dueTo, string? status, int page, int pageSize);
    Task<AssignmentDto> GetAssignmentByIdAsync(int id);
    Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest request);
    Task<AssignmentDto> UpdateAssignmentAsync(int id, UpdateAssignmentRequest request);
}
