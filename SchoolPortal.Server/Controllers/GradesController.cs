using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Grades;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Teacher")]
public class GradesController : ControllerBase
{
    private readonly IGradeService _gradeService;

    public GradesController(IGradeService gradeService)
    {
        _gradeService = gradeService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGrade([FromBody] CreateGradeRequest request)
    {
        await _gradeService.CreateGradeAsync(request);
        return CreatedAtAction(nameof(CreateGrade), new { }, null);
    }

    [HttpPatch("bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkGrade([FromBody] BulkGradeRequest request)
    {
        await _gradeService.BulkGradeAsync(request);
        return NoContent();
    }
}
