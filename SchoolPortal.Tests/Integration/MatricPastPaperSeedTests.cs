using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Seeds;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Sprint 1.5.2 — pins the verified past-paper catalogue sync (MatricPastPaperSeedData):
/// fresh-install counts (November + 2014 Exemplars), idempotency, the three v1 defect
/// repairs (broken index URL healed, phantom 2019 P2 rows deactivated, unverified memo
/// links cleared), and the exemplar memo rules (verified memos seeded; DBE's mislinked /
/// missing English memos NOT seeded).
/// </summary>
[Collection("Postgres")]
public class MatricPastPaperSeedTests
{
    private readonly PostgresFixture _pg;
    public MatricPastPaperSeedTests(PostgresFixture pg) => _pg = pg;

    private const string BrokenV1Url =
        "https://www.education.gov.za/Curriculum/NationalSenioCertificate(NSC)Examinations/NSCPreviousExaminationPapers.aspx";

    [Fact]
    public async Task Sync_SeedsVerifiedCatalogue_AndIsIdempotent()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await MatricPastPaperSeedData.SyncAsync(db, NullLogger.Instance);

            // November: 24 papers/year 2020–2024 + 22 in 2019 (single-paper Accounting/Business
            // Studies year) = 142. Exemplars: the 17 English papers DBE published in 2014.
            Assert.Equal(142, await db.MatricPastPapers.CountAsync(p => p.PaperType == PastPaperType.NSCNovember));
            Assert.Equal(17, await db.MatricPastPapers.CountAsync(p => p.PaperType == PastPaperType.Exemplar));

            // Every exemplar row is 2014, Grade 12, English, active, with a verified direct URL
            // (never the index fallback).
            var exemplars = await db.MatricPastPapers.Where(p => p.PaperType == PastPaperType.Exemplar).ToListAsync();
            Assert.All(exemplars, p =>
            {
                Assert.Equal(2014, p.Year);
                Assert.Equal(12, p.Grade);
                Assert.Equal("English", p.Language);
                Assert.True(p.IsActive);
                Assert.Contains("fileticket=", p.Url);
            });

            // Exemplar memo rules: 14 verified memos; the three DBE failures carry none —
            // Maths P2 (memo link mislinked to the paper), Geography P1 (no English memo
            // published), Geography P2 (English-labelled link serves the Afrikaans file).
            Assert.Equal(14, exemplars.Count(p => p.HasMemo));
            Assert.All(exemplars.Where(p => p.HasMemo), p => Assert.Contains("fileticket=", p.MemoUrl!));
            Assert.False(exemplars.Single(p => p.Subject == "Mathematics" && p.PaperNumber == 2).HasMemo);
            Assert.False(exemplars.Single(p => p.Subject == "Geography" && p.PaperNumber == 1).HasMemo);
            Assert.False(exemplars.Single(p => p.Subject == "Geography" && p.PaperNumber == 2).HasMemo);

            // Single-paper exemplar subjects: DBE published P1 only — no P2 row may exist.
            foreach (var subject in new[] { "Accounting", "Business Studies", "English Home Language" })
                Assert.Null(exemplars.FirstOrDefault(p => p.Subject == subject && p.PaperNumber == 2));

            // 2019 November single-paper reality: no Accounting/Business Studies P2 seeded.
            Assert.False(await db.MatricPastPapers.AnyAsync(p =>
                p.Year == 2019 && p.PaperNumber == 2 && p.PaperType == PastPaperType.NSCNovember &&
                (p.Subject == "Accounting" || p.Subject == "Business Studies")));

            // Re-run: nothing added, nothing mutated (idempotent).
            var total = await db.MatricPastPapers.CountAsync();
            await MatricPastPaperSeedData.SyncAsync(db, NullLogger.Instance);
            Assert.Equal(total, await db.MatricPastPapers.CountAsync());
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Sync_RepairsV1Defects_OnAlreadySeededDatabase()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            // Simulate a live DB carrying the v1 defects: a row on the broken index URL
            // (a subject OUTSIDE the sync catalogue — the heal must still reach it), a
            // phantom 2019 Accounting P2, and a November row with an unverified memo link.
            db.MatricPastPapers.AddRange(
                new MatricPastPaper
                {
                    Subject = "Afrikaans Home Language", Year = 2023, PaperNumber = 1,
                    PaperType = PastPaperType.NSCNovember, Url = BrokenV1Url,
                    HasMemo = true, MemoUrl = BrokenV1Url,
                },
                new MatricPastPaper
                {
                    Subject = "Accounting", Year = 2019, PaperNumber = 2,
                    PaperType = PastPaperType.NSCNovember, Url = BrokenV1Url,
                },
                new MatricPastPaper
                {
                    Subject = "Mathematics", Year = 2023, PaperNumber = 1,
                    PaperType = PastPaperType.NSCNovember, Url = BrokenV1Url,
                    HasMemo = true, MemoUrl = "https://example.invalid/memo.pdf",
                });
            await db.SaveChangesAsync();

            await MatricPastPaperSeedData.SyncAsync(db, NullLogger.Instance);

            // Defect 1 — broken index URL healed everywhere, incl. out-of-catalogue subjects.
            Assert.False(await db.MatricPastPapers.AnyAsync(p => p.Url == BrokenV1Url || p.MemoUrl == BrokenV1Url));
            var afrikaans = await db.MatricPastPapers.SingleAsync(p => p.Subject == "Afrikaans Home Language");
            Assert.Equal(MatricPastPaperSeedData.DbeIndexUrl, afrikaans.Url);

            // Defect 2 — phantom 2019 Accounting P2 deactivated, not deleted.
            var phantom = await db.MatricPastPapers.SingleAsync(p =>
                p.Subject == "Accounting" && p.Year == 2019 && p.PaperNumber == 2);
            Assert.False(phantom.IsActive);

            // Defect 3 — the upserted November row got the verified URL and its unverified
            // memo link cleared.
            var maths = await db.MatricPastPapers.SingleAsync(p =>
                p.Subject == "Mathematics" && p.Year == 2023 && p.PaperNumber == 1 &&
                p.PaperType == PastPaperType.NSCNovember);
            Assert.Contains("fileticket=", maths.Url);
            Assert.False(maths.HasMemo);
            Assert.Null(maths.MemoUrl);
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
