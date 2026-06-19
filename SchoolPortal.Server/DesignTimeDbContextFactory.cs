using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using SchoolPortal.Data;

namespace SchoolPortal.Server;

/// <summary>
/// Used ONLY by the `dotnet ef` design-time tooling (never at runtime). Without it, EF boots the
/// full API host (Program.cs), whose startup block runs EnsureCreated/Migrate + seeds — which would
/// interfere with a clean, deterministic migration replay. This factory builds the DbContext directly
/// from the EF_MIGRATIONS_CONNECTION environment variable (set by the CI "Migration replay" job),
/// falling back to a localhost default for local use. It performs no migration or seeding itself.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SchoolPortalDbContext>
{
    public SchoolPortalDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("EF_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=schoolport_migrate;Username=postgres;Password=postgres";

        // EnableDynamicJson mirrors Program.cs so the jsonb POCO columns map identically.
        var dataSource = new NpgsqlDataSourceBuilder(connection).EnableDynamicJson().Build();

        var options = new DbContextOptionsBuilder<SchoolPortalDbContext>()
            .UseNpgsql(dataSource)
            .Options;

        return new SchoolPortalDbContext(options);
    }
}
