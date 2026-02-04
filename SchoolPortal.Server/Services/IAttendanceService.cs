using SchoolPortal.Shared.DTOs.Attendance;

namespace SchoolPortal.Server.Services;

public interface IAttendanceService
{
    Task<List<AttendanceDto>> GetAttendanceAsync(Guid classId, DateTime date);
    Task BulkUpsertAttendanceAsync(BulkAttendanceRequest request);
}
