using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Server.Seeds;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Validates the STEP 3 Δ1 catalogue sync-by-key mechanism on real Postgres: a re-run
/// adds nothing (idempotent), and a catalogue key missing from the database (the
/// "already-seeded live DB needs new keys" scenario) is inserted by the next run
/// without touching existing rows.
/// </summary>
[Collection("Postgres")]
public class SeedSyncTests
{
    private readonly PostgresFixture _pg;
    public SeedSyncTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task SeedSync_IsIdempotent_AndBackfillsMissingKeys()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            // First run: full catalogue.
            await PositionsSeedData.SeedAsync(db, NullLogger.Instance);
            var perms = await db.Permissions.CountAsync();
            var positions = await db.Positions.CountAsync();
            var mappings = await db.PositionPermissions.CountAsync();
            Assert.True(perms > 0 && positions > 0 && mappings > 0);

            // Identity-implicit additions from Step 3 are present.
            Assert.True(await db.Permissions.AnyAsync(p => p.Key == "assignments.submit"));
            Assert.True(await db.Permissions.AnyAsync(p => p.Key == "finance.pay"));

            // Step 6 / D1: platform.access is seeded in the catalogue and, being identity-implicit,
            // is attached to NO position (it is granted by identity, not appointment).
            Assert.True(await db.Permissions.AnyAsync(p => p.Key == "platform.access"));
            var platformPerm = await db.Permissions.SingleAsync(p => p.Key == "platform.access");
            Assert.False(await db.PositionPermissions.AnyAsync(pp => pp.PermissionId == platformPerm.PermissionId));

            // Re-run: nothing changes (idempotent).
            await PositionsSeedData.SeedAsync(db, NullLogger.Instance);
            Assert.Equal(perms, await db.Permissions.CountAsync());
            Assert.Equal(positions, await db.Positions.CountAsync());
            Assert.Equal(mappings, await db.PositionPermissions.CountAsync());

            // Simulate an older-seeded DB missing a key (and its mappings): delete one
            // permission; FK cascade removes its position mappings.
            var victim = await db.Permissions.SingleAsync(p => p.Key == "finance.refund");
            db.Permissions.Remove(victim);
            await db.SaveChangesAsync();
            Assert.Equal(perms - 1, await db.Permissions.CountAsync());

            // Next run restores the missing key AND its mappings, touching nothing else.
            await PositionsSeedData.SeedAsync(db, NullLogger.Instance);
            Assert.Equal(perms, await db.Permissions.CountAsync());
            Assert.Equal(mappings, await db.PositionPermissions.CountAsync());
            Assert.True(await db.Permissions.AnyAsync(p => p.Key == "finance.refund"));
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
