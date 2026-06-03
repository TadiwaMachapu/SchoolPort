using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClassesController : ControllerBase
{
    private readonly IClassService _classService;

    public ClassesController(IClassService classService)
    {
        _classService = classService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ClassDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(
        [FromQuery] int? year,
        [FromQuery] string? q,
        [FromQuery] bool mine = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _classService.GetClassesAsync(year, q, page, pageSize, mine);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClassDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClass(Guid id)
    {
        var classDto = await _classService.GetClassByIdAsync(id);
        return Ok(classDto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ClassDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request)
    {
        var classDto = await _classService.CreateClassAsync(request);
        return CreatedAtAction(nameof(GetClass), new { id = classDto.ClassId }, classDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ClassDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClass(Guid id, [FromBody] UpdateClassRequest request)
    {
        var classDto = await _classService.UpdateClassAsync(id, request);
        return Ok(classDto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClass(Guid id)
    {
        await _classService.DeleteClassAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/students")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents(Guid id)
    {
        var students = await _classService.GetStudentsAsync(id);
        return Ok(students);
    }

    [HttpGet("{id}/subjects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjects(Guid id)
    {
        var subjects = await _classService.GetSubjectsForClassAsync(id);
        return Ok(subjects);
    }
}
