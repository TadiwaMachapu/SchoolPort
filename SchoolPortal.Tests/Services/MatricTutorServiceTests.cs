using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Tests.Integration;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.2 Step 3 — Matric tutor v2 rate limiting, on real Postgres (Gemini backend).
/// Contract under test: learners get MatricTutorDailyLimit successful non-cached answers
/// per UTC day (default 20; &lt;= 0 disables); failed calls and yesterday's usage consume
/// no quota; cache hits bypass the limit entirely; staff callers (no Student row) are
/// never day-capped. IGeminiService is mocked: an unconfigured key surfaces as
/// GeminiNotConfiguredException → "not_configured" (distinguishing it from "rate_limited"
/// in the limit tests), a GeminiException → "api_error" without consuming quota, and a
/// scripted answer drives the full success path.
/// </summary>
[Collection("Postgres")]
public class MatricTutorServiceTests
{
    private readonly PostgresFixture _pg;
    public MatricTutorServiceTests(PostgresFixture pg) => _pg = pg;

    private static readonly Guid StudentId = Guid.NewGuid();

    // Gemini "not configured" — any call that reaches the AI throws GeminiNotConfiguredException.
    private static MatricTutorService ServiceFor(SchoolPortalDbContext db)
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeminiNotConfiguredException());
        return new MatricTutorService(db, gemini.Object, NullLogger<MatricTutorService>.Instance);
    }

    // Gemini scripted to return a fixed answer — drives the full success path.
    private static MatricTutorService ServiceWithGemini(SchoolPortalDbContext db, string answerText)
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answerText);
        return new MatricTutorService(db, gemini.Object, NullLogger<MatricTutorService>.Instance);
    }

    // Gemini scripted to fail (non-200) — drives the api_error path.
    private static MatricTutorService ServiceWithFailingGemini(SchoolPortalDbContext db)
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GeminiException(503));
        return new MatricTutorService(db, gemini.Object, NullLogger<MatricTutorService>.Instance);
    }

    private static async Task<Guid> SeedSchoolAsync(SchoolPortalDbContext db, int? dailyLimit = null)
    {
        var school = new School
        {
            SchoolId = Guid.NewGuid(),
            Name = "Tutor Test School",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        if (dailyLimit.HasValue) school.Settings.MatricTutorDailyLimit = dailyLimit.Value;
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        return school.SchoolId;
    }

    private static AiUsageLog Usage(Guid schoolId, Guid? studentId, bool success, DateTime createdAt) => new()
    {
        AiUsageLogId = Guid.NewGuid(),
        SchoolId = schoolId,
        Feature = "MatricTutor",
        StudentId = studentId,
        EstimatedCostZar = 0.05m,
        CreatedAt = createdAt,
        Success = success,
    };

    [Fact]
    public async Task Gemini_SuccessPath_ReturnsAnswer_LogsUsage_DecrementsQuota_AndCaches()
    {
        // Drives the full success path with a scripted IGeminiService: the returned text is the
        // markdown answer directly (no JSON wrapper), usage is logged (cost 0, free tier) so the
        // daily tally moves, and the answer is cached for reuse.
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db);
            const string answer = "## Area of a circle\nWhat do you already know about π and the radius?";

            var outcome = await ServiceWithGemini(db, answer)
                .GetExplanationAsync(StudentId, schoolId, "Mathematics", "Area of a circle?");

            Assert.True(outcome.Available);
            Assert.Equal(answer, outcome.AnswerMarkdown);
            Assert.False(outcome.FromCache);
            Assert.Equal(19, outcome.RemainingToday); // 20 default − this successful call

            var log = await db.AiUsageLogs.SingleAsync(l => l.SchoolId == schoolId);
            Assert.True(log.Success);
            Assert.Equal(0m, log.EstimatedCostZar); // free tier
            Assert.True(await db.MatricTutorCaches.AnyAsync(c => c.AnswerMarkdown == answer));
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task GeminiFailure_ReturnsApiError_AndConsumesNoQuota()
    {
        // A non-200 from Gemini surfaces as reason "api_error" and the failed call must NOT
        // consume the learner's daily quota (only Success=true rows count toward the tally).
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db);

            var outcome = await ServiceWithFailingGemini(db)
                .GetExplanationAsync(StudentId, schoolId, "Mathematics", "Explain factorising");

            Assert.False(outcome.Available);
            Assert.Equal("api_error", outcome.Reason);
            Assert.Equal(20, outcome.RemainingToday); // untouched

            var log = await db.AiUsageLogs.SingleAsync(l => l.SchoolId == schoolId);
            Assert.False(log.Success); // logged for diagnostics, but never counted
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Learner_AtDailyLimit_IsRateLimited()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db); // default limit: 20
            for (var i = 0; i < 20; i++)
                db.AiUsageLogs.Add(Usage(schoolId, StudentId, success: true, DateTime.UtcNow.AddMinutes(-i)));
            await db.SaveChangesAsync();

            var outcome = await ServiceFor(db).GetExplanationAsync(StudentId, schoolId, "Mathematics", "What is a limit?");

            Assert.False(outcome.Available);
            Assert.Equal("rate_limited", outcome.Reason);
            Assert.Equal(0, outcome.RemainingToday);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task FailedCalls_YesterdaysUsage_AndOtherLearners_ConsumeNoQuota()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db);
            var today = DateTime.UtcNow;
            for (var i = 0; i < 19; i++)
                db.AiUsageLogs.Add(Usage(schoolId, StudentId, success: true, today.AddMinutes(-i)));   // 19 count
            for (var i = 0; i < 5; i++)
                db.AiUsageLogs.Add(Usage(schoolId, StudentId, success: false, today.AddMinutes(-i)));  // failures don't
            for (var i = 0; i < 10; i++)
                db.AiUsageLogs.Add(Usage(schoolId, StudentId, success: true, today.AddDays(-1)));      // yesterday doesn't
            for (var i = 0; i < 10; i++)
                db.AiUsageLogs.Add(Usage(schoolId, Guid.NewGuid(), success: true, today));             // other learners don't
            await db.SaveChangesAsync();

            var outcome = await ServiceFor(db).GetExplanationAsync(StudentId, schoolId, "Mathematics", "What is a derivative?");

            // 1 question left → passes the rate gate and fails later at the API-key stage.
            Assert.False(outcome.Available);
            Assert.Equal("not_configured", outcome.Reason);
            Assert.Equal(1, outcome.RemainingToday);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task CacheHit_BypassesRateLimit_AndConsumesNoQuota()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db);
            for (var i = 0; i < 20; i++) // quota fully exhausted
                db.AiUsageLogs.Add(Usage(schoolId, StudentId, success: true, DateTime.UtcNow.AddMinutes(-i)));

            const string subject = "Physical Sciences";
            const string question = "Explain Newton's second law";
            db.MatricTutorCaches.Add(new MatricTutorCache
            {
                MatricTutorCacheId = Guid.NewGuid(),
                Subject = subject,
                InputFingerprint = Fingerprint(subject, question),
                Question = question,
                AnswerMarkdown = "## Newton's second law\nF = ma …",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();

            var outcome = await ServiceFor(db).GetExplanationAsync(StudentId, schoolId, subject, question);

            Assert.True(outcome.Available);
            Assert.True(outcome.FromCache);
            Assert.Equal(0, outcome.RemainingToday); // reported, not consumed
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task StaffCaller_IsNotDayCapped()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db);
            for (var i = 0; i < 40; i++) // staff usage logs (StudentId null) — irrelevant to any cap
                db.AiUsageLogs.Add(Usage(schoolId, null, success: true, DateTime.UtcNow.AddMinutes(-i)));
            await db.SaveChangesAsync();

            var outcome = await ServiceFor(db).GetExplanationAsync(null, schoolId, "Accounting", "Explain depreciation");

            // Not rate-limited — falls through to the API-key stage, uncapped.
            Assert.Equal("not_configured", outcome.Reason);
            Assert.Equal(-1, outcome.RemainingToday);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task School_CanDisableTheDailyLimit()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var schoolId = await SeedSchoolAsync(db, dailyLimit: 0);
            for (var i = 0; i < 50; i++)
                db.AiUsageLogs.Add(Usage(schoolId, StudentId, success: true, DateTime.UtcNow.AddMinutes(-i)));
            await db.SaveChangesAsync();

            var outcome = await ServiceFor(db).GetExplanationAsync(StudentId, schoolId, "History", "Causes of the Cold War?");

            Assert.Equal("not_configured", outcome.Reason); // past the rate gate
            Assert.Equal(-1, outcome.RemainingToday);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    // Mirrors MatricTutorService.BuildFingerprint (private): SHA-256 of "subject:question", lowercased/trimmed.
    private static string Fingerprint(string subject, string question)
    {
        var key = $"{subject.Trim().ToLowerInvariant()}:{question.Trim().ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    }
}
