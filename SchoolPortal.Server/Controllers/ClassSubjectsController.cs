using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/class-subjects")]
[Authorize(Roles = "Admin,Teacher")]
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
