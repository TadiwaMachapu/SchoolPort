using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Positions;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/positions")]
// Sprint 1.5.0 Step 9 — position assignment/management. Entirely gated by system.positions_assign
// (Principal, DeputyPrincipal, ITAdministrator; Sensitive → DB-resolved per request).
[RequirePermission(PermissionKeys.SystemPositionsAssign)]
public class PositionsController : ControllerBase
{
    private readonly IPositionService _positions;

    public PositionsController(IPositionService positions)
    {
        _positions = positions;
    }

    [HttpGet("catalogue")]
    public async Task<IActionResult> GetCatalogue() => Ok(await _positions.GetCatalogueAsync());

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview() => Ok(await _positions.GetOverviewAsync());

    [HttpGet("assignments")]
    public async Task<IActionResult> GetUserAssignments([FromQuery] Guid userId)
    {
        try { return Ok(await _positions.GetUserAssignmentsAsync(userId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] AssignPositionRequest request)
    {
        try { return Ok(await _positions.AssignAsync(request)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("assignments/{userPositionId}")]
    public async Task<IActionResult> Update(Guid userPositionId, [FromBody] UpdateAssignmentRequest request)
    {
        try { return Ok(await _positions.UpdateAsync(userPositionId, request)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("assignments/{userPositionId}/revoke")]
    public async Task<IActionResult> Revoke(Guid userPositionId)
    {
        try { await _positions.RevokeAsync(userPositionId); return Ok(new { revoked = true }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
