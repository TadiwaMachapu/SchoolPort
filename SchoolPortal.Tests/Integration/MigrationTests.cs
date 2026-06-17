using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Verifies the Sprint 1.5.0 schema is correct on real Postgres: the new Identity/
/// Positions/Permissions tables exist and are queryable, and the audit_logs key-type
/// repair (bigint -> uuid) took effect so audit writes succeed. (The schema is built via
/// EnsureCreated — see PostgresFixture for why the historical migration chain is not run.)
/// </summary>
[Collection("Postgres")]
public class MigrationTests
{
    private readonly PostgresFixture _pg;

    public MigrationTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Schema_HasIdentityPositionsPermissionsTables()
    {
        await using var ctx = _pg.CreateContext();

        // The new tables exist and are queryable.
        Assert.Equal(0, await ctx.Positions.CountAsync());
        Assert.Equal(0, await ctx.Permissions.CountAsync());
        Assert.Equal(0, await ctx.UserPositions.CountAsync());
        Assert.Equal(0, await ctx.UserPositionScopes.CountAsync());
    }

    [Fact]
    public async Task AuditLogId_IsUuid_AfterMigration()
    {
        await using var conn = (NpgsqlConnection)_pg.DataSource.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT data_type FROM information_schema.columns
                            WHERE table_name = 'audit_logs' AND column_name = 'audit_log_id';";
        var dataType = (string?)await cmd.ExecuteScalarAsync();

        Assert.Equal("uuid", dataType);
    }

    [Fact]
    public async Task AuditLog_AcceptsInsert_WithUuidDefault()
    {
        // The whole point of the key-type fix: inserts now succeed (previously the
        // bigint/uuid mismatch made every audit write fail).
        await using var ctx = _pg.CreateContext();
        ctx.AuditLogs.Add(new Data.Entities.AuditLog
        {
            Action = "test.write",
            EntityType = "Test",
            Timestamp = DateTime.UtcNow
        });
        var saved = await ctx.SaveChangesAsync();
        Assert.Equal(1, saved);
    }
}
