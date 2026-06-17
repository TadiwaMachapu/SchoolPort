using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using System.Text;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize(Roles = "Admin")]. All three endpoints are school-wide bulk PII
// exports for SA-SAMS, so they share one permission. system.data_export is Sensitive →
// the handler re-resolves from the DB per request (never trusts cached JWT claims).
[RequirePermission(PermissionKeys.SystemDataExport)]
public class SaSamsController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SaSamsController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // GET /api/sasams/export/learners
    [HttpGet("export/learners")]
    public async Task<IActionResult> ExportLearners()
    {
        var schoolId = _currentUser.SchoolId;

        var learners = await _context.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .Include(s => s.User)
            .Include(s => s.Enrollments.Where(e => e.IsActive))
                .ThenInclude(e => e.Class)
            .OrderBy(s => s.User.LastName)
            .Select(s => new
            {
                s.StudentNumber,
                Surname = s.User.LastName,
                FirstName = s.User.FirstName,
                s.DateOfBirth,
                GradeLevel = s.GradeLevel.HasValue ? s.GradeLevel.Value.ToString() : "",
                ClassName = s.Enrollments.Where(e => e.IsActive).Select(e => e.Class.Name).FirstOrDefault() ?? "",
                s.User.Email
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("LearnerNo,Surname,FirstName,DateOfBirth,Grade,ClassName,Email");
        foreach (var l in learners)
        {
            sb.AppendLine($"{CsvEscape(l.StudentNumber)},{CsvEscape(l.Surname)},{CsvEscape(l.FirstName)}," +
                          $"{(l.DateOfBirth.HasValue ? l.DateOfBirth.Value.ToString("yyyy-MM-dd") : "")}," +
                          $"{CsvEscape(l.GradeLevel)},{CsvEscape(l.ClassName)},{CsvEscape(l.Email)}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"sa-sams-learners-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    // GET /api/sasams/export/attendance?termId=
    [HttpGet("export/attendance")]
    public async Task<IActionResult> ExportAttendance([FromQuery] Guid? termId)
    {
        var schoolId = _currentUser.SchoolId;

        DateTime? start = null, end = null;
        if (termId.HasValue)
        {
            var term = await _context.Terms.AsNoTracking()
                .Where(t => t.TermId == termId.Value && t.SchoolId == schoolId)
                .Select(t => new { t.StartDate, t.EndDate })
                .FirstOrDefaultAsync();
            if (term != null) { start = term.StartDate; end = term.EndDate; }
        }

        var query = _context.Attendances
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId);

        if (start.HasValue) query = query.Where(a => a.Date >= start && a.Date <= end);

        var records = await query
            .Include(a => a.Student).ThenInclude(s => s.User)
            .OrderBy(a => a.Date).ThenBy(a => a.Student.User.LastName)
            .Select(a => new
            {
                a.Student.StudentNumber,
                Surname = a.Student.User.LastName,
                FirstName = a.Student.User.FirstName,
                Date = a.Date.ToString("yyyy-MM-dd"),
                Status = a.Status == 1 ? "P" : a.Status == 2 ? "L" : "A"
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("LearnerNo,Surname,FirstName,Date,Status");
        foreach (var r in records)
            sb.AppendLine($"{CsvEscape(r.StudentNumber)},{CsvEscape(r.Surname)},{CsvEscape(r.FirstName)},{r.Date},{r.Status}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"sa-sams-attendance-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    // GET /api/sasams/export/results?termId=
    [HttpGet("export/results")]
    public async Task<IActionResult> ExportResults([FromQuery] Guid? termId)
    {
        var schoolId = _currentUser.SchoolId;

        DateTime? start = null, end = null;
        if (termId.HasValue)
        {
            var term = await _context.Terms.AsNoTracking()
                .Where(t => t.TermId == termId.Value && t.SchoolId == schoolId)
                .Select(t => new { t.StartDate, t.EndDate })
                .FirstOrDefaultAsync();
            if (term != null) { start = term.StartDate; end = term.EndDate; }
        }

        var gradesQuery = _context.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId);

        if (start.HasValue)
            gradesQuery = gradesQuery.Where(g => g.Submission.Assignment.DueAt >= start && g.Submission.Assignment.DueAt <= end);

        var grades = await gradesQuery
            .Select(g => new
            {
                g.Submission.Student.StudentNumber,
                Surname = g.Submission.Student.User.LastName,
                FirstName = g.Submission.Student.User.FirstName,
                ClassName = g.Submission.Assignment.ClassSubject.Class.Name,
                Subject = g.Submission.Assignment.ClassSubject.Subject.Name,
                Assessment = g.Submission.Assignment.Title,
                MaxMark = g.Submission.Assignment.MaxMarks,
                Mark = g.Score,
                DueDate = g.Submission.Assignment.DueAt.ToString("yyyy-MM-dd")
            })
            .OrderBy(g => g.Surname).ThenBy(g => g.Subject).ThenBy(g => g.DueDate)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("LearnerNo,Surname,FirstName,ClassName,Subject,Assessment,MaxMark,Mark,Percentage,Date");
        foreach (var g in grades)
        {
            var pct = g.MaxMark > 0 ? Math.Round((double)g.Mark / (double)g.MaxMark * 100, 1) : 0;
            sb.AppendLine($"{CsvEscape(g.StudentNumber)},{CsvEscape(g.Surname)},{CsvEscape(g.FirstName)}," +
                          $"{CsvEscape(g.ClassName)},{CsvEscape(g.Subject)},{CsvEscape(g.Assessment)}," +
                          $"{g.MaxMark},{g.Mark},{pct},{g.DueDate}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"sa-sams-results-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
