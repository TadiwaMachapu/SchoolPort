using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + [Authorize(Roles="Admin")]. Subject reads (reference data) →
// platform.access; subject management → academics.manage.
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _subjectService;

    public SubjectsController(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(List<SubjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects = await _subjectService.GetSubjectsAsync();
        return Ok(subjects);
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubject(Guid id)
    {
        var subject = await _subjectService.GetSubjectByIdAsync(id);
        return Ok(subject);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.AcademicsManage)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest request)
    {
        var subject = await _subjectService.CreateSubjectAsync(request);
        return CreatedAtAction(nameof(GetSubject), new { id = subject.SubjectId }, subject);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.AcademicsManage)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] UpdateSubjectRequest request)
    {
        var subject = await _subjectService.UpdateSubjectAsync(id, request);
        return Ok(subject);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.AcademicsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        await _subjectService.DeleteSubjectAsync(id);
        return NoContent();
    }
}
