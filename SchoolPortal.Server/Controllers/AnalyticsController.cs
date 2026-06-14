using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize(Roles="Admin,Teacher")]. All endpoints are school-wide analytics
// dashboards (named at-risk lists, all-class performance) → one oversight permission.
// Intentional tightening: rank-and-file teachers no longer have school-wide analytics.
// analytics.view_school is Sensitive → handler re-resolves from the DB per request.
[RequirePermission(PermissionKeys.AnalyticsViewSchool)]
public class AnalyticsController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AnalyticsController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var schoolId = _currentUser.SchoolId;

        var totalStudents = await _context.Students.CountAsync(s => s.SchoolId == schoolId);
        var totalTeachers = await _context.Teachers.CountAsync(t => t.SchoolId == schoolId);
        var totalClasses = await _context.Classes.CountAsync(c => c.SchoolId == schoolId);
        var totalCourses = await _context.Courses.CountAsync(c => c.SchoolId == schoolId && c.IsPublished);
        var totalAssignments = await _context.Assignments.CountAsync(a => a.SchoolId == schoolId);
        var pendingSubmissions = await _context.Submissions
            .CountAsync(s => s.SchoolId == schoolId && s.Grade == null);

        var attendanceThisMonth = await _context.Attendances
            .Where(a => a.SchoolId == schoolId && a.Date.Month == DateTime.UtcNow.Month && a.Date.Year == DateTime.UtcNow.Year)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Present = g.Count(a => a.Status == 1)
            })
            .FirstOrDefaultAsync();

        var attendanceRate = attendanceThisMonth?.Total > 0
            ? Math.Round((double)attendanceThisMonth.Present / attendanceThisMonth.Total * 100, 1)
            : 0d;

        return Ok(new
        {
            TotalStudents = totalStudents,
            TotalTeachers = totalTeachers,
            TotalClasses = totalClasses,
            TotalCourses = totalCourses,
            TotalAssignments = totalAssignments,
            PendingSubmissions = pendingSubmissions,
            AttendanceRateThisMonth = attendanceRate
        });
    }

    [HttpGet("grade-distribution")]
    public async Task<IActionResult> GetGradeDistribution([FromQuery] Guid? classId)
    {
        var query = _context.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == _currentUser.SchoolId);

        if (classId.HasValue)
        {
            query = query.Where(g =>
                g.Submission.Assignment.ClassSubject.ClassId == classId.Value);
        }

        var grades = await query
            .Include(g => g.Submission).ThenInclude(s => s.Assignment)
            .Select(g => new { Percentage = (double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100 })
            .ToListAsync();

        var distribution = new
        {
            APlus = grades.Count(g => g.Percentage >= 90),
            A = grades.Count(g => g.Percentage >= 80 && g.Percentage < 90),
            B = grades.Count(g => g.Percentage >= 70 && g.Percentage < 80),
            C = grades.Count(g => g.Percentage >= 60 && g.Percentage < 70),
            D = grades.Count(g => g.Percentage >= 50 && g.Percentage < 60),
            F = grades.Count(g => g.Percentage < 50),
            Average = grades.Count > 0 ? Math.Round(grades.Average(g => g.Percentage), 1) : 0d,
            Total = grades.Count
        };

        return Ok(distribution);
    }

    [HttpGet("attendance-trend")]
    public async Task<IActionResult> GetAttendanceTrend([FromQuery] int weeks = 8)
    {
        var startDate = DateTime.UtcNow.AddDays(-weeks * 7);

        var records = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.SchoolId == _currentUser.SchoolId && a.Date >= startDate)
            .GroupBy(a => new { a.Date.Year, a.Date.Month, Week = (a.Date.DayOfYear - 1) / 7 })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Week,
                Total = g.Count(),
                Present = g.Count(a => a.Status == 1),
                Rate = Math.Round((double)g.Count(a => a.Status == 1) / g.Count() * 100, 1)
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Week)
            .ToListAsync();

        return Ok(records);
    }

    [HttpGet("at-risk-students")]
    public async Task<IActionResult> GetAtRiskStudents()
    {
        var schoolId = _currentUser.SchoolId;
        var cutoff = DateTime.UtcNow.AddDays(-30);

        // Students with attendance rate < 75% in last 30 days
        var lowAttendance = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId && a.Date >= cutoff)
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                AttendanceRate = Math.Round((double)g.Count(a => a.Status == 1) / g.Count() * 100, 1)
            })
            .Where(x => x.AttendanceRate < 75)
            .ToListAsync();

        var studentIds = lowAttendance.Select(x => x.StudentId).ToList();

        var students = await _context.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.StudentId) && s.SchoolId == schoolId)
            .Include(s => s.User)
            .Select(s => new { s.StudentId, Name = $"{s.User.FirstName} {s.User.LastName}", s.StudentNumber })
            .ToListAsync();

        var result = students.Select(s => new
        {
            s.StudentId,
            s.Name,
            s.StudentNumber,
            AttendanceRate = lowAttendance.First(x => x.StudentId == s.StudentId).AttendanceRate,
            Risk = "High"
        }).ToList();

        return Ok(result);
    }

    [HttpGet("class-performance")]
    public async Task<IActionResult> GetClassPerformance()
    {
        var schoolId = _currentUser.SchoolId;

        var performance = await _context.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == schoolId)
            .Select(c => new
            {
                c.ClassId,
                c.Name,
                StudentCount = c.Enrollments.Count(e => e.IsActive),
                AverageGrade = c.ClassSubjects
                    .SelectMany(cs => cs.Assignments)
                    .SelectMany(a => a.Submissions)
                    .Where(s => s.Grade != null)
                    .Select(s => (double?)((double)s.Grade!.Score / (double)s.Assignment.MaxMarks * 100))
                    .Average()  // double? — returns null for empty sets, never throws
            })
            .ToListAsync();

        return Ok(performance);
    }

    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity()
    {
        var schoolId = _currentUser.SchoolId;
        var since = DateTime.UtcNow.AddDays(-7);

        var submissions = await _context.Submissions
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && s.SubmittedAt >= since)
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Assignment)
            .OrderByDescending(s => s.SubmittedAt)
            .Take(10)
            .Select(s => new
            {
                Type = "submission",
                Description = $"{s.Student.User.FirstName} submitted '{s.Assignment.Title}'",
                Timestamp = s.SubmittedAt
            })
            .ToListAsync();

        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId && g.GradedAt >= since)
            .Include(g => g.Submission).ThenInclude(s => s.Student).ThenInclude(st => st.User)
            .Include(g => g.Submission).ThenInclude(s => s.Assignment)
            .OrderByDescending(g => g.GradedAt)
            .Take(10)
            .Select(g => new
            {
                Type = "grade",
                Description = $"Grade posted for '{g.Submission.Assignment.Title}'",
                Timestamp = (DateTime?)g.GradedAt
            })
            .ToListAsync();

        var activity = submissions.Cast<object>().Concat(grades.Cast<object>())
            .OrderByDescending(x => x.GetType().GetProperty("Timestamp")?.GetValue(x))
            .Take(15);

        return Ok(activity);
    }
}
