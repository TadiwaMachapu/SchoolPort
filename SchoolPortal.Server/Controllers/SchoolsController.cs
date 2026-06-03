using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Schools;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchoolsController : ControllerBase
{
    private readonly ISchoolService _schoolService;
    private readonly ISubjectService _subjectService;

    public SchoolsController(ISchoolService schoolService, ISubjectService subjectService)
    {
        _schoolService = schoolService;
        _subjectService = subjectService;
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(SchoolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentSchool()
    {
        var school = await _schoolService.GetCurrentSchoolAsync();
        return Ok(school);
    }

    [HttpPut("info")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SchoolDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateInfo([FromBody] UpdateSchoolInfoRequest request)
    {
        var school = await _schoolService.UpdateInfoAsync(request);
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

    [HttpGet("settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _schoolService.GetSettingsAsync();
        return Ok(settings);
    }

    [HttpPut("settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSchoolSettingsRequest request)
    {
        var settings = await _schoolService.UpdateSettingsAsync(request);
        return Ok(settings);
    }

    [HttpPost("seed-caps-subjects")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CapsSeedException), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedCapsSubjects()
    {
        var result = await _subjectService.SeedCapsSubjectsAsync();
        return Ok(result);
    }
}
