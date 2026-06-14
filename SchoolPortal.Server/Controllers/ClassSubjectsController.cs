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
}
