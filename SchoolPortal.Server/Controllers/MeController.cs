using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Users;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize]. Returns the current user's own profile → platform.access (D1).
[RequirePermission(PermissionKeys.PlatformAccess)]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUser;

    public MeController(IUserService userService, ICurrentUserService currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe()
    {
        var response = await _userService.GetMeAsync(_currentUser.UserId);
        return Ok(response);
    }
}
