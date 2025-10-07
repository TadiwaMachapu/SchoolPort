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
}
