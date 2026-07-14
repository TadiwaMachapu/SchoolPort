using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize(Roles="Admin,Teacher")]. reporting.view covers report viewing/
// generation (incl. AI comment GENERATION — distinct from report.draft = comment submission).
// principal-summary additionally requires reporting.principal_summary (Sensitive), method-level.
[RequirePermission(PermissionKeys.ReportingView)]
public class ReportsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolPortalDbContext _context;
    private readonly ISmartReportsService _smartReports;
    private readonly IScopeService _scope;

    public ReportsController(
        IConfiguration configuration,
        ICurrentUserService currentUser,
        SchoolPortalDbContext context,
        ISmartReportsService smartReports,
        IScopeService scope)
    {
        _configuration = configuration;
        _currentUser = currentUser;
        _context = context;
        _smartReports = smartReports;
        _scope = scope;
    }

    [HttpGet("term-report/{classId}/{termId}")]
    public async Task<IActionResult> GetTermReport(Guid classId, Guid termId)
    {
        if (!await _scope.CanAccessClassAsync(classId)) return NotFound(); // Step 7 IDOR
        var schoolId = _currentUser.SchoolId;

        var term = await _context.Terms
            .AsNoTracking()
            .Where(t => t.TermId == termId && t.SchoolId == schoolId)
            .Include(t => t.AcademicYear)
            .FirstOrDefaultAsync();

        if (term == null) return NotFound("Term not found");

        var cls = await _context.Classes
            .AsNoTracking()
            .Where(c => c.ClassId == classId && c.SchoolId == schoolId)
            .Select(c => new { c.ClassId, c.Name })
            .FirstOrDefaultAsync();

        if (cls == null) return NotFound("Class not found");

        var students = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.IsActive && e.SchoolId == schoolId)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .OrderBy(e => e.Student.User.LastName)
            .Select(e => new { e.Student.StudentId, Name = $"{e.Student.User.FirstName} {e.Student.User.LastName}", e.Student.StudentNumber })
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();

        // #2 fix — read the CAPTURED marks path (Grade.StudentId/AssignmentId, not absent, score set),
        // the same source of truth as the at-risk primitive and every other 1.5.x surface. The old
        // Submission-join path could not see directly-captured marks (Sprint 1.5.2.5 decoupled Grade
        // from Submission → SubmissionId is null for bulk-captured marks), so the Term Report and the
        // at-risk/Matric views disagreed on the same learner's averages. One path now, everywhere.
        var grades = await _context.Grades
            .AsNoTracking()
            .Where(AtRiskMarks.CapturedPredicate(schoolId))
            .Where(g =>
                g.Assignment.ClassSubject.ClassId == classId &&
                g.Assignment.MaxMarks > 0 &&
                g.Assignment.DueAt >= term.StartDate &&
                g.Assignment.DueAt <= term.EndDate)
            .Select(g => new
            {
                g.StudentId,
                SubjectName = g.Assignment.ClassSubject.Subject.Name,
                CapsPhase = g.Assignment.ClassSubject.Subject.CapsPhase,
                Percentage = Math.Round((double)g.Score!.Value / (double)g.Assignment.MaxMarks * 100, 1)
            })
            .ToListAsync();

        var attendanceDays = await _context.Attendances
            .AsNoTracking()
            .Where(a =>
                a.SchoolId == schoolId &&
                a.ClassId == classId &&
                a.Date >= term.StartDate &&
                a.Date <= term.EndDate)
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Absent = g.Count(a => a.Status == AttendanceSignal.Absent)   // Late = attended
            })
            .ToListAsync();

        var attendanceLookup = attendanceDays.ToDictionary(a => a.StudentId);

        var report = students.Select(s =>
        {
            var sGrades = grades.Where(g => g.StudentId == s.StudentId).ToList();
            var bySubject = sGrades
                .GroupBy(g => new { g.SubjectName, g.CapsPhase })
                .Select(g =>
                {
                    var avg = Math.Round(g.Average(x => x.Percentage), 1);
                    // #3 fix: the CAPS achievement level (1–7) is the national scale for ALL phases
                    // (Gr R–12), FET included — not just Senior Phase. Compute it for every subject
                    // with an average (was previously null for FET, rendering "—").
                    var capsLevel = CapsLevelFromPercent(avg);
                    return new
                    {
                        SubjectName = g.Key.SubjectName,
                        CapsPhase = g.Key.CapsPhase,
                        Average = avg,
                        CapsLevel = capsLevel,
                        AssignmentCount = g.Count()
                    };
                })
                .OrderBy(x => x.SubjectName)
                .ToList();

            var overallAvg = bySubject.Count > 0 ? Math.Round(bySubject.Average(x => x.Average), 1) : (double?)null;

            attendanceLookup.TryGetValue(s.StudentId, out var att);
            var attendancePct = att is null ? (double?)null : AttendanceSignal.Percent(att.Total, att.Absent);
            var daysAbsent = att?.Absent ?? 0;

            return new
            {
                s.StudentId,
                s.Name,
                s.StudentNumber,
                SubjectResults = bySubject,
                OverallAverage = overallAvg,
                AttendancePercent = attendancePct,
                DaysAbsent = daysAbsent
            };
        }).ToList();

        return Ok(new
        {
            ClassId = cls.ClassId,
            ClassName = cls.Name,
            TermId = term.TermId,
            TermNumber = term.TermNumber,
            Year = term.AcademicYear.Year,
            StartDate = term.StartDate,
            EndDate = term.EndDate,
            Students = report
        });
    }

    [HttpGet("at-risk")]
    public async Task<IActionResult> GetAtRisk([FromQuery] Guid classId, [FromQuery] Guid termId)
    {
        if (!await _scope.CanAccessClassAsync(classId)) return NotFound(); // Step 7 IDOR
        var schoolId = _currentUser.SchoolId;
        var result = await _smartReports.GetAtRiskStudentsAsync(classId, termId, schoolId);
        return Ok(result);
    }

    [HttpPost("comment")]
    public async Task<IActionResult> GetReportComment(
        [FromQuery] Guid studentId, [FromQuery] Guid termId, [FromQuery] bool forceRefresh = false)
    {
        if (!await _scope.CanAccessStudentAsync(studentId)) return NotFound(); // Step 7 IDOR
        var schoolId = _currentUser.SchoolId;
        var result = await _smartReports.GetReportCommentAsync(studentId, termId, schoolId, forceRefresh);
        if (result == null)
            return Ok(new { available = false, commentText = (string?)null, fromCache = false });
        return Ok(new { available = true, commentText = result.CommentText, fromCache = result.FromCache });
    }

    [HttpPost("principal-summary")]
    [RequirePermission(PermissionKeys.ReportingPrincipalSummary)]
    public async Task<IActionResult> GetPrincipalSummary(
        [FromQuery] Guid classId, [FromQuery] Guid termId, [FromQuery] bool forceRefresh = false)
    {
        if (!await _scope.CanAccessClassAsync(classId)) return NotFound(); // Step 7 IDOR (oversight: always in scope)
        var schoolId = _currentUser.SchoolId;
        var result = await _smartReports.GetPrincipalSummaryAsync(classId, termId, schoolId, forceRefresh);
        if (result == null)
            return Ok(new { available = false, summaryMarkdown = (string?)null, fromCache = false });
        return Ok(new { available = true, summaryMarkdown = result.SummaryMarkdown, fromCache = result.FromCache });
    }

    private static int CapsLevelFromPercent(double pct) => pct switch
    {
        >= 80 => 7,
        >= 70 => 6,
        >= 60 => 5,
        >= 50 => 4,
        >= 40 => 3,
        >= 30 => 2,
        _ => 1
    };

    [HttpGet("attendance-summary")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendanceSummary([FromQuery] Guid? classId, [FromQuery] int? year)
    {
        // Step 7: a specific class must be in scope; non-oversight callers may not pull school-wide.
        if (classId.HasValue && !await _scope.CanAccessClassAsync(classId.Value)) return NotFound();
        if (!classId.HasValue && await _scope.GetAccessibleClassIdsAsync() is not null)
            return Ok(new List<Dictionary<string, object?>>());

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM vw_attendance_summary WHERE school_id = @school_id";
        var parameters = new List<NpgsqlParameter> { new("@school_id", _currentUser.SchoolId) };

        if (classId.HasValue)
        {
            sql += " AND class_id = @class_id";
            parameters.Add(new NpgsqlParameter("@class_id", classId.Value));
        }

        if (year.HasValue)
        {
            sql += " AND year = @year";
            parameters.Add(new NpgsqlParameter("@year", year.Value));
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return Ok(results);
    }

    [HttpGet("gradebook-simple")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradebookSimple([FromQuery] Guid? classId)
    {
        // Step 7: a specific class must be in scope; non-oversight callers may not pull school-wide.
        if (classId.HasValue && !await _scope.CanAccessClassAsync(classId.Value)) return NotFound();
        if (!classId.HasValue && await _scope.GetAccessibleClassIdsAsync() is not null)
            return Ok(new List<Dictionary<string, object?>>());

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM vw_gradebook_simple WHERE school_id = @school_id";
        var parameters = new List<NpgsqlParameter> { new("@school_id", _currentUser.SchoolId) };

        if (classId.HasValue)
        {
            sql += " AND class_id = @class_id";
            parameters.Add(new NpgsqlParameter("@class_id", classId.Value));
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return Ok(results);
    }
}
