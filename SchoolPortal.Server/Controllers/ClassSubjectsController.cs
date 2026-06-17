using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/class-subjects")]
// Step 6: was [Authorize(Roles="Admin,Teacher")]. Subject-teacher assignment is academic
// structure management → academics.manage (AS-3 tightening off rank-and-file teachers).
[RequirePermission(PermissionKeys.AcademicsManage)]
public class ClassSubjectsController : ControllerBase
{
    private readonly ISubjectService _subjectService;

    public ClassSubjectsController(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    [HttpPost("bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkAssign([FromBody] BulkClassSubjectRequest request)
    {
        await _subjectService.BulkAssignClassSubjectsAsync(request);
        return NoContent();
    }

    // Step 9.5 (Build #6b): assignable teachers for the class-subject "Assign teacher" UI.
    // Inherits the class-level academics.manage gate (same as the assignment itself).
    [HttpGet("teachers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers = await _subjectService.GetTeachersAsync();
        return Ok(teachers);
    }
}
