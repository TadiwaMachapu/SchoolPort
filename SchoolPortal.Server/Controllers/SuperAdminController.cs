using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/super")]
public class SuperAdminController : ControllerBase
{
    private readonly ISuperAdminService _service;

    public SuperAdminController(ISuperAdminService service)
    {
        _service = service;
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SuperAdminLoginRequest request)
    {
        var result = await _service.LoginAsync(request.Email, request.Password);
        return Ok(result);
    }

    [HttpGet("stats")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _service.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("schools")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetSchools()
    {
        var schools = await _service.GetAllSchoolsAsync();
        return Ok(schools);
    }

    [HttpPost("schools")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolRequest request)
    {
        var school = await _service.CreateSchoolAsync(request);
        return CreatedAtAction(nameof(GetSchools), school);
    }

    [HttpPut("schools/{id:guid}/features")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdateFeatures(Guid id, [FromBody] UpdateSchoolFeaturesRequest request)
    {
        var school = await _service.UpdateFeaturesAsync(id, request);
        return Ok(school);
    }

    [HttpPatch("schools/{id:guid}/status")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetSchoolStatusRequest request)
    {
        var school = await _service.SetStatusAsync(id, request.IsActive);
        return Ok(school);
    }
}
