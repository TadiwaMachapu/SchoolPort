using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Classes;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize(Roles="Admin")]. Bulk roster management → system.users_manage
// (Principal, ITAdministrator). Sensitive → DB-resolved per request.
[RequirePermission(PermissionKeys.SystemUsersManage)]
public class EnrolmentsController : ControllerBase
{
    private readonly IClassService _classService;

    public EnrolmentsController(IClassService classService)
    {
        _classService = classService;
    }

    [HttpPost("bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkEnroll([FromBody] BulkEnrollmentRequest request)
    {
        await _classService.BulkEnrollAsync(request);
        return NoContent();
    }
}
