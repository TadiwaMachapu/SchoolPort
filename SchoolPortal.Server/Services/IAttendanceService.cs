using SchoolPortal.Shared.DTOs.Attendance;

namespace SchoolPortal.Server.Services;

public interface IAttendanceService
{
    Task<List<AttendanceDto>> GetAttendanceAsync(int classId, DateTime date);
    Task BulkUpsertAttendanceAsync(BulkAttendanceRequest request);
}
