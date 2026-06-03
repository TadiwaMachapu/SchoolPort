using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

public interface IMatricTutorService
{
    Task<TutorResponseDto?> GetExplanationAsync(
        Guid studentId, Guid schoolId, string subject, string question, bool forceRefresh = false);
}

public record TutorResponseDto(string AnswerMarkdown, bool FromCache);

public class MatricTutorService : IMatricTutorService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-6";
    private const decimal InputCostPerToken = 3m / 1_000_000m;
    private const decimal OutputCostPerToken = 15m / 1_000_000m;
    private const decimal UsdToZarRate = 18.5m;
    private const int CacheTtlDays = 30;

    private readonly SchoolPortalDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<MatricTutorService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public MatricTutorService(
        SchoolPortalDbContext context,
        IConfiguration config,
        ILogger<MatricTutorService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TutorResponseDto?> GetExplanationAsync(
        Guid studentId, Guid schoolId, string subject, string question, bool forceRefresh = false)
    {
        var fingerprint = BuildFingerprint(subject, question);

        if (!forceRefresh)
        {
            var cached = await _context.MatricTutorCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.InputFingerprint == fingerprint &&
                    c.ExpiresAt > DateTime.UtcNow);

            if (cached != null)
                return new TutorResponseDto(cached.AnswerMarkdown, FromCache: true);
        }

        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Anthropic:ApiKey not configured — tutor unavailable");
            return null;
        }

        if (!await CheckCostCapAsync(schoolId))
            return null;

        var prompt = BuildPrompt(subject, question);
        var (answerMarkdown, inputTokens, outputTokens) = await CallClaudeAsync(apiKey, prompt);

        var costZar = (inputTokens * InputCostPerToken + outputTokens * OutputCostPerToken) * UsdToZarRate;

        if (answerMarkdown == null)
        {
            await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, false, "Claude API call failed");
            return null;
        }

        await LogUsageAsync(schoolId, studentId, inputTokens, outputTokens, costZar, true, null);

        // Upsert cache (remove stale entry with same fingerprint if forceRefresh)
        var stale = await _context.MatricTutorCaches
            .Where(c => c.InputFingerprint == fingerprint)
            .ToListAsync();
        if (stale.Any()) _context.MatricTutorCaches.RemoveRange(stale);

        _context.MatricTutorCaches.Add(new MatricTutorCache
        {
            MatricTutorCacheId = Guid.NewGuid(),
            Subject = subject,
            InputFingerprint = fingerprint,
            Question = question,
            AnswerMarkdown = answerMarkdown,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(CacheTtlDays),
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        });

        await _context.SaveChangesAsync();

        return new TutorResponseDto(answerMarkdown, FromCache: false);
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
            _logger.LogInformation("AI cost cap reached for school {SchoolId}: R{Spent} >= R{Cap}", schoolId, spent, cap);
            return false;
        }

        return true;
    }

    private static string BuildFingerprint(string subject, string question)
    {
        var key = $"{subject.Trim().ToLowerInvariant()}:{question.Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildPrompt(string subject, string question)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are an expert South African Grade 12 tutor specialising in {subject} (CAPS curriculum).");
        sb.AppendLine("A learner has asked you a question. Provide a clear, helpful explanation.");
        sb.AppendLine("Use South African English. Keep your response under 500 words.");
        sb.AppendLine("Use markdown formatting where helpful (## headings, **bold**, bullet lists, numbered steps).");
        sb.AppendLine();
        sb.AppendLine($"SUBJECT: {subject}");
        sb.AppendLine($"LEARNER QUESTION: {question}");
        sb.AppendLine();
        sb.AppendLine("Respond with ONLY a JSON object (no other text):");
        sb.AppendLine("""{ "answer": "your full markdown answer here" }""");

        return sb.ToString();
    }

    private async Task<(string? answer, int inputTokens, int outputTokens)> CallClaudeAsync(string apiKey, string prompt)
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
                max_tokens = 1024,
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

            using var parsed = JsonDocument.Parse(text[start..(end + 1)]);
            var answer = parsed.RootElement.GetProperty("answer").GetString();
            return (answer, inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API call failed for matric tutor");
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
            Feature = "MatricTutor",
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
