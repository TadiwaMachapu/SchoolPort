using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

public interface ISmartReportsService
{
    Task<List<AtRiskStudentDto>> GetAtRiskStudentsAsync(Guid classId, Guid termId, Guid schoolId);
    Task<ReportCommentDto?> GetReportCommentAsync(
        Guid studentId, Guid termId, Guid schoolId, bool forceRefresh = false);
    Task<PrincipalSummaryDto?> GetPrincipalSummaryAsync(
        Guid classId, Guid termId, Guid schoolId, bool forceRefresh = false);
}

// Sprint 1.5.3 — at-risk shape now carries the SHARED primitive's judgment (InterventionBand +
// per-subject SubjectRiskDto with risk/trend). RiskFlags is SmartReports' own layer on top —
// today just attendance (LowAttendance). No more locally-computed marks flags.
public record AtRiskStudentDto(
    Guid StudentId,
    string Name,
    string StudentNumber,
    double? OverallAverage,
    double? AttendancePercent,
    string? InterventionBand,
    List<string> RiskFlags,
    List<SubjectRiskDto> SubjectResults
);

public record ReportCommentDto(string CommentText, bool FromCache);
public record PrincipalSummaryDto(string SummaryMarkdown, bool FromCache);

public class SmartReportsService : ISmartReportsService
{
    // Gemini free tier — usage logged at cost 0. No cost cap: the 7-day fingerprint cache is the
    // throttle (a given student/term report regenerates at most once a week). Sprint 1.5.3.
    private const int CacheTtlDays = 7;

    private readonly SchoolPortalDbContext _context;
    private readonly ILogger<SmartReportsService> _logger;
    private readonly IGeminiService _gemini;
    private readonly IAtRiskService _atRisk;

    public SmartReportsService(
        SchoolPortalDbContext context,
        ILogger<SmartReportsService> logger,
        IGeminiService gemini,
        IAtRiskService atRisk)
    {
        _context = context;
        _logger = logger;
        _gemini = gemini;
        _atRisk = atRisk;
    }

    // ── At-Risk ────────────────────────────────────────────────────────────────

    public async Task<List<AtRiskStudentDto>> GetAtRiskStudentsAsync(
        Guid classId, Guid termId, Guid schoolId)
    {
        var term = await _context.Terms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TermId == termId && t.SchoolId == schoolId);
        if (term == null) return [];

        // At-risk JUDGMENT comes from the ONE shared primitive — the same computation the Matric
        // dashboard uses, so the two can never disagree (AtRisk_BothSurfaces_AgreeForSameLearner).
        var risk = await _atRisk.EvaluateAsync(schoolId, new[] { classId }, termId);

        var students = await _context.Enrollments.AsNoTracking()
            .Where(e => e.ClassId == classId && e.IsActive && e.SchoolId == schoolId)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Select(e => new
            {
                e.Student.StudentId,
                Name = $"{e.Student.User.FirstName} {e.Student.User.LastName}",
                e.Student.StudentNumber,
            })
            .ToListAsync();
        if (students.Count == 0) return [];

        // Attendance is SmartReports' OWN signal, layered on top of the shared marks band. Absent
        // count only (Late = attended); AttendanceSignal nulls out a too-thin sample so a school
        // that hasn't captured attendance never false-flags LowAttendance.
        var attendanceLookup = (await _context.Attendances.AsNoTracking()
                .Where(a => a.SchoolId == schoolId && a.ClassId == classId &&
                            a.Date >= term.StartDate && a.Date <= term.EndDate)
                .GroupBy(a => a.StudentId)
                .Select(g => new { StudentId = g.Key, Total = g.Count(), Absent = g.Count(a => a.Status == AttendanceSignal.Absent) })
                .ToListAsync())
            .ToDictionary(a => a.StudentId);

        var result = new List<AtRiskStudentDto>();
        foreach (var s in students)
        {
            risk.TryGetValue(s.StudentId, out var r);
            var band = r?.InterventionBand;
            var subjects = r?.Subjects ?? new List<SubjectRiskDto>();

            attendanceLookup.TryGetValue(s.StudentId, out var att);
            var attPct = att is null ? (double?)null : AttendanceSignal.Percent(att.Total, att.Absent);

            var flags = new List<string>();
            if (attPct is < 80) flags.Add("LowAttendance");

            // "At risk" iff the shared band flags them OR attendance is low.
            if (band != null || flags.Count > 0)
                result.Add(new AtRiskStudentDto(
                    s.StudentId, s.Name, s.StudentNumber,
                    r?.OverallAverage, attPct, band, flags, subjects));
        }

