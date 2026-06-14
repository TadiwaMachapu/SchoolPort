using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Announcements;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + [Authorize(Roles="Admin,Teacher")] writes. Reading is any-
// authenticated (platform.access); publishing announcements → announcements.publish.
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementsController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.PlatformAccess)]
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
    [RequirePermission(PermissionKeys.AnnouncementsPublish)]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest request)
    {
        var announcement = await _announcementService.CreateAnnouncementAsync(request);
        return CreatedAtAction(nameof(GetAnnouncements), new { }, announcement);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.AnnouncementsPublish)]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAnnouncement(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        var announcement = await _announcementService.UpdateAnnouncementAsync(id, request);
        return Ok(announcement);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.AnnouncementsPublish)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnnouncement(Guid id)
    {
        await _announcementService.DeleteAnnouncementAsync(id);
        return NoContent();
    }
}
