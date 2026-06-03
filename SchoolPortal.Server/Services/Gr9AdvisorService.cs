using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

public interface IGr9AdvisorService
{
    Task<Gr9ProfileDto> GetGr9ProfileAsync(Guid studentId, Guid schoolId);
    Task<Gr9AiAdviceDto?> GetAiAdviceAsync(Guid studentId, Guid schoolId, bool forceRefresh = false);
}

public record Gr9SubjectMarkDto(string SubjectName, double AveragePercent);

public record FetSubjectEligibilityDto(
    string FetSubject,
    string Eligibility,   // "Recommended" | "Borderline" | "NotRecommended" | "NoData"
    string? Gr9Subject,
    int? RecommendedMin,
    double? StudentPercent,
    List<string> CareerPaths
);

public record Gr9ProfileDto(
    bool IsGrade9,
    List<Gr9SubjectMarkDto> Marks,
    List<FetSubjectEligibilityDto> FetEligibility,
    List<string> SavedCareerGoals
);

public record Gr9AiRecommendedSubjectDto(string Name, string Reason, List<string> CareerLinks);
public record Gr9AiImprovementAreaDto(string Subject, double CurrentPercent, string Advice);

public record Gr9AiAdviceDto(
    string Summary,
    List<Gr9AiRecommendedSubjectDto> RecommendedSubjects,
    List<Gr9AiImprovementAreaDto> ImprovementAreas,
    List<string> CareerPathsEnabled,
    bool FromCache
);