        return result.OrderBy(s => s.Name).ToList();
    }

    // ── AI Report Comment ──────────────────────────────────────────────────────

    public async Task<ReportCommentDto?> GetReportCommentAsync(
        Guid studentId, Guid termId, Guid schoolId, bool forceRefresh = false)
    {
        var term = await _context.Terms
            .AsNoTracking()
            .Include(t => t.AcademicYear)
            .FirstOrDefaultAsync(t => t.TermId == termId && t.SchoolId == schoolId);

        if (term == null) return null;

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.SchoolId == schoolId);

        if (student == null) return null;

        // Fetch grades within the term
        // Captured marks (shared predicate) on the Grade path — sees 1.5.2.5 capture marks.
        var grades = await _context.Grades
            .AsNoTracking()
            .Where(AtRiskMarks.CapturedPredicate(schoolId))
            .Where(g =>
                g.StudentId == studentId &&
                g.Assignment.DueAt >= term.StartDate &&
                g.Assignment.DueAt <= term.EndDate)
            .Select(g => new
            {
                SubjectName = g.Assignment.ClassSubject.Subject.Name,
                Percentage = Math.Round((double)g.Score!.Value / (double)g.Assignment.MaxMarks * 100, 1)
            })
            .ToListAsync();

        var bySubject = grades
            .GroupBy(g => g.SubjectName)
            .Select(g => new { Subject = g.Key, Avg = Math.Round(g.Average(x => x.Percentage), 1) })
            .OrderBy(x => x.Subject)
            .ToList();

        var overallAvg = bySubject.Count > 0 ? bySubject.Average(x => x.Avg) : (double?)null;

        // Fetch attendance within term
        var att = await _context.Attendances
            .AsNoTracking()
            .Where(a =>
                a.SchoolId == schoolId &&
                a.StudentId == studentId &&
                a.Date >= term.StartDate &&
                a.Date <= term.EndDate)
            .GroupBy(a => a.StudentId)
            .Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == 1) })
            .FirstOrDefaultAsync();

        var attPct = att is { Total: > 0 }
            ? Math.Round((double)att.Present / att.Total * 100, 1)
            : (double?)null;

        var subjectCsv = string.Join(",", bySubject.Select(x => $"{x.Subject}:{x.Avg}"));
        var fingerprint = BuildFingerprint($"{studentId}:{termId}:{subjectCsv}");

        if (!forceRefresh)
        {
            var cached = await _context.ReportCommentCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.StudentId == studentId &&
                    c.TermId == termId &&
                    c.InputFingerprint == fingerprint &&
                    c.ExpiresAt > DateTime.UtcNow);

            if (cached != null)
                return new ReportCommentDto(cached.CommentText, FromCache: true);
        }

        var prompt = BuildCommentPrompt(
            student.User.FirstName, term.TermNumber, term.AcademicYear.Year,
            bySubject.Select(x => (x.Subject, x.Avg)).ToList(),
            overallAvg, attPct);

        // Token counts don't flow through IGeminiService; free tier — logged at 0/0 and cost 0.
        const int inputTokens = 0, outputTokens = 0;
        const decimal costZar = 0m;

        string? commentText;
        try
        {
            commentText = await CallGeminiAsync(prompt, "comment");
        }
        catch (GeminiNotConfiguredException)
        {
            _logger.LogWarning("Gemini:ApiKey not configured — report comment unavailable");
            return null;
        }
        catch (GeminiException ex)
        {
            await LogUsageAsync(schoolId, studentId, "ReportComment", inputTokens, outputTokens, costZar, false, ex.Message);
            return null;
        }

        if (commentText == null)
        {
            await LogUsageAsync(schoolId, studentId, "ReportComment", inputTokens, outputTokens, costZar, false, "Gemini returned no text");
            return null;
        }

        await LogUsageAsync(schoolId, studentId, "ReportComment", inputTokens, outputTokens, costZar, true, null);

        var stale = await _context.ReportCommentCaches
            .Where(c => c.StudentId == studentId && c.TermId == termId)
            .ToListAsync();
        if (stale.Count > 0) _context.ReportCommentCaches.RemoveRange(stale);

        _context.ReportCommentCaches.Add(new ReportCommentCache
        {
            ReportCommentCacheId = Guid.NewGuid(),
            StudentId = studentId,
            TermId = termId,
            InputFingerprint = fingerprint,
            CommentText = commentText,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(CacheTtlDays),
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        await _context.SaveChangesAsync();

        return new ReportCommentDto(commentText, FromCache: false);
    }

    // ── AI Principal Summary ───────────────────────────────────────────────────

    public async Task<PrincipalSummaryDto?> GetPrincipalSummaryAsync(
        Guid classId, Guid termId, Guid schoolId, bool forceRefresh = false)
    {
        var term = await _context.Terms
            .AsNoTracking()
            .Include(t => t.AcademicYear)
            .FirstOrDefaultAsync(t => t.TermId == termId && t.SchoolId == schoolId);

        if (term == null) return null;

        var cls = await _context.Classes
            .AsNoTracking()
            .Where(c => c.ClassId == classId && c.SchoolId == schoolId)
            .Select(c => new { c.ClassId, c.Name })
            .FirstOrDefaultAsync();

        if (cls == null) return null;

        var atRisk = await GetAtRiskStudentsAsync(classId, termId, schoolId);

        var totalStudents = await _context.Enrollments
            .Where(e => e.ClassId == classId && e.IsActive && e.SchoolId == schoolId)
            .CountAsync();

        // Captured marks (shared predicate) on the Grade path — sees 1.5.2.5 capture marks.
        var grades = await _context.Grades
            .AsNoTracking()
            .Where(AtRiskMarks.CapturedPredicate(schoolId))
            .Where(g =>
                g.Assignment.ClassSubject.ClassId == classId &&
                g.Assignment.DueAt >= term.StartDate &&
                g.Assignment.DueAt <= term.EndDate)
            .Select(g => new
            {
                SubjectName = g.Assignment.ClassSubject.Subject.Name,
                Percentage = Math.Round((double)g.Score!.Value / (double)g.Assignment.MaxMarks * 100, 1)
            })
            .ToListAsync();

        var subjectAvgs = grades
            .GroupBy(g => g.SubjectName)
            .Select(g => new { Subject = g.Key, Avg = Math.Round(g.Average(x => x.Percentage), 1) })
            .OrderBy(x => x.Subject)
            .ToList();

        var classAvg = subjectAvgs.Count > 0 ? Math.Round(subjectAvgs.Average(x => x.Avg), 1) : (double?)null;

        var attData = await _context.Attendances
            .AsNoTracking()
            .Where(a =>
                a.SchoolId == schoolId &&
                a.ClassId == classId &&
                a.Date >= term.StartDate &&
                a.Date <= term.EndDate)
            .GroupBy(a => a.StudentId)
            .Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == 1) })
            .ToListAsync();

        var avgAttPct = attData.Count > 0
            ? Math.Round(attData.Average(a => a.Total > 0 ? (double)a.Present / a.Total * 100 : 0), 1)
            : (double?)null;

        var subjectCsv = string.Join(",", subjectAvgs.Select(x => $"{x.Subject}:{x.Avg}"));
        var fingerprintKey = $"{classId}:{termId}:{totalStudents}:{classAvg:F1}:{atRisk.Count}:{subjectCsv}";
        var fingerprint = BuildFingerprint(fingerprintKey);

        if (!forceRefresh)
        {
            var cached = await _context.PrincipalSummaryCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.ClassId == classId &&
                    c.TermId == termId &&
                    c.InputFingerprint == fingerprint &&
                    c.ExpiresAt > DateTime.UtcNow);

            if (cached != null)
                return new PrincipalSummaryDto(cached.SummaryMarkdown, FromCache: true);
        }

        var prompt = BuildSummaryPrompt(
            cls.Name, term.TermNumber, term.AcademicYear.Year,
            totalStudents, atRisk.Count,
            subjectAvgs.Select(x => (x.Subject, x.Avg)).ToList(),
            classAvg, avgAttPct);

        // Token counts don't flow through IGeminiService; free tier — logged at 0/0 and cost 0.
        const int inputTokens = 0, outputTokens = 0;
        const decimal costZar = 0m;

        string? summaryMarkdown;
        try
        {
            summaryMarkdown = await CallGeminiAsync(prompt, "summary");
        }
        catch (GeminiNotConfiguredException)
        {
            _logger.LogWarning("Gemini:ApiKey not configured — principal summary unavailable");
            return null;
        }
        catch (GeminiException ex)
        {
            await LogUsageAsync(schoolId, null, "PrincipalSummary", inputTokens, outputTokens, costZar, false, ex.Message);
            return null;
        }

        if (summaryMarkdown == null)
        {
            await LogUsageAsync(schoolId, null, "PrincipalSummary", inputTokens, outputTokens, costZar, false, "Gemini returned no text");
            return null;
        }

        await LogUsageAsync(schoolId, null, "PrincipalSummary", inputTokens, outputTokens, costZar, true, null);

        var stale = await _context.PrincipalSummaryCaches
            .Where(c => c.ClassId == classId && c.TermId == termId)
            .ToListAsync();
        if (stale.Count > 0) _context.PrincipalSummaryCaches.RemoveRange(stale);

        _context.PrincipalSummaryCaches.Add(new PrincipalSummaryCache
        {
            PrincipalSummaryCacheId = Guid.NewGuid(),
            ClassId = classId,
            TermId = termId,
            InputFingerprint = fingerprint,
            SummaryMarkdown = summaryMarkdown,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(CacheTtlDays),
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        await _context.SaveChangesAsync();

        return new PrincipalSummaryDto(summaryMarkdown, FromCache: false);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string BuildFingerprint(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildCommentPrompt(
        string firstName, int termNumber, int year,
        List<(string Subject, double Avg)> subjects,
        double? overallAvg, double? attPct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a South African school teacher writing a professional term report comment.");
        sb.AppendLine("Write a concise, encouraging comment (2–3 sentences) for this learner's report card.");
        sb.AppendLine("Use South African English. Mention specific strengths and one area to improve.");
        sb.AppendLine("Use the learner's first name only. Be warm but professional.");
        sb.AppendLine();
        sb.AppendLine($"LEARNER FIRST NAME: {firstName}");
        sb.AppendLine($"TERM: Term {termNumber} {year}");
        if (overallAvg.HasValue) sb.AppendLine($"OVERALL AVERAGE: {overallAvg:F1}%");
        if (attPct.HasValue) sb.AppendLine($"ATTENDANCE: {attPct:F1}%");
        sb.AppendLine("SUBJECT AVERAGES:");
        foreach (var (subject, avg) in subjects)
            sb.AppendLine($"  - {subject}: {avg:F1}%");
        sb.AppendLine();
        sb.AppendLine("""Respond with ONLY a JSON object: {"comment": "your comment text here"}""");
        return sb.ToString();
    }

    private static string BuildSummaryPrompt(
        string className, int termNumber, int year,
        int totalStudents, int atRiskCount,
        List<(string Subject, double Avg)> subjectAvgs,
        double? classAvg, double? avgAttPct)
    {
        var atRiskPct = totalStudents > 0 ? Math.Round((double)atRiskCount / totalStudents * 100, 0) : 0;
        var sb = new StringBuilder();
        sb.AppendLine("You are writing an executive academic summary for a South African school principal.");
        sb.AppendLine("Provide a concise summary (3–4 sentences) of the class performance for the term.");
        sb.AppendLine("Use South African English. Highlight key achievements and areas of concern.");
        sb.AppendLine("Use markdown formatting where helpful (**bold** for key metrics).");
        sb.AppendLine();
        sb.AppendLine($"CLASS: {className}");
        sb.AppendLine($"TERM: Term {termNumber} {year}");
        sb.AppendLine($"TOTAL LEARNERS: {totalStudents}");
        sb.AppendLine($"AT-RISK LEARNERS: {atRiskCount} ({atRiskPct}%)");
        if (classAvg.HasValue) sb.AppendLine($"CLASS AVERAGE: {classAvg:F1}%");
        if (avgAttPct.HasValue) sb.AppendLine($"AVERAGE ATTENDANCE: {avgAttPct:F1}%");
        sb.AppendLine("SUBJECT CLASS AVERAGES:");
        foreach (var (subject, avg) in subjectAvgs)
            sb.AppendLine($"  - {subject}: {avg:F1}%");
        sb.AppendLine();
        sb.AppendLine("""Respond with ONLY a JSON object: {"summary": "your markdown summary here"}""");
        return sb.ToString();
    }

    // Gemini returns free text; the prompt asks for a JSON object with a named key, so extract the
    // JSON block and read that key exactly as before (prompts are unchanged from the Anthropic
    // version). Propagates GeminiNotConfiguredException / GeminiException to the call sites.
    private async Task<string?> CallGeminiAsync(string prompt, string jsonKey)
    {
        var json = GeminiJson.ExtractObject(
            await _gemini.GenerateAsync(GeminiService.StructuredSystemPrompt, prompt));
        if (json == null) return null;

        try
        {
            using var parsed = JsonDocument.Parse(json);
            return parsed.RootElement.GetProperty(jsonKey).GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini smart-reports JSON");
            return null;
        }
    }

    private async Task LogUsageAsync(
        Guid schoolId, Guid? studentId, string feature,
        int inputTokens, int outputTokens, decimal costZar,
        bool success, string? error)
    {
        _context.AiUsageLogs.Add(new AiUsageLog
        {
            AiUsageLogId = Guid.NewGuid(),
            SchoolId = schoolId,
            Feature = feature,
            StudentId = studentId,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostZar = costZar,
            CreatedAt = DateTime.UtcNow,
            Success = success,
            ErrorMessage = error
        });

        await _context.SaveChangesAsync();
    }
}
