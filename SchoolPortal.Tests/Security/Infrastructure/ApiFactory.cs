using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Auth;
using Xunit;

namespace SchoolPortal.Tests.Security.Infrastructure;

/// <summary>
/// Step 10 security-suite foundation. Boots the REAL API (full middleware + auth + permission
/// pipeline) via <see cref="WebApplicationFactory{Program}"/> against a throwaway Postgres database,
/// and mints tokens through the REAL <see cref="AuthService.LoginAsync"/> path — same claims, signing
/// key, and tiered TTL as production. There is deliberately NO parallel test-only token minter: a test
/// that passes here exercises the production token-issuing code, not a stand-in.
///
/// SCHEMA SOURCE: the test database is built with <c>EnsureCreated</c> (from the current EF model),
/// NOT by replaying the migration history — the chain cannot replay from scratch (see CLAUDE.md:
/// InitialCreate/super_admins gaps). This is correct for the security suite (permissions, scopes, and
/// tenant boundaries do not depend on matviews or raw-SQL indexes), but it means the test schema is
/// model-shaped, NOT byte-identical to the migrated production database. Do not assume otherwise.
///
/// Connection: derived from TEST_PG_CONNECTION (default :5432) with a unique database name, so the
/// suite is isolated from dev/other test DBs. The connection string and JWT settings are pushed via
/// environment variables set BEFORE the host is created, because Program.cs builds its Npgsql data
/// source eagerly from configuration in top-level code (UseSetting/ConfigureAppConfiguration would be
/// read too late).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestSecretKey = "step10-security-suite-signing-key-min-32-bytes-long!!";
    private const string TestIssuer = "SchoolPortalTest";
    private const string TestAudience = "SchoolPortalTestClient";

    private readonly string _baseConnection =
        Environment.GetEnvironmentVariable("TEST_PG_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=schoolport_test;Username=postgres;Password=postgres";

    private readonly string _dbName = "t_api_" + Guid.NewGuid().ToString("N");
    private string _appConnection = "";

    public async Task InitializeAsync()
    {
        // Create the throwaway database on the configured server.
        var adminCsb = new NpgsqlConnectionStringBuilder(_baseConnection) { Database = "postgres" };
        await using (var admin = new NpgsqlConnection(adminCsb.ConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{_dbName}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        _appConnection = new NpgsqlConnectionStringBuilder(_baseConnection) { Database = _dbName }.ConnectionString;

        // Pushed via env vars so Program.cs's eager data-source build picks them up. The "Testing"
        // environment switches startup to EnsureCreated (see Program.cs) + seeds the catalogue, so
        // real permission resolution works.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _appConnection);
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", TestSecretKey);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience", TestAudience);

        // Force the host to build now (runs startup migrate→EnsureCreated + seeds).
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        var adminCsb = new NpgsqlConnectionStringBuilder(_baseConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminCsb.ConnectionString);
        await admin.OpenAsync();
        // Terminate lingering connections then drop the throwaway DB.
        await using (var term = admin.CreateCommand())
        {
            term.CommandText =
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_dbName}' AND pid <> pg_backend_pid()";
            await term.ExecuteNonQueryAsync();
        }
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_dbName}\"";
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>Runs an action inside a fresh service scope with the app's real DbContext.</summary>
    public async Task<T> WithScopeAsync<T>(Func<SchoolPortalDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolPortalDbContext>();
        return await work(db);
    }

    public Task WithScopeAsync(Func<SchoolPortalDbContext, Task> work) =>
        WithScopeAsync(async db => { await work(db); return true; });

    /// <summary>
    /// Seeds a User (BCrypt password) with the given identity and active positions, then returns a
    /// real access token by calling the production <see cref="AuthService.LoginAsync"/> — full path:
    /// password verify → IssueTokens → claims → signing → tiered TTL.
    /// </summary>
    public async Task<SeededUser> MintTokenAsync(
        Guid schoolId,
        string identity,
        params string[] positionKeys)
    {
        const string password = "Passw0rd!";
        var userId = Guid.NewGuid();
        var email = $"u_{userId:N}@test.local";

        await WithScopeAsync(async db =>
        {
            // The school must exist (FK + tenant). Create on demand; reuse if already seeded.
            if (!await db.Schools.AnyAsync(s => s.SchoolId == schoolId))
                db.Schools.Add(new School { SchoolId = schoolId, Name = "Test School " + schoolId.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });

            db.Users.Add(new User
            {
                UserId = userId,
                SchoolId = schoolId,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "Test",
                LastName = identity,
                Role = identity == "Staff" ? "Admin" : identity,   // legacy claim; identity drives auth
                Identity = identity,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

            foreach (var key in positionKeys)
            {
                var positionId = await db.Positions.Where(p => p.Key == key).Select(p => p.PositionId).FirstAsync();
                db.UserPositions.Add(new UserPosition
                {
                    UserPositionId = Guid.NewGuid(),
                    SchoolId = schoolId,
                    UserId = userId,
                    PositionId = positionId,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        });

        // Real token-issuing path.
        using var scope = Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var login = await auth.LoginAsync(new LoginRequest { Email = email, Password = password });

        return new SeededUser(userId, schoolId, email, login.AccessToken, login.ExpiresAt);
    }

    /// <summary>
    /// Mints a Parent holder AND seeds a real child (a Student whose <c>ParentUserId</c> is the parent)
    /// in the same school, returning both. Lets the parent-child read endpoints be exercised against an
    /// OWNED child id — proving the true holder contract (own child → 2xx) instead of accidentally
    /// tripping the <c>IsMyChild</c> ownership guard with a random id.
    /// </summary>
    public async Task<(SeededUser Parent, Guid ChildStudentId)> MintParentWithChildAsync()
    {
        var schoolId = Guid.NewGuid();
        var parent = await MintTokenAsync(schoolId, "Parent");

        var childStudentId = Guid.NewGuid();
        await WithScopeAsync(async db =>
        {
            var childUserId = Guid.NewGuid();
            db.Users.Add(new User
            {
                UserId = childUserId,
                SchoolId = schoolId,
                Email = $"child_{childUserId:N}@test.local",
                PasswordHash = "x",
                FirstName = "Child",
                LastName = "Of" + parent.UserId.ToString("N")[..6],
                Role = "Student",
                Identity = "Learner",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            db.Students.Add(new Student
            {
                StudentId = childStudentId,
                SchoolId = schoolId,
                UserId = childUserId,
                ParentUserId = parent.UserId,
                StudentNumber = "N" + childUserId.ToString("N")[..6],
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        return (parent, childStudentId);
    }

    /// <summary>An <see cref="HttpClient"/> with the Bearer token preset (no auto-redirect, so we see 401/403/3xx as-is).</summary>
    public HttpClient ClientFor(SeededUser user)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.AccessToken);
        return client;
    }

    /// <summary>An anonymous client (no Authorization header).</summary>
    public HttpClient AnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

public sealed record SeededUser(Guid UserId, Guid SchoolId, string Email, string AccessToken, DateTime ExpiresAt);

[CollectionDefinition("SecurityApi")]
public sealed class SecurityApiCollection : ICollectionFixture<ApiFactory> { }
