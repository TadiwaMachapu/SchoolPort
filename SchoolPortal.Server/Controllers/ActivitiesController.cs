using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Learner's own activities → platform.access; all
// staff endpoints (list/participants/create/update/delete) → activities.manage (MIC + HOD/
// GradeHead + SMT; CS-5 intentional tightening of the views). MIC→own-activity scope in Step 7.
public class ActivitiesController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IScopeService _scope;

    public ActivitiesController(SchoolPortalDbContext context, ICurrentUserService currentUser, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _scope = scope;
    }

    // GET /api/activities [Admin, Teacher]
    [HttpGet]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> GetAll()
    {
        var schoolId = _currentUser.SchoolId;
        var query = _context.Activities
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId);

        // Step 7: a MIC sees their own + unassigned activities; oversight (marks.view_all) sees all.
        if (!_currentUser.HasPermission(PermissionKeys.MarksViewAll))
            query = query.Where(a => a.OwnerUserId == _currentUser.UserId || a.OwnerUserId == null);

        var activities = await query
            .OrderByDescending(a => a.Date)
            .Select(a => new
            {
                a.ActivityId,
                a.Name,
                a.Description,
                a.ActivityType,
                a.Date,
                a.CreatedAt,
                ParticipantCount = a.Participants.Count
            })
            .ToListAsync();

        return Ok(activities);
    }

    // GET /api/activities/mine [Student]
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

        var activities = await _context.ActivityParticipants
            .AsNoTracking()
            .Where(p => p.StudentId == student.StudentId && p.SchoolId == schoolId)
            .Include(p => p.Activity)
            .OrderByDescending(p => p.Activity.Date)
            .Select(p => new
            {
                p.ActivityParticipantId,
                p.ActivityId,
                p.Activity.Name,
                p.Activity.Description,
                p.Activity.ActivityType,
                p.Activity.Date,
                p.Notes,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(activities);
    }

    // POST /api/activities [Admin]
    [HttpPost]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> Create([FromBody] ActivityRequest request)
    {
        var activity = new Activity
        {
            SchoolId = _currentUser.SchoolId,
            Name = request.Name,
            Description = request.Description,
            ActivityType = request.ActivityType,
            Date = request.Date,
            OwnerUserId = _currentUser.UserId, // Step 7: creator owns the activity (MIC scope)
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        return Ok(new { activity.ActivityId });
    }

    // PUT /api/activities/{id} [Admin]
    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ActivityRequest request)
    {
        if (!await _scope.CanAccessActivityAsync(id)) return Forbid(); // Step 7 IDOR (write)
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.SchoolId == _currentUser.SchoolId);

        if (activity == null) return NotFound();

        activity.Name = request.Name;
        activity.Description = request.Description;
        activity.ActivityType = request.ActivityType;
        activity.Date = request.Date;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/activities/{id} [Admin]
    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await _scope.CanAccessActivityAsync(id)) return Forbid(); // Step 7 IDOR (write)
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.SchoolId == _currentUser.SchoolId);

        if (activity == null) return NotFound();
        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/activities/{id}/participants [Admin, Teacher]
    [HttpGet("{id}/participants")]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> GetParticipants(Guid id)
    {
        if (!await _scope.CanAccessActivityAsync(id)) return NotFound(); // Step 7 IDOR
        var schoolId = _currentUser.SchoolId;
        var participants = await _context.ActivityParticipants
            .AsNoTracking()
            .Where(p => p.ActivityId == id && p.SchoolId == schoolId)
            .Include(p => p.Student).ThenInclude(s => s.User)
            .OrderBy(p => p.Student.User.LastName)
            .Select(p => new
            {
                p.ActivityParticipantId,
                p.StudentId,
                Name = $"{p.Student.User.FirstName} {p.Student.User.LastName}",
                p.Student.StudentNumber,
                p.Notes,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(participants);
    }

    // POST /api/activities/{id}/participants [Admin, Teacher] — accepts userId
    [HttpPost("{id}/participants")]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddParticipantRequest request)
    {
        if (!await _scope.CanAccessActivityAsync(id)) return Forbid(); // Step 7 IDOR (write)
        var schoolId = _currentUser.SchoolId;

        var activity = await _context.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActivityId == id && a.SchoolId == schoolId);
        if (activity == null) return NotFound("Activity not found.");

        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId && s.SchoolId == schoolId)
            .Select(s => new { s.StudentId })
            .FirstOrDefaultAsync();
        if (student == null) return NotFound("Learner not found.");

        var existing = await _context.ActivityParticipants
            .AnyAsync(p => p.ActivityId == id && p.StudentId == student.StudentId);
        if (existing) return Conflict("Learner is already a participant.");

        var participant = new ActivityParticipant
        {
            ActivityId = id,
            StudentId = student.StudentId,
            SchoolId = schoolId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _context.ActivityParticipants.Add(participant);
        await _context.SaveChangesAsync();
        return Ok(new { participant.ActivityParticipantId });
    }

    // DELETE /api/activities/{id}/participants/{participantId} [Admin, Teacher]
    [HttpDelete("{id}/participants/{participantId}")]
    [RequirePermission(PermissionKeys.ActivitiesManage)]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId)
    {
        if (!await _scope.CanAccessActivityAsync(id)) return Forbid(); // Step 7 IDOR (write)
        var p = await _context.ActivityParticipants
            .FirstOrDefaultAsync(x => x.ActivityParticipantId == participantId
                                   && x.ActivityId == id
                                   && x.SchoolId == _currentUser.SchoolId);
        if (p == null) return NotFound();
        _context.ActivityParticipants.Remove(p);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public record ActivityRequest(string Name, string? Description, string ActivityType, DateTime Date);
public record AddParticipantRequest(Guid UserId, string? Notes);
