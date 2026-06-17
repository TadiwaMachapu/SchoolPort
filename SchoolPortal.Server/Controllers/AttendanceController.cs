using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Attendance;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] (class) + role overrides. Class attendance view (takes classId) →
// attendance.view_class; learner's own → attendance.view_own; capture → attendance.capture.
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.AttendanceViewClass)]
    [ProducesResponseType(typeof(List<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendance([FromQuery] Guid classId, [FromQuery] DateTime date)
    {
        var attendance = await _attendanceService.GetAttendanceAsync(classId, date);
        return Ok(attendance);
    }

    [HttpGet("mine")]
    [RequirePermission(PermissionKeys.AttendanceViewOwn)]
    [ProducesResponseType(typeof(List<MyAttendanceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAttendance([FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _attendanceService.GetMyAttendanceAsync(month, year);
        return Ok(result);
    }

    [HttpPost("bulk")]
    [RequirePermission(PermissionKeys.AttendanceCapture)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkUpsertAttendance([FromBody] BulkAttendanceRequest request)
    {
        await _attendanceService.BulkUpsertAttendanceAsync(request);
        return NoContent();
    }
}
