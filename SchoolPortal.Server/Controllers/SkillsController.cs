using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Learner self-service (mine/create/delete-own) →
// platform.access; staff view + endorsement of learner skills → skills.endorse (CS-6 — a staff
// trust action, never platform.access).
public class SkillsController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SkillsController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // GET /api/skills/mine [Student]
    [HttpGet("mine")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetMine()
    {
        var schoolId = _currentUser.SchoolId;
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => new { s.StudentId })
            .FirstOrDefaultAsync();

        if (student == null) return NotFound("Student record not found.");

        var skills = await _context.SkillEntries
            .AsNoTracking()
            .Where(e => e.StudentId == student.StudentId && e.SchoolId == schoolId)
            .OrderByDescending(e => e.Date)
            .Select(e => new
            {
                e.SkillEntryId,
                e.Title,
                e.Category,
                e.Description,
                e.Date,
                e.EndorsedByUserId,
                e.EndorsedAt,
                EndorsedByName = e.EndorsedByUser != null
                    ? $"{e.EndorsedByUser.FirstName} {e.EndorsedByUser.LastName}"
                    : (string?)null,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(skills);
    }

    // GET /api/skills/learner/{userId} [Admin, Teacher]
    [HttpGet("learner/{userId}")]
    [RequirePermission(PermissionKeys.SkillsEndorse)]
    public async Task<IActionResult> GetLearnerSkills(Guid userId)
    {
        var schoolId = _currentUser.SchoolId;
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.SchoolId == schoolId)
            .Select(s => new { s.StudentId })
            .FirstOrDefaultAsync();

        if (student == null) return NotFound("Learner not found.");

        var skills = await _context.SkillEntries
            .AsNoTracking()
            .Where(e => e.StudentId == student.StudentId && e.SchoolId == schoolId)
            .OrderByDescending(e => e.Date)
            .Select(e => new
            {
                e.SkillEntryId,
                e.Title,
                e.Category,
                e.Description,
                e.Date,
                e.EndorsedByUserId,
                e.EndorsedAt,
                EndorsedByName = e.EndorsedByUser != null
                    ? $"{e.EndorsedByUser.FirstName} {e.EndorsedByUser.LastName}"
                    : (string?)null,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(skills);
    }

    // POST /api/skills [Student]
    [HttpPost]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> Create([FromBody] CreateSkillRequest request)
    {
        var schoolId = _currentUser.SchoolId;
        var student = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => new { s.StudentId })
            .FirstOrDefaultAsync();

        if (student == null) return NotFound("Student record not found.");

        var entry = new SkillEntry
        {
            StudentId = student.StudentId,
            SchoolId = schoolId,
            Title = request.Title,
            Category = request.Category,
            Description = request.Description,
            Date = request.Date,
            CreatedAt = DateTime.UtcNow
        };

        _context.SkillEntries.Add(entry);
        await _context.SaveChangesAsync();
        return Ok(new { entry.SkillEntryId });
    }

    // DELETE /api/skills/{id} [Student — own only]
    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var schoolId = _currentUser.SchoolId;
        var student = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => new { s.StudentId })
            .FirstOrDefaultAsync();

        if (student == null) return NotFound();

        var entry = await _context.SkillEntries
            .FirstOrDefaultAsync(e => e.SkillEntryId == id && e.StudentId == student.StudentId);

        if (entry == null) return NotFound();

        _context.SkillEntries.Remove(entry);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/skills/{id}/endorse [Admin, Teacher]
    [HttpPost("{id}/endorse")]
    [RequirePermission(PermissionKeys.SkillsEndorse)]
    public async Task<IActionResult> Endorse(Guid id)
    {
        var entry = await _context.SkillEntries
            .FirstOrDefaultAsync(e => e.SkillEntryId == id && e.SchoolId == _currentUser.SchoolId);

        if (entry == null) return NotFound();

        entry.EndorsedByUserId = _currentUser.UserId;
        entry.EndorsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateSkillRequest(string Title, string Category, string? Description, DateTime Date);
