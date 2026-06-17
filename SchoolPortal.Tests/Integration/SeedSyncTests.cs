using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
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

    /// <summary>Step 6 Finance SoD (FIN-1/FIN-2): the seed produces the corrected finance grant
    /// matrix, and the revocation block is delete-if-present — re-introducing a violating grant on
    /// an already-seeded DB and re-running removes it again.</summary>
    [Fact]
    public async Task SeedSync_EnforcesFinanceSoD_AndRevokesViolatingGrants()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await PositionsSeedData.SeedAsync(db, NullLogger.Instance);

            // FIN-1: FinanceManager keeps everything except exempt_approve.
            Assert.False(await Holds(db, "FinanceManager", "finance.exempt_approve"));
            Assert.True(await Holds(db, "FinanceManager", "finance.exempt_initiate"));
            Assert.True(await Holds(db, "FinanceManager", "finance.create_invoice"));
            Assert.True(await Holds(db, "FinanceManager", "finance.refund"));

            // FIN-2: Bursar is capture-and-chase only.
            Assert.False(await Holds(db, "BursarDebtorsClerk", "finance.create_invoice"));
            Assert.False(await Holds(db, "BursarDebtorsClerk", "finance.exempt_initiate"));
            Assert.True(await Holds(db, "BursarDebtorsClerk", "finance.capture_payment"));
            Assert.True(await Holds(db, "BursarDebtorsClerk", "finance.view_all"));

            // FIN-1: SMT approves exemptions.
            Assert.True(await Holds(db, "Principal", "finance.exempt_approve"));
            Assert.True(await Holds(db, "DeputyPrincipal", "finance.exempt_approve"));

            // SoD line: no single position holds both create_invoice AND exempt_approve.
            Assert.False(await Holds(db, "Principal", "finance.create_invoice"));
            Assert.False(await Holds(db, "DeputyPrincipal", "finance.create_invoice"));

            // Revocation is delete-if-present: re-introduce a violating grant (as a legacy DB had),
            // re-seed, and confirm it is removed again.
            var fmId = (await db.Positions.SingleAsync(p => p.Key == "FinanceManager")).PositionId;
            var approveId = (await db.Permissions.SingleAsync(p => p.Key == "finance.exempt_approve")).PermissionId;
            db.PositionPermissions.Add(new PositionPermission { PositionId = fmId, PermissionId = approveId });
            await db.SaveChangesAsync();
            Assert.True(await Holds(db, "FinanceManager", "finance.exempt_approve")); // re-introduced

            await PositionsSeedData.SeedAsync(db, NullLogger.Instance);
            Assert.False(await Holds(db, "FinanceManager", "finance.exempt_approve")); // revoked again
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    private static async Task<bool> Holds(SchoolPortalDbContext db, string posKey, string permKey)
    {
        var posId = await db.Positions.Where(p => p.Key == posKey).Select(p => p.PositionId).FirstOrDefaultAsync();
        var permId = await db.Permissions.Where(p => p.Key == permKey).Select(p => p.PermissionId).FirstOrDefaultAsync();
        if (posId == Guid.Empty || permId == Guid.Empty) return false;
        return await db.PositionPermissions.AnyAsync(pp => pp.PositionId == posId && pp.PermissionId == permId);
    }
}
