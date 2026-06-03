using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Attendance;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendance([FromQuery] Guid classId, [FromQuery] DateTime date)
    {
        var attendance = await _attendanceService.GetAttendanceAsync(classId, date);
        return Ok(attendance);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(List<MyAttendanceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAttendance([FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _attendanceService.GetMyAttendanceAsync(month, year);
        return Ok(result);
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkUpsertAttendance([FromBody] BulkAttendanceRequest request)
    {
        await _attendanceService.BulkUpsertAttendanceAsync(request);
        return NoContent();
    }
}
