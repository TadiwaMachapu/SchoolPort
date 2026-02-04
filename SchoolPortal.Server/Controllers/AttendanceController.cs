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

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkUpsertAttendance([FromBody] BulkAttendanceRequest request)
    {
        await _attendanceService.BulkUpsertAttendanceAsync(request);
        return NoContent();
    }
}
