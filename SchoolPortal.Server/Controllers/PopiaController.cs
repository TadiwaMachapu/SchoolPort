using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PopiaController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PopiaController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // GET /api/popia/consents/mine
    [HttpGet("consents/mine")]
    public async Task<IActionResult> GetMyConsents()
    {
        var record = await _context.ConsentRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == _currentUser.UserId && c.SchoolId == _currentUser.SchoolId);

        return Ok(record ?? new ConsentRecord
        {
            UserId = _currentUser.UserId,
            SchoolId = _currentUser.SchoolId,
            DataProcessing = false,
            MarketingCommunications = false,
            ThirdPartySharing = false,
            Photography = false,
            UpdatedAt = DateTime.UtcNow
        });
    }

    // PUT /api/popia/consents
    [HttpPut("consents")]
    public async Task<IActionResult> UpdateConsents([FromBody] ConsentUpdateRequest request)
    {
        var existing = await _context.ConsentRecords
            .FirstOrDefaultAsync(c => c.UserId == _currentUser.UserId && c.SchoolId == _currentUser.SchoolId);

        if (existing == null)
        {
            existing = new ConsentRecord
            {
                UserId = _currentUser.UserId,
                SchoolId = _currentUser.SchoolId
            };
            _context.ConsentRecords.Add(existing);
        }

        existing.DataProcessing = request.DataProcessing;
        existing.MarketingCommunications = request.MarketingCommunications;
        existing.ThirdPartySharing = request.ThirdPartySharing;
        existing.Photography = request.Photography;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    // GET /api/popia/requests/mine
    [HttpGet("requests/mine")]
    public async Task<IActionResult> GetMyRequests()
    {
        var requests = await _context.DataSubjectRequests
            .AsNoTracking()
            .Where(r => r.UserId == _currentUser.UserId && r.SchoolId == _currentUser.SchoolId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.RequestId,
                r.RequestType,
                r.Description,
                r.Status,
                r.AdminNotes,
                r.CreatedAt,
                r.ResolvedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    // POST /api/popia/requests
    [HttpPost("requests")]
    public async Task<IActionResult> SubmitRequest([FromBody] DataSubjectRequestCreate request)
    {
        var dsr = new DataSubjectRequest
        {
            SchoolId = _currentUser.SchoolId,
            UserId = _currentUser.UserId,
            RequestType = request.RequestType,
            Description = request.Description,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.DataSubjectRequests.Add(dsr);
        await _context.SaveChangesAsync();
        return Ok(new { dsr.RequestId });
    }

    // GET /api/popia/admin/consents [Admin]
    [HttpGet("admin/consents")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminGetConsents()
    {
        var consents = await _context.ConsentRecords
            .AsNoTracking()
            .Where(c => c.SchoolId == _currentUser.SchoolId)
            .Include(c => c.User)
            .OrderBy(c => c.User.LastName)
            .Select(c => new
            {
                c.ConsentRecordId,
                c.UserId,
                Name = $"{c.User.FirstName} {c.User.LastName}",
                c.User.Email,
                c.User.Role,
                c.DataProcessing,
                c.MarketingCommunications,
                c.ThirdPartySharing,
                c.Photography,
                c.UpdatedAt
            })
            .ToListAsync();

        return Ok(consents);
    }

    // GET /api/popia/admin/requests [Admin]
    [HttpGet("admin/requests")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminGetRequests([FromQuery] string? status)
    {
        var query = _context.DataSubjectRequests
            .AsNoTracking()
            .Where(r => r.SchoolId == _currentUser.SchoolId)
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.RequestId,
                r.UserId,
                Name = $"{r.User.FirstName} {r.User.LastName}",
                r.User.Email,
                r.RequestType,
                r.Description,
                r.Status,
                r.AdminNotes,
                r.CreatedAt,
                r.ResolvedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    // PUT /api/popia/admin/requests/{id} [Admin]
    [HttpPut("admin/requests/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateRequest(Guid id, [FromBody] RequestStatusUpdate update)
    {
        var request = await _context.DataSubjectRequests
            .FirstOrDefaultAsync(r => r.RequestId == id && r.SchoolId == _currentUser.SchoolId);

        if (request == null) return NotFound();

        request.Status = update.Status;
        request.AdminNotes = update.AdminNotes;
        if (update.Status is "Completed" or "Rejected")
            request.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record ConsentUpdateRequest(bool DataProcessing, bool MarketingCommunications, bool ThirdPartySharing, bool Photography);
public record DataSubjectRequestCreate(string RequestType, string? Description);
public record RequestStatusUpdate(string Status, string? AdminNotes);
