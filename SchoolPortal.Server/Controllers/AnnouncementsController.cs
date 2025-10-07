using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Announcements;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementsController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnnouncements(
        [FromQuery] DateTime? since,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _announcementService.GetAnnouncementsAsync(since, page, pageSize);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest request)
    {
        var announcement = await _announcementService.CreateAnnouncementAsync(request);
        return CreatedAtAction(nameof(GetAnnouncements), new { }, announcement);
    }
}
