using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Services;

public interface IAssignmentService
{
    Task<PaginatedResult<AssignmentDto>> GetAssignmentsAsync(Guid? classId, DateTime? dueFrom, DateTime? dueTo, string? status, int page, int pageSize);
    Task<AssignmentDto> GetAssignmentByIdAsync(Guid id);
    Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest request);
    Task<AssignmentDto> UpdateAssignmentAsync(Guid id, UpdateAssignmentRequest request);
}
