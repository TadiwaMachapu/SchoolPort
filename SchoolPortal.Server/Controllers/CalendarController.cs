using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Calendar/timetable views → platform.access;
// event create/delete → calendar.manage; timetable management → timetable.manage (SMT only).
public class CalendarController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CalendarController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet("events")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetEvents([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var schoolId = _currentUser.SchoolId;
        var query = _context.CalendarEvents
            .AsNoTracking()
            .Where(e => e.SchoolId == schoolId);

        if (from.HasValue) query = query.Where(e => e.StartAt >= from.Value);
        if (to.HasValue) query = query.Where(e => e.StartAt <= to.Value);

        // Also include assignment due dates as events
        var assignmentEvents = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId &&
                        (!from.HasValue || a.DueAt >= from.Value) &&
                        (!to.HasValue || a.DueAt <= to.Value))
            .Select(a => new
            {
                EventId = a.AssignmentId,
                a.Title,
                Type = "Assignment",
                StartAt = a.DueAt,
                EndAt = (DateTime?)null,
                AllDay = false,
                ClassId = a.ClassSubject.ClassId
            })
            .ToListAsync();

        var calendarEvents = await query
            .Select(e => new
            {
                e.EventId,
                e.Title,
                e.Type,
                e.StartAt,
                e.EndAt,
                e.AllDay,
                e.ClassId
            })
            .ToListAsync();

        return Ok(new { Events = calendarEvents, AssignmentDueDates = assignmentEvents });
    }

    [HttpPost("events")]
    [RequirePermission(PermissionKeys.CalendarManage)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        var ev = new CalendarEvent
        {
            SchoolId = _currentUser.SchoolId,
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            AllDay = request.AllDay,
            ClassId = request.ClassId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.CalendarEvents.Add(ev);
        await _context.SaveChangesAsync();
        return Ok(ev);
    }

    [HttpDelete("events/{id}")]
    [RequirePermission(PermissionKeys.CalendarManage)]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var ev = await _context.CalendarEvents
            .FirstOrDefaultAsync(e => e.EventId == id && e.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Event not found");

        _context.CalendarEvents.Remove(ev);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("timetable")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetTimetable([FromQuery] Guid? classId)
    {
        var query = _context.TimetableSlots
            .AsNoTracking()
            .Where(s => s.ClassSubject.SchoolId == _currentUser.SchoolId);

        if (classId.HasValue)
            query = query.Where(s => s.ClassSubject.ClassId == classId.Value);

        var slots = await query
            .Include(s => s.ClassSubject).ThenInclude(cs => cs.Subject)
            .Include(s => s.ClassSubject).ThenInclude(cs => cs.Class)
            .Include(s => s.ClassSubject).ThenInclude(cs => cs.Teacher).ThenInclude(t => t!.User)
            .Select(s => new
            {
                s.SlotId,
                s.DayOfWeek,
                s.StartTime,
                s.EndTime,
                s.Room,
                Subject = s.ClassSubject.Subject.Name,
                Class = s.ClassSubject.Class.Name,
                Teacher = s.ClassSubject.Teacher != null
                    ? $"{s.ClassSubject.Teacher.User.FirstName} {s.ClassSubject.Teacher.User.LastName}"
                    : null
            })
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync();

        return Ok(slots);
    }

    [HttpPost("timetable")]
    [RequirePermission(PermissionKeys.TimetableManage)]
    public async Task<IActionResult> AddTimetableSlot([FromBody] CreateTimetableSlotRequest request)
    {
        var slot = new TimetableSlot
        {
            SchoolId = _currentUser.SchoolId,
            ClassSubjectId = request.ClassSubjectId,
            DayOfWeek = request.DayOfWeek,
            StartTime = TimeOnly.FromTimeSpan(request.StartTime),
            EndTime = TimeOnly.FromTimeSpan(request.EndTime),
            Room = request.Room
        };

        _context.TimetableSlots.Add(slot);
        await _context.SaveChangesAsync();
        return Ok(slot);
    }
}

public record CreateEventRequest(
    string Title, string Type, DateTime StartAt, string? Description = null,
    DateTime? EndAt = null, bool AllDay = false, Guid? ClassId = null);

public record CreateTimetableSlotRequest(
    Guid ClassSubjectId, int DayOfWeek, TimeSpan StartTime, TimeSpan EndTime, string? Room = null);
