using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _subjectService;

    public SubjectsController(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SubjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects = await _subjectService.GetSubjectsAsync();
        return Ok(subjects);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest request)
    {
        var subject = await _subjectService.CreateSubjectAsync(request);
        return CreatedAtAction(nameof(GetSubjects), new { }, subject);
    }
}
