using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/super")]
// Step 6 D3: SuperAdmin is a platform-level role OUTSIDE the school identity/permission model, so
// these endpoints use [RequireSuperAdmin] (not [RequirePermission]). The class-level justification
// records why they are exempt from the school permission model for the governance test.
[AnonymousJustification("SuperAdmin endpoints use a separate platform-level auth scheme outside the school identity model.")]
public class SuperAdminController : ControllerBase
{
    private readonly ISuperAdminService _service;

    public SuperAdminController(ISuperAdminService service)
    {
        _service = service;
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    [AnonymousJustification("Super-admin login: the caller has no token yet; this endpoint issues one.")]
    public async Task<IActionResult> Login([FromBody] SuperAdminLoginRequest request)
    {
        var result = await _service.LoginAsync(request.Email, request.Password);
        return Ok(result);
    }

    [HttpGet("stats")]
    [RequireSuperAdmin]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _service.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("schools")]
    [RequireSuperAdmin]
    public async Task<IActionResult> GetSchools()
    {
        var schools = await _service.GetAllSchoolsAsync();
        return Ok(schools);
    }

    [HttpPost("schools")]
    [RequireSuperAdmin]
    public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolRequest request)
    {
        var school = await _service.CreateSchoolAsync(request);
        return CreatedAtAction(nameof(GetSchools), school);
    }

    [HttpPut("schools/{id:guid}/features")]
    [RequireSuperAdmin]
    public async Task<IActionResult> UpdateFeatures(Guid id, [FromBody] UpdateSchoolFeaturesRequest request)
    {
        var school = await _service.UpdateFeaturesAsync(id, request);
        return Ok(school);
    }

    [HttpPatch("schools/{id:guid}/status")]
    [RequireSuperAdmin]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetSchoolStatusRequest request)
    {
        var school = await _service.SetStatusAsync(id, request.IsActive);
        return Ok(school);
    }
}
