using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using System.Security.Cryptography;
using System.Text;

namespace SchoolPortal.Server.Services;

public interface IMatricTutorService
{
    Task<TutorOutcome> GetExplanationAsync(
        Guid? studentId, Guid schoolId, string subject, string question, bool forceRefresh = false);
}

/// <summary>Tutor v2 outcome. Unavailable reasons: "rate_limited", "not_configured",
/// "api_error". RemainingToday is -1 for callers without a daily cap (staff testers,
/// or a school that disabled the limit).</summary>
public record TutorOutcome(bool Available, string? Reason, string? AnswerMarkdown, bool FromCache, int RemainingToday)
{
    public static TutorOutcome Unavailable(string reason, int remaining = -1) =>
        new(false, reason, null, false, remaining);
}

public class MatricTutorService : IMatricTutorService
{
    // Gemini free tier — no per-call cost, so usage is logged at cost 0 and abuse is bounded by
    // the per-learner daily rate limit alone (the Anthropic-priced monthly cost cap is removed).
    private const int CacheTtlDays = 30;

    private readonly SchoolPortalDbContext _context;
    private readonly IGeminiService _gemini;
    private readonly ILogger<MatricTutorService> _logger;

    public MatricTutorService(
        SchoolPortalDbContext context,
        IGeminiService gemini,
        ILogger<MatricTutorService> logger)
    {
        _context = context;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<TutorOutcome> GetExplanationAsync(
        Guid? studentId, Guid schoolId, string subject, string question, bool forceRefresh = false)
    {
        var fingerprint = BuildFingerprint(subject, question);

        // Cache hits are free — they cost nothing and consume no daily quota, so the
        // cache is checked BEFORE the rate limit.
        if (!forceRefresh)
        {
            var cached = await _context.MatricTutorCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.InputFingerprint == fingerprint &&
                    c.ExpiresAt > DateTime.UtcNow);

            if (cached != null)
                return new TutorOutcome(true, null, cached.AnswerMarkdown, FromCache: true,
                    await RemainingTodayAsync(schoolId, studentId));
        }

        // Sprint 1.5.2 Step 3 — per-learner daily cap (default 20; School.Settings). Only
        // learners are day-capped; staff testers have no Student row (studentId null) and
        // are bounded by the monthly cost cap instead.
        var remaining = await RemainingTodayAsync(schoolId, studentId);
        if (remaining == 0)
            return TutorOutcome.Unavailable("rate_limited", 0);

        string? answerMarkdown;
        try
        {
            answerMarkdown = await _gemini.GenerateAsync(BuildSystemInstruction(subject), question);
        }
        catch (GeminiNotConfiguredException)
        {
            _logger.LogWarning("Gemini:ApiKey not configured — tutor unavailable");
            return TutorOutcome.Unavailable("not_configured", remaining);
        }
        catch (GeminiException ex)
        {
            // API failure — the learner's quota is NOT consumed (only Success=true rows count).
            _logger.LogError(ex, "Gemini call failed for matric tutor");
            await LogUsageAsync(schoolId, studentId, success: false, ex.Message);
            return TutorOutcome.Unavailable("api_error", remaining);
        }

        if (answerMarkdown == null)
        {
            // 200 with no usable candidate (safety block / empty) — same api_error contract, no quota.
            await LogUsageAsync(schoolId, studentId, success: false, "Gemini returned no text");
            return TutorOutcome.Unavailable("api_error", remaining);
        }

        await LogUsageAsync(schoolId, studentId, success: true, null);

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
        });

        await _context.SaveChangesAsync();

        return new TutorOutcome(true, null, answerMarkdown, FromCache: false,
            remaining > 0 ? remaining - 1 : remaining);
    }

    /// <summary>Successful, non-cached answers left today for a learner. -1 = uncapped
    /// (staff caller, or the school disabled the limit); 0 = quota exhausted.</summary>
    private async Task<int> RemainingTodayAsync(Guid schoolId, Guid? studentId)
    {
        if (studentId == null) return -1;

        var school = await _context.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.SchoolId == schoolId);
        var limit = school?.Settings.MatricTutorDailyLimit ?? 20;
        if (limit <= 0) return -1;

        var dayStart = DateTime.UtcNow.Date;
        var usedToday = await _context.AiUsageLogs.CountAsync(l =>
            l.SchoolId == schoolId && l.StudentId == studentId &&
            l.Feature == "MatricTutor" && l.Success && l.CreatedAt >= dayStart);

        return Math.Max(0, limit - usedToday);
    }

    private static string BuildFingerprint(string subject, string question)
    {
        var key = $"{subject.Trim().ToLowerInvariant()}:{question.Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Tutor v2 system prompt — VERBATIM from the sprint spec (all five sentences, including the
    // final follow-up/practice rule). Sent as the Gemini `system_instruction`; the learner's
    // question is the `contents`. The operational lines below (subject, length, format) are
    // implementation scaffolding; the JSON-wrapper instruction is gone — Gemini returns plain
    // text (candidates[0].content.parts[0].text) which is the markdown answer directly.
    private static string BuildSystemInstruction(string subject)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert NSC matric tutor for South African Grade 12 learners.");
        sb.AppendLine("Answer in a teaching style — explain concepts, give examples, test understanding.");
        sb.AppendLine("Reference CAPS curriculum where relevant. Never just give the answer — guide the learner to it.");
        sb.AppendLine("End each response with either a follow-up question to check understanding, or a practice suggestion.");
        sb.AppendLine();
        sb.AppendLine($"You are tutoring {subject}.");
        sb.AppendLine("Use South African English. Keep your response under 500 words.");
        sb.AppendLine("Use markdown formatting where helpful (## headings, **bold**, bullet lists, numbered steps).");

        return sb.ToString();
    }

    // Gemini free tier: no token counts flow through IGeminiService and there is no per-call
    // charge — rows log at cost 0 and exist purely to drive the daily rate-limit tally.
    private async Task LogUsageAsync(Guid schoolId, Guid? studentId, bool success, string? error)
    {
        _context.AiUsageLogs.Add(new AiUsageLog
        {
            AiUsageLogId = Guid.NewGuid(),
            SchoolId = schoolId,
            Feature = "MatricTutor",
            StudentId = studentId,
            EstimatedCostZar = 0m,
            CreatedAt = DateTime.UtcNow,
            Success = success,
            ErrorMessage = error
        });

        await _context.SaveChangesAsync();
    }
}
