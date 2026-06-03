using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Parent")]
public class ParentController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPathwaysService _pathways;

    public ParentController(SchoolPortalDbContext context, ICurrentUserService currentUser, IPathwaysService pathways)
    {
        _context = context;
        _currentUser = currentUser;
        _pathways = pathways;
    }

    [HttpGet("children")]
    public async Task<IActionResult> GetChildren()
    {
        var children = await _context.Students
            .AsNoTracking()
            .Where(s => s.ParentUserId == _currentUser.UserId && s.SchoolId == _currentUser.SchoolId)
            .Include(s => s.User)
            .Select(s => new
            {
                s.StudentId,
                s.StudentNumber,
                s.GradeLevel,
                Name = $"{s.User.FirstName} {s.User.LastName}",
                s.User.Email
            })
            .ToListAsync();

        return Ok(children);
    }

    [HttpGet("children/{studentId}/grades")]
    public async Task<IActionResult> GetChildGrades(Guid studentId)
    {
        if (!await IsMyChild(studentId)) return Forbid();

        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.Submission.StudentId == studentId && g.SchoolId == _currentUser.SchoolId)
            .Include(g => g.Submission).ThenInclude(s => s.Assignment).ThenInclude(a => a.ClassSubject).ThenInclude(cs => cs.Subject)
            .Include(g => g.Submission).ThenInclude(s => s.Assignment).ThenInclude(a => a.ClassSubject).ThenInclude(cs => cs.Class)
            .OrderByDescending(g => g.GradedAt)
            .Select(g => new
            {
                g.GradeId,
                g.Score,
                MaxMarks = g.Submission.Assignment.MaxMarks,
                Percentage = Math.Round(g.Score / g.Submission.Assignment.MaxMarks * 100, 1),
                AssignmentTitle = g.Submission.Assignment.Title,
                Subject = g.Submission.Assignment.ClassSubject.Subject.Name,
                Class = g.Submission.Assignment.ClassSubject.Class.Name,
                g.Feedback,
                g.GradedAt
            })
            .ToListAsync();

        return Ok(grades);
    }

    [HttpGet("children/{studentId}/attendance")]
    public async Task<IActionResult> GetChildAttendance(Guid studentId, [FromQuery] int? month, [FromQuery] int? year)
    {
        if (!await IsMyChild(studentId)) return Forbid();

        var query = _context.Attendances
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.SchoolId == _currentUser.SchoolId);

        if (month.HasValue && year.HasValue)
        {
            query = query.Where(a => a.Date.Month == month.Value && a.Date.Year == year.Value);
        }

        var records = await query
            .Include(a => a.Class)
            .OrderByDescending(a => a.Date)
            .Select(a => new
            {
                a.AttendanceId,
                a.Date,
                a.Status,
                StatusText = a.Status == 1 ? "Present" : a.Status == 0 ? "Absent" : "Late",
                ClassName = a.Class.Name,
                a.Notes
            })
            .ToListAsync();

        var summary = new
        {
            Total = records.Count,
            Present = records.Count(r => r.Status == 1),
            Absent = records.Count(r => r.Status == 0),
            Late = records.Count(r => r.Status == 2),
            AttendanceRate = records.Count > 0 ? Math.Round((double)records.Count(r => r.Status == 1) / records.Count * 100, 1) : 0d,
            Records = records
        };

        return Ok(summary);
    }

    [HttpGet("children/{studentId}/assignments")]
    public async Task<IActionResult> GetChildAssignments(Guid studentId)
    {
        if (!await IsMyChild(studentId)) return Forbid();

        var enrolledClassSubjectIds = await _context.Enrollments
            .Where(e => e.StudentId == studentId && e.IsActive)
            .SelectMany(e => _context.ClassSubjects.Where(cs => cs.ClassId == e.ClassId).Select(cs => cs.ClassSubjectId))
            .ToListAsync();

        var assignments = await _context.Assignments
            .AsNoTracking()
            .Where(a => enrolledClassSubjectIds.Contains(a.ClassSubjectId) && a.SchoolId == _currentUser.SchoolId)
            .Include(a => a.ClassSubject).ThenInclude(cs => cs.Subject)
            .Include(a => a.ClassSubject).ThenInclude(cs => cs.Class)
            .Include(a => a.Submissions.Where(s => s.StudentId == studentId))
            .OrderBy(a => a.DueAt)
            .Select(a => new
            {
                a.AssignmentId,
                a.Title,
                a.DueAt,
                a.MaxMarks,
                Subject = a.ClassSubject.Subject.Name,
                Class = a.ClassSubject.Class.Name,
                IsSubmitted = a.Submissions.Any(s => s.StudentId == studentId),
                IsOverdue = a.DueAt < DateTime.UtcNow && !a.Submissions.Any(s => s.StudentId == studentId)
            })
            .ToListAsync();

        return Ok(assignments);
    }

    [HttpGet("children/{studentId}/announcements")]
    public async Task<IActionResult> GetSchoolAnnouncements(Guid studentId)
    {
        if (!await IsMyChild(studentId)) return Forbid();

        var announcements = await _context.Announcements
            .AsNoTracking()
            .Where(a => a.SchoolId == _currentUser.SchoolId && a.IsActive)
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .Select(a => new
            {
                a.AnnouncementId,
                a.Title,
                a.Content,
                a.Audience,
                CreatedBy = $"{a.CreatedByUser.FirstName} {a.CreatedByUser.LastName}",
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(announcements);
    }

    [HttpGet("pathways")]
    public async Task<IActionResult> GetPathways()
    {
        var result = await _pathways.GetParentPathwaysAsync(_currentUser.UserId, _currentUser.SchoolId);
        return Ok(result);
    }

    private async Task<bool> IsMyChild(Guid studentId) =>
        await _context.Students.AnyAsync(s =>
            s.StudentId == studentId &&
            s.ParentUserId == _currentUser.UserId &&
            s.SchoolId == _currentUser.SchoolId);
}
