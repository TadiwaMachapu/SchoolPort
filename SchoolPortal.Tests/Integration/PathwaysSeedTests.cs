using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Server.Seeds;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Sprint 1.5.1 Gap 4 — university seed expansion to all 26 SA public universities.
/// Pins: full count on a fresh seed, idempotency, and the additive sync-by-abbreviation
/// (a database missing an expansion university converges on the next seed run — the
/// mechanism already-seeded live databases rely on).
/// </summary>
[Collection("Postgres")]
public class PathwaysSeedTests
{
    private readonly PostgresFixture _pg;
    public PathwaysSeedTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Seed_Produces26PublicUniversities_AndIsIdempotent()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await PathwaysSeedData.SeedAsync(db, NullLogger.Instance);

            Assert.Equal(26, await db.Universities.CountAsync());
            var courses = await db.UniversityCourses.CountAsync();
            Assert.True(courses >= 75, $"Expected at least 75 courses, got {courses}");

            // Every university has at least one course (incl. UFH's institutional-minimum row).
            var uniWithoutCourses = await db.Universities
                .Where(u => !u.Courses.Any())
                .Select(u => u.Abbreviation)
                .ToListAsync();
            Assert.Empty(uniWithoutCourses);

            // Group B convention: own-scale universities seed MinimumAps = 0 with notes.
            var nmuMbchb = await db.UniversityCourses
                .FirstAsync(c => c.University.Abbreviation == "NMU" && c.Name.Contains("MBChB"));
            Assert.Equal(0, nmuMbchb.MinimumAps);
            Assert.Contains("AS 430", nmuMbchb.ApsNotes);

            // UFH decision: single institutional-minimum row at APS 26.
            var ufh = await db.UniversityCourses
                .FirstAsync(c => c.University.Abbreviation == "UFH");
            Assert.Equal(26, ufh.MinimumAps);

            // Idempotent: a second run adds nothing.
            var unis = await db.Universities.CountAsync();
            await PathwaysSeedData.SeedAsync(db, NullLogger.Instance);
            Assert.Equal(unis, await db.Universities.CountAsync());
            Assert.Equal(courses, await db.UniversityCourses.CountAsync());
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Seed_BackfillsMissingExpansionUniversity_OnAlreadySeededDatabase()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await PathwaysSeedData.SeedAsync(db, NullLogger.Instance);

            // Simulate an older database seeded before the expansion contained SMU.
            var smu = await db.Universities.Include(u => u.Courses)
                .FirstAsync(u => u.Abbreviation == "SMU");
            db.Universities.Remove(smu); // cascade removes its courses
            await db.SaveChangesAsync();
            Assert.Equal(25, await db.Universities.CountAsync());

            // Next seed run (Universities already non-empty → additive path) restores it.
            await PathwaysSeedData.SeedAsync(db, NullLogger.Instance);
            Assert.Equal(26, await db.Universities.CountAsync());
            Assert.True(await db.UniversityCourses
                .AnyAsync(c => c.University.Abbreviation == "SMU" && c.Name.Contains("MBChB")));
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
