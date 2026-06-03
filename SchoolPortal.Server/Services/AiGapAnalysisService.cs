using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

public interface IAiGapAnalysisService
{
    Task<GapAnalysisResultDto?> GetGapAnalysisAsync(
        Guid studentId, Guid schoolId, Guid universityCourseId, bool forceRefresh = false);
}

public record GapAnalysisSubjectGapDto(
    string Subject,
    double? CurrentPercent,
    int RequiredPercent,
    string Advice
);

public record GapAnalysisResultDto(
    string Summary,
    int CurrentAps,
    int RequiredAps,
    int ApsGap,
    List<GapAnalysisSubjectGapDto> SubjectGaps,
    string OverallAdvice,
    List<string> StudySuggestions,
    bool FromCache
);

public class AiGapAnalysisService : IAiGapAnalysisService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-6";
    private const decimal InputCostPerToken = 3m / 1_000_000m;
    private const decimal OutputCostPerToken = 15m / 1_000_000m;
    private const decimal UsdToZarRate = 18.5m;
    private const int CacheTtlDays = 7;

    private readonly SchoolPortalDbContext _context;
    private readonly IPathwaysService _pathways;
    private readonly IConfiguration _config;
    private readonly ILogger<AiGapAnalysisService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiGapAnalysisService(
        SchoolPortalDbContext context,
        IPathwaysService pathways,
        IConfiguration config,
        ILogger<AiGapAnalysisService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _pathways = pathways;
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GapAnalysisResultDto?> GetGapAnalysisAsync(
        Guid studentId, Guid schoolId, Guid universityCourseId, bool forceRefresh = false)
    {
        var course = await _context.UniversityCourses
            .AsNoTracking()
            .Include(c => c.University)
            .Include(c => c.SubjectRequirements)
            .FirstOrDefaultAsync(c => c.UniversityCourseId == universityCourseId);

        if (course == null) return null;

        var aps = await _pathways.GetLearnerApsAsync(studentId, schoolId);
        var fingerprint = BuildFingerprint(aps, course);

        if (!forceRefresh)
        {
            var cached = await _context.AiGapAnalysisCaches
                .AsNoTracking()
                .Where(c =>
                    c.StudentId == studentId &&
                    c.UniversityCourseId == universityCourseId &&
                    c.InputFingerprint == fingerprint &&
                    c.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<GapAnalysisResultDto>(cached.ResultJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cachedResult != null)
                    return cachedResult with { FromCache = true };
            }
        }

        // Check API key
        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Anthropic:ApiKey not configured — gap analysis unavailable");
            return null;
        }

        // Check monthly cost cap for this school
        var school = await _context.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.SchoolId == schoolId);
        if (school != null)
        {
            var cap = school.Settings.AiMonthlyCostCapZar;
            if (cap > 0)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var spentThisMonth = await _context.AiUsageLogs
                    .Where(l => l.SchoolId == schoolId && l.CreatedAt >= monthStart && l.Success)
                    .SumAsync(l => (decimal?)l.EstimatedCostZar) ?? 0m;

                if (spentThisMonth >= cap)
                {
                    _logger.LogInformation("AI cost cap reached for school {SchoolId}: R{Spent} >= R{Cap}", schoolId, spentThisMonth, cap);
                    return null;
                }
            }
        }

        var prompt = BuildPrompt(aps, course);

        var (resultJson, inputTokens, outputTokens) = await CallClaudeAsync(apiKey, prompt);

        var costZar = (inputTokens * InputCostPerToken + outputTokens * OutputCostPerToken) * UsdToZarRate;

        if (resultJson == null)
        {
            await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, false, "Claude API call failed");
            return null;
        }

        GapAnalysisResultDto? result;
        try
        {
            result = ParseResult(resultJson, aps.StandardAps, course.MinimumAps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse gap analysis response");
            await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, false, "Parse failed: " + ex.Message);
            return null;
        }

        await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, true, null);

        // Persist to cache (clear any stale cache for this learner/course first)
        var staleEntries = await _context.AiGapAnalysisCaches
            .Where(c => c.StudentId == studentId && c.UniversityCourseId == universityCourseId)
            .ToListAsync();
        if (staleEntries.Any()) _context.AiGapAnalysisCaches.RemoveRange(staleEntries);

        _context.AiGapAnalysisCaches.Add(new AiGapAnalysisCache
        {
            AiGapAnalysisCacheId = Guid.NewGuid(),
            StudentId = studentId,
            UniversityCourseId = universityCourseId,
            InputFingerprint = fingerprint,
            ResultJson = JsonSerializer.Serialize(result),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(CacheTtlDays),
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        await _context.SaveChangesAsync();

        return result;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string BuildFingerprint(LearnerApsResult aps, UniversityCourse course)
    {
        var key = new StringBuilder();
        key.Append(course.UniversityCourseId);
        key.Append(':');
        key.Append(aps.StandardAps);
        foreach (var s in aps.SubjectScores.OrderBy(x => x.SubjectName))
            key.Append($"|{s.SubjectName}={s.AveragePercent?.ToString("F1") ?? "null"}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildPrompt(LearnerApsResult aps, UniversityCourse course)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert South African university admissions advisor.");
        sb.AppendLine("A Grade 12 learner wants to qualify for a specific university course.");
        sb.AppendLine("Analyse their current marks and provide structured gap analysis advice.");
        sb.AppendLine();
        sb.AppendLine($"TARGET COURSE: {course.Name} at {course.University.Name} ({course.University.Abbreviation})");
        if (course.Faculty != null) sb.AppendLine($"Faculty: {course.Faculty}");
        sb.AppendLine($"Minimum APS required: {course.MinimumAps}");
        if (course.ApsNotes != null) sb.AppendLine($"Notes: {course.ApsNotes}");
        sb.AppendLine();
        sb.AppendLine("SUBJECT REQUIREMENTS:");
        foreach (var req in course.SubjectRequirements.Where(r => r.IsRequired))
        {
            sb.AppendLine($"  - {req.SubjectName}: minimum {req.MinimumPercent ?? 0}%{(req.Notes != null ? $" ({req.Notes})" : "")}");
        }
        sb.AppendLine();
        sb.AppendLine($"LEARNER'S CURRENT MARKS (APS = {aps.StandardAps}):");
        foreach (var s in aps.SubjectScores.Where(x => x.AveragePercent.HasValue).OrderByDescending(x => x.AveragePercent))
        {
            sb.AppendLine($"  - {s.SubjectName}: {s.AveragePercent:F1}% (APS: {s.ApsPoints})");
        }
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with valid JSON in this exact structure (no other text):");
        sb.AppendLine("""
{
  "summary": "One sentence summarising the gap or confirming the learner qualifies.",
  "subjectGaps": [
    {
      "subject": "Mathematics",
      "currentPercent": 65.0,
      "requiredPercent": 70,
      "advice": "Specific, actionable advice for improving this subject by the required amount."
    }
  ],
  "overallAdvice": "2-3 sentences of overall strategic advice prioritising the highest-impact improvements.",
  "studySuggestions": [
    "Specific study tip 1",
    "Specific study tip 2",
    "Specific study tip 3"
  ]
}
""");
        sb.AppendLine("Only include subjects in subjectGaps where the learner has not yet met the requirement.");
        sb.AppendLine("Use South African English. Keep advice encouraging and practical.");

        return sb.ToString();
    }

    private async Task<(string? json, int inputTokens, int outputTokens)> CallClaudeAsync(string apiKey, string prompt)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("anthropic");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var body = new
            {
                model = Model,
                max_tokens = 1000,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var response = await client.PostAsync(AnthropicApiUrl,
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API returned {Status}", response.StatusCode);
                return (null, 0, 0);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";

            var usage = doc.RootElement.GetProperty("usage");
            var inputTokens = usage.GetProperty("input_tokens").GetInt32();
            var outputTokens = usage.GetProperty("output_tokens").GetInt32();

            // Extract JSON block from response
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                _logger.LogWarning("No JSON found in gap analysis response");
                return (null, inputTokens, outputTokens);
            }

            return (text[start..(end + 1)], inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API call failed for gap analysis");
            return (null, 0, 0);
        }
    }

    private static GapAnalysisResultDto ParseResult(string json, int currentAps, int requiredAps)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var summary = root.GetProperty("summary").GetString() ?? "";
        var overallAdvice = root.GetProperty("overallAdvice").GetString() ?? "";

        var subjectGaps = new List<GapAnalysisSubjectGapDto>();
        if (root.TryGetProperty("subjectGaps", out var gapsEl))
        {
            foreach (var g in gapsEl.EnumerateArray())
            {
                subjectGaps.Add(new GapAnalysisSubjectGapDto(
                    g.GetProperty("subject").GetString() ?? "",
                    g.TryGetProperty("currentPercent", out var cp) ? (double?)cp.GetDouble() : null,
                    g.TryGetProperty("requiredPercent", out var rp) ? rp.GetInt32() : 0,
                    g.TryGetProperty("advice", out var adv) ? adv.GetString() ?? "" : ""
                ));
            }
        }

        var suggestions = new List<string>();
        if (root.TryGetProperty("studySuggestions", out var sugEl))
            foreach (var s in sugEl.EnumerateArray())
                suggestions.Add(s.GetString() ?? "");

        return new GapAnalysisResultDto(
            summary,
            currentAps,
            requiredAps,
            Math.Max(0, requiredAps - currentAps),
            subjectGaps,
            overallAdvice,
            suggestions,
            FromCache: false
        );
    }

    private async Task LogUsageAsync(
        Guid schoolId, Guid studentId, int inputTokens, int outputTokens,
        decimal costZar, bool success, string? error)
    {
        _context.AiUsageLogs.Add(new AiUsageLog
        {
            AiUsageLogId = Guid.NewGuid(),
            SchoolId = schoolId,
            Feature = "GapAnalysis",
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
