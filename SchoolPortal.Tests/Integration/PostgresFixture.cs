using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SchoolPortal.Data;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Shared real-Postgres fixture for integration and security tests. Real Postgres is
/// required because: (1) the in-memory provider cannot map the jsonb POCO columns
/// (School.Features/Theme/Settings); (2) RLS, raw-SQL migrations, and scope-filtering
/// fidelity cannot be exercised in-memory.
///
/// The Testcontainers .NET wrapper is intentionally NOT used — Smart App Control on this
/// machine blocks the unsigned assembly. Instead the fixture connects to a Postgres the
/// developer starts via the Docker CLI:
///
///   docker run --rm -d --name schoolport-test-pg -p 5432:5432 \
///     -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=schoolport_test postgres:16-alpine
///
/// Override the connection with the TEST_PG_CONNECTION environment variable if needed.
/// The data source enables dynamic JSON, mirroring production (Program.cs).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=schoolport_test;Username=postgres;Password=postgres";

    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("TEST_PG_CONNECTION") ?? DefaultConnection;

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        DataSource = new NpgsqlDataSourceBuilder(ConnectionString)
            .EnableDynamicJson()
            .Build();

        // Build the schema directly from the current model. We do NOT run the migration
        // chain here: the historical migrations are pre-existingly broken (InitialCreate
        // had an invalid audit_logs default; AddAcademicCalendar references super_admins,
        // which no migration creates), so the chain cannot apply from scratch. EnsureCreated
        // yields a correct, complete schema for integration/security tests. The Sprint 1.5.0
        // migration itself is validated against the live database at apply time (gated).
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public SchoolPortalDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SchoolPortalDbContext>()
            .UseNpgsql(DataSource)
            .ConfigureWarnings(SuppressManyServiceProviders)
            .Options;
        return new SchoolPortalDbContext(options);
    }

    // Each isolated database needs its own NpgsqlDataSource (distinct connection string), and EF
    // builds a separate internal service provider per data source. With enough isolated-DB tests the
    // count crosses 20 and EF escalates ManyServiceProvidersCreatedWarning to an error — a false alarm
    // here, since the many providers are intentional per-test isolation, not a leaked singleton.
    private static void SuppressManyServiceProviders(WarningsConfigurationBuilder w) =>
        w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);

    /// <summary>
    /// Creates a brand-new, isolated database in the container with a fresh schema, so a
    /// mutating test cannot affect any other test. The caller owns the returned context and
    /// data source and must dispose both.
    /// </summary>
    public async Task<(SchoolPortalDbContext Context, NpgsqlDataSource Source)> CreateIsolatedDatabaseAsync()
    {
        var dbName = "t_" + Guid.NewGuid().ToString("N");
        await using (var admin = DataSource.CreateConnection())
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        var csb = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = dbName };
        var source = new NpgsqlDataSourceBuilder(csb.ConnectionString).EnableDynamicJson().Build();
        var options = new DbContextOptionsBuilder<SchoolPortalDbContext>().UseNpgsql(source)
            .ConfigureWarnings(SuppressManyServiceProviders).Options;
        var ctx = new SchoolPortalDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        return (ctx, source);
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null) await DataSource.DisposeAsync();
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }
