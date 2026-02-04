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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _classService.GetClassesAsync(year, q, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClassDto), StatusCodes.Status200OK)]
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
}