public class Gr9AdvisorService : IGr9AdvisorService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-6";
    private const decimal InputCostPerToken = 3m / 1_000_000m;
    private const decimal OutputCostPerToken = 15m / 1_000_000m;
    private const decimal UsdToZarRate = 18.5m;
    private const int CacheTtlDays = 7;

    // CAPS FET elective subjects available at most SA schools
    private static readonly string[] FetSubjects =
    {
        "Mathematics", "Mathematical Literacy",
        "Physical Sciences", "Life Sciences",
        "Geography", "History", "Tourism",
        "Accounting", "Business Studies", "Economics",
        "Information Technology", "Computer Applications Technology",
        "Engineering Graphics & Design", "Agricultural Sciences",
        "Consumer Studies", "Dramatic Arts", "Music", "Visual Arts",
        "Hospitality Studies", "Civil Technology"
    };

    private readonly SchoolPortalDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<Gr9AdvisorService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public Gr9AdvisorService(
        SchoolPortalDbContext context,
        IConfiguration config,
        ILogger<Gr9AdvisorService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Gr9ProfileDto> GetGr9ProfileAsync(Guid studentId, Guid schoolId)
    {
        var inGr9 = await _context.Enrollments
            .AnyAsync(e =>
                e.StudentId == studentId &&
                e.IsActive &&
                e.SchoolId == schoolId &&
                e.Class.GradeLevel == 9);

        if (!inGr9)
            return new Gr9ProfileDto(false, [], [], []);

        // Current Gr 9 subject marks
        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g =>
                g.SchoolId == schoolId &&
                g.Submission.StudentId == studentId &&
                g.Submission.Assignment.ClassSubject.Class.GradeLevel == 9)
            .Select(g => new
            {
                SubjectName = g.Submission.Assignment.ClassSubject.Subject.Name,
                Percentage = g.Submission.Assignment.MaxMarks > 0
                    ? Math.Round((double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100, 1)
                    : 0.0
            })
            .ToListAsync();

        var marksBySubject = grades
            .GroupBy(g => g.SubjectName)
            .Select(g => new Gr9SubjectMarkDto(g.Key, Math.Round(g.Average(x => x.Percentage), 1)))
            .OrderBy(m => m.SubjectName)
            .ToList();

        // SeniorPhaseRequirements
        var requirements = await _context.SeniorPhaseRequirements
            .AsNoTracking()
            .ToListAsync();

        // Career paths per FET subject from the university course graph
        var careerMap = await BuildCareerMapAsync();

        // Build eligibility for each FET subject
        var eligibility = new List<FetSubjectEligibilityDto>();
        foreach (var fetSubject in FetSubjects)
        {
            var req = requirements.FirstOrDefault(r => r.FetSubjectName == fetSubject);
            var careers = careerMap.TryGetValue(fetSubject, out var c) ? c : new List<string>();

            if (req == null)
            {
                eligibility.Add(new FetSubjectEligibilityDto(
                    fetSubject, "NoData", null, null, null, careers));
                continue;
            }

            var studentMark = marksBySubject
                .FirstOrDefault(m => m.SubjectName.Equals(req.RequiredSeniorPhaseSubjectName, StringComparison.OrdinalIgnoreCase));

            if (studentMark == null)
            {
                eligibility.Add(new FetSubjectEligibilityDto(
                    fetSubject, "NoData", req.RequiredSeniorPhaseSubjectName, req.RecommendedMinPercent, null, careers));
                continue;
            }

            var threshold = req.RecommendedMinPercent ?? 50;
            string status;
            if (studentMark.AveragePercent >= threshold)
                status = "Recommended";
            else if (studentMark.AveragePercent >= threshold - 10)
                status = "Borderline";
            else
                status = "NotRecommended";

            eligibility.Add(new FetSubjectEligibilityDto(
                fetSubject, status, req.RequiredSeniorPhaseSubjectName,
                req.RecommendedMinPercent, studentMark.AveragePercent, careers));
        }

        // Saved career goals (course names)
        var savedGoals = await _context.LearnerCareerGoals
            .AsNoTracking()
            .Where(g => g.StudentId == studentId && g.SchoolId == schoolId)
            .Include(g => g.UniversityCourse)
                .ThenInclude(c => c.Career)
            .Select(g => g.UniversityCourse.Career != null
                ? g.UniversityCourse.Career.Name
                : g.UniversityCourse.Name)
            .ToListAsync();

        return new Gr9ProfileDto(true, marksBySubject, eligibility, savedGoals);
    }

    public async Task<Gr9AiAdviceDto?> GetAiAdviceAsync(Guid studentId, Guid schoolId, bool forceRefresh = false)
    {
        var profile = await GetGr9ProfileAsync(studentId, schoolId);
        if (!profile.IsGrade9) return null;

        var fingerprint = BuildFingerprint(studentId, profile.Marks);

        if (!forceRefresh)
        {
            var cached = await _context.Gr9SubjectAdviceCaches
                .AsNoTracking()
                .Where(c =>
                    c.StudentId == studentId &&
                    c.InputFingerprint == fingerprint &&
                    c.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (cached != null)
            {
                var cachedResult = DeserialiseAdvice(cached.AdviceJson);
                if (cachedResult != null)
                    return cachedResult with { FromCache = true };
            }
        }

        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Anthropic:ApiKey not configured — Gr9 advisor unavailable");
            return null;
        }

        if (!await CheckCostCapAsync(schoolId))
            return null;

        var prompt = BuildPrompt(profile);
        var (resultJson, inputTokens, outputTokens) = await CallClaudeAsync(apiKey, prompt);

        var costZar = (inputTokens * InputCostPerToken + outputTokens * OutputCostPerToken) * UsdToZarRate;

        if (resultJson == null)
        {
            await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, false, "Claude API call failed");
            return null;
        }

        Gr9AiAdviceDto? advice;
        try
        {
            advice = DeserialiseAdvice(resultJson);
            if (advice == null) throw new InvalidOperationException("Null result after deserialisation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gr9 advisor response");
            await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, false, "Parse failed: " + ex.Message);
            return null;
        }

        await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, true, null);

        // Cache
        var stale = await _context.Gr9SubjectAdviceCaches
            .Where(c => c.StudentId == studentId)
            .ToListAsync();
        if (stale.Any()) _context.Gr9SubjectAdviceCaches.RemoveRange(stale);

        _context.Gr9SubjectAdviceCaches.Add(new Gr9SubjectAdviceCache
        {
            Gr9SubjectAdviceCacheId = Guid.NewGuid(),
            StudentId = studentId,
            InputFingerprint = fingerprint,
            AdviceJson = resultJson,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(CacheTtlDays),
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        await _context.SaveChangesAsync();

        return advice;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, List<string>>> BuildCareerMapAsync()
    {
        var requirements = await _context.CourseSubjectRequirements
            .AsNoTracking()
            .Include(r => r.UniversityCourse)
                .ThenInclude(c => c.Career)
            .Where(r => r.IsRequired && r.UniversityCourse.Career != null)
            .ToListAsync();

        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in requirements)
        {
            if (!map.ContainsKey(req.SubjectName))
                map[req.SubjectName] = new List<string>();

            var careerName = req.UniversityCourse.Career!.Name;
            if (!map[req.SubjectName].Contains(careerName))
                map[req.SubjectName].Add(careerName);
        }

        return map;
    }

    private static string BuildFingerprint(Guid studentId, List<Gr9SubjectMarkDto> marks)
    {
        var key = new StringBuilder();
        key.Append(studentId);
        foreach (var m in marks.OrderBy(x => x.SubjectName))
            key.Append($"|{m.SubjectName}={m.AveragePercent:F1}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildPrompt(Gr9ProfileDto profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a South African school education counsellor helping a Grade 9 learner choose their Grade 10–12 (FET phase) subjects.");
        sb.AppendLine("Base your advice on CAPS curriculum requirements and South African university admission requirements.");
        sb.AppendLine("Use South African English. Be encouraging, practical, and specific.");
        sb.AppendLine();
        sb.AppendLine("LEARNER'S CURRENT GRADE 9 MARKS:");
        if (profile.Marks.Count == 0)
        {
            sb.AppendLine("  No marks recorded yet (provide general guidance).");
        }
        else
        {
            foreach (var m in profile.Marks)
                sb.AppendLine($"  - {m.SubjectName}: {m.AveragePercent:F1}%");
        }
        sb.AppendLine();

        var recommended = profile.FetEligibility.Where(e => e.Eligibility == "Recommended").ToList();
        var borderline = profile.FetEligibility.Where(e => e.Eligibility == "Borderline").ToList();

        sb.AppendLine("FET SUBJECT ELIGIBILITY (based on Gr 9 marks):");
        sb.AppendLine($"  Recommended: {string.Join(", ", recommended.Select(e => e.FetSubject))}");
        sb.AppendLine($"  Borderline: {string.Join(", ", borderline.Select(e => e.FetSubject))}");
        sb.AppendLine();

        if (profile.SavedCareerGoals.Count > 0)
        {
            sb.AppendLine("CAREER GOALS ALREADY SAVED:");
            foreach (var g in profile.SavedCareerGoals)
                sb.AppendLine($"  - {g}");
            sb.AppendLine();
        }

        sb.AppendLine("Respond with ONLY a JSON object (no other text):");
        sb.AppendLine("""
{
  "summary": "2–3 sentence personalised overview for this learner.",
  "recommendedSubjects": [
    { "name": "Mathematics", "reason": "Brief personalised reason based on their marks.", "careerLinks": ["Career1", "Career2"] }
  ],
  "improvementAreas": [
    { "subject": "Natural Sciences", "currentPercent": 52.0, "advice": "Specific advice to improve before subject choice." }
  ],
  "careerPathsEnabled": ["Career A", "Career B", "Career C"]
}
""");
        sb.AppendLine("Recommend 4–6 FET subjects. Only include improvementAreas for subjects where improvement would unlock better subject choices. Keep careerPathsEnabled to the top 6 most relevant careers.");

        return sb.ToString();
    }

    private static Gr9AiAdviceDto? DeserialiseAdvice(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var summary = root.GetProperty("summary").GetString() ?? "";

        var recommended = new List<Gr9AiRecommendedSubjectDto>();
        if (root.TryGetProperty("recommendedSubjects", out var recEl))
        {
            foreach (var item in recEl.EnumerateArray())
            {
                var careers = new List<string>();
                if (item.TryGetProperty("careerLinks", out var cl))
                    foreach (var c in cl.EnumerateArray())
                        careers.Add(c.GetString() ?? "");

                recommended.Add(new Gr9AiRecommendedSubjectDto(
                    item.GetProperty("name").GetString() ?? "",
                    item.GetProperty("reason").GetString() ?? "",
                    careers));
            }
        }

        var improvements = new List<Gr9AiImprovementAreaDto>();
        if (root.TryGetProperty("improvementAreas", out var impEl))
        {
            foreach (var item in impEl.EnumerateArray())
            {
                improvements.Add(new Gr9AiImprovementAreaDto(
                    item.GetProperty("subject").GetString() ?? "",
                    item.TryGetProperty("currentPercent", out var cp) ? cp.GetDouble() : 0,
                    item.GetProperty("advice").GetString() ?? ""));
            }
        }

        var enabledCareers = new List<string>();
        if (root.TryGetProperty("careerPathsEnabled", out var careersEl))
            foreach (var c in careersEl.EnumerateArray())
                enabledCareers.Add(c.GetString() ?? "");

        return new Gr9AiAdviceDto(summary, recommended, improvements, enabledCareers, FromCache: false);
    }

    private async Task<bool> CheckCostCapAsync(Guid schoolId)
    {
        var school = await _context.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.SchoolId == schoolId);
        if (school == null) return true;

        var cap = school.Settings.AiMonthlyCostCapZar;
        if (cap <= 0) return true;

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var spent = await _context.AiUsageLogs
            .Where(l => l.SchoolId == schoolId && l.CreatedAt >= monthStart && l.Success)
            .SumAsync(l => (decimal?)l.EstimatedCostZar) ?? 0m;

        if (spent >= cap)
        {
            _logger.LogInformation("AI cost cap reached for school {SchoolId}", schoolId);
            return false;
        }

        return true;
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
                max_tokens = 1500,
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

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return (null, inputTokens, outputTokens);

            return (text[start..(end + 1)], inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API call failed for Gr9 advisor");
            return (null, 0, 0);
        }
    }

    private async Task LogUsageAsync(
        Guid schoolId, Guid studentId, int inputTokens, int outputTokens,
        decimal costZar, bool success, string? error)
    {
        _context.AiUsageLogs.Add(new AiUsageLog
        {
            AiUsageLogId = Guid.NewGuid(),
            SchoolId = schoolId,
            Feature = "Gr9Advisor",
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
