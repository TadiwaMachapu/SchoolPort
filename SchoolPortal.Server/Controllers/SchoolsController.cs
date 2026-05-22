using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchoolsController : ControllerBase
{
    private readonly ISchoolService _schoolService;

    public SchoolsController(ISchoolService schoolService)
    {
        _schoolService = schoolService;
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(SchoolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentSchool()
    {
        var school = await _schoolService.GetCurrentSchoolAsync();
        return Ok(school);
    }

    [HttpPut("theme")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SchoolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTheme([FromBody] UpdateSchoolThemeRequest request)
    {
        var school = await _schoolService.UpdateThemeAsync(request);
        return Ok(school);
    }

    [HttpPut("features")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SchoolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFeatures([FromBody] UpdateSchoolFeaturesRequest request)
    {
        var school = await _schoolService.UpdateFeaturesAsync(request);
        return Ok(school);
    }
}
