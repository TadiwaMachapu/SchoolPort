using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Auth;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// STEP 5 Section D on real Postgres: token refresh re-reads positions from the database at
/// refresh time (not from the old token), and refresh tokens are single-use (rotated). The
/// "re-read from DB" behaviour is what lets a position added after login propagate without
/// waiting out the old access token's TTL.
/// </summary>
[Collection("Postgres")]
public class RefreshTokenTests
{
    private readonly PostgresFixture _pg;
    public RefreshTokenTests(PostgresFixture pg) => _pg = pg;

    private static IConfiguration JwtConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "test-secret-key-that-is-long-enough-for-hmac-sha256-0123456789",
            ["JwtSettings:Issuer"] = "schoolport-test",
            ["JwtSettings:Audience"] = "schoolport-test",
        })
        .Build();

    private static string? PosClaim(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims
            .FirstOrDefault(c => c.Type == "pos")?.Value;

    [Fact]
    public async Task Refresh_ReReadsPositionsFromDatabase_AndRotatesToken()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await PositionsSeedData.SeedAsync(db, NullLogger.Instance);

            var schoolId = Guid.NewGuid();
            db.Schools.Add(new School { SchoolId = schoolId, Name = "Test High", IsActive = true, CreatedAt = DateTime.UtcNow });
            var user = new User
            {
                UserId = Guid.NewGuid(),
                SchoolId = schoolId,
                Email = "staff@t.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pw"),
                FirstName = "Sam",
                LastName = "Staff",
                Role = "Teacher",
                Identity = "Staff",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var auth = new AuthService(db, JwtConfig(), NullLogger<AuthService>.Instance);

            // 1) Login while the user has NO positions → token carries no "pos" claim.
            var login = await auth.LoginAsync(new LoginRequest { Email = "staff@t.com", Password = "pw" });
            Assert.Null(PosClaim(login.AccessToken));

            // 2) Grant a position AFTER login (simulating an appointment between sessions).
            var position = await db.Positions.FirstAsync(p => !p.IsExternal && !p.IsSystem);
            db.UserPositions.Add(new UserPosition
            {
                UserPositionId = Guid.NewGuid(),
                SchoolId = schoolId,
                UserId = user.UserId,
                PositionId = position.PositionId,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            // 3) Refresh re-reads from the DB → the new access token now carries the position.
            var refreshed = await auth.RefreshTokenAsync(login.RefreshToken);
            var pos = PosClaim(refreshed.AccessToken);
            Assert.NotNull(pos);
            Assert.Contains(position.Key, pos);
            Assert.Equal("Staff", refreshed.User.Identity); // new token shape carries identity

            // 4) Rotation: the presented refresh token is single-use and now rejected.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => auth.RefreshTokenAsync(login.RefreshToken));
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Throws()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            var auth = new AuthService(db, JwtConfig(), NullLogger<AuthService>.Instance);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => auth.RefreshTokenAsync("not-a-real-token"));
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
