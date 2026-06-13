using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolPortal.Data;
using SchoolPortal.Shared.DTOs.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SchoolPortal.Server.Services;

public class AuthService : IAuthService
{
    private readonly SchoolPortalDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(SchoolPortalDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Invalid credentials for email: {Email}", request.Email);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            user.LastLoginAt = DateTime.UtcNow;
            var (response, _) = await IssueTokensAsync(user);
            await _context.SaveChangesAsync();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            throw;
        }
    }

    /// <summary>
    /// Re-issues an access/refresh pair from a presented refresh token (STEP 5 Section D).
    /// Positions are re-read from the database here (via <see cref="LoadPositionsAndTokenExpiryAsync"/>),
    /// NOT taken from the old token — so a position added or revoked since the previous login
    /// propagates into the new access token rather than waiting out the old token's TTL. The
    /// presented token is rotated: revoked and linked to its successor.
    /// </summary>
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedAccessException("Refresh token is required");

        var hash = HashRefreshToken(refreshToken);
        var now = DateTime.UtcNow;

        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is null || stored.RevokedAt != null || stored.ExpiresAt <= now)
            throw new UnauthorizedAccessException("Invalid or expired refresh token");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == stored.UserId && u.IsActive);
        if (user is null)
            throw new UnauthorizedAccessException("User is no longer active");

        stored.RevokedAt = now;                          // rotate: the presented token is single-use
        var (response, newToken) = await IssueTokensAsync(user);
        stored.ReplacedByTokenId = newToken.RefreshTokenId;
        await _context.SaveChangesAsync();
        return response;
    }

    /// <summary>How long a refresh token stays valid; matches the 30-day cookie set client-side.</summary>
    private const int RefreshTokenLifetimeDays = 30;

    /// <summary>
    /// Builds an access token + a freshly-persisted (hashed) refresh token for a user, and the
    /// matching <see cref="LoginResponse"/>. Adds the refresh-token row to the context but does
    /// NOT call SaveChanges — the caller owns the transaction (login also stamps LastLoginAt;
    /// refresh also revokes the old token), so a single SaveChanges covers the whole operation.
    /// </summary>
    private async Task<(LoginResponse Response, Data.Entities.RefreshToken Token)> IssueTokensAsync(
        Data.Entities.User user)
    {
        var (positions, expiresAt) = await LoadPositionsAndTokenExpiryAsync(user);
        var accessToken = GenerateAccessToken(user, positions, expiresAt);
        var rawRefresh = GenerateRefreshToken();

        var token = new Data.Entities.RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            SchoolId = user.SchoolId,
            TokenHash = HashRefreshToken(rawRefresh),
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
            CreatedAt = DateTime.UtcNow,
        };
        _context.RefreshTokens.Add(token);

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefresh,
            ExpiresAt = expiresAt,
            User = new UserInfo
            {
                UserId = user.UserId,
                SchoolId = user.SchoolId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                Identity = user.Identity ?? string.Empty,
            }
        };
        return (response, token);
    }

    /// <summary>SHA-256 (hex) of a raw refresh token. Only the hash is ever stored, so a
    /// database read cannot recover a usable token.</summary>
    private static string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>
    /// Loads the user's ACTIVE in-window positions (with scopes) for the "pos" claim and
    /// computes the tiered token expiry (STEP 3 Section D):
    ///   Learner/Parent/routine Staff → 8h; any Finance position → 1h;
    ///   External → min(1h, EffectiveTo); System → min(30min, EffectiveTo).
    /// Tokens never outlive a time-limited appointment. Sensitive operations re-resolve
    /// from the database regardless of token contents (PermissionResolver).
    /// </summary>
    private async Task<(List<Authorization.PositionClaim> Positions, DateTime ExpiresAt)>
        LoadPositionsAndTokenExpiryAsync(Data.Entities.User user)
    {
        var now = DateTime.UtcNow;

        var rows = await _context.UserPositions.AsNoTracking()
            .Where(up => up.UserId == user.UserId
                      && up.SchoolId == user.SchoolId
                      && up.IsActive
                      && up.EffectiveFrom <= now
                      && (up.EffectiveTo == null || up.EffectiveTo >= now))
            .Select(up => new
            {
                up.Position.Key,
                up.Position.Category,
                up.Position.IsExternal,
                up.Position.IsSystem,
                up.EffectiveFrom,
                up.EffectiveTo,
                Scopes = up.Scopes.Select(s => new Authorization.ScopeClaim
                {
                    ScopeType = s.ScopeType,
                    ScopeRefId = s.ScopeRefId,
                    ScopeValue = s.ScopeValue,
                }).ToList(),
            })
            .ToListAsync();

        var positions = rows.Select(r => new Authorization.PositionClaim
        {
            Key = r.Key,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            Scopes = r.Scopes,
        }).ToList();

        var expiresAt = now.AddHours(8);
        if (rows.Any(r => r.IsSystem))
            expiresAt = now.AddMinutes(30);
        else if (rows.Any(r => r.IsExternal))
            expiresAt = now.AddHours(1);
        else if (rows.Any(r => r.Category == "Finance"))
            expiresAt = now.AddHours(1);

        // A token must never outlive a time-limited (External/System) appointment.
        var earliestElevatedEnd = rows
            .Where(r => (r.IsExternal || r.IsSystem) && r.EffectiveTo != null)
            .Select(r => r.EffectiveTo!.Value)
            .DefaultIfEmpty(DateTime.MaxValue)
            .Min();
        if (earliestElevatedEnd < expiresAt) expiresAt = earliestElevatedEnd;

        return (positions, expiresAt);
    }

    private string GenerateAccessToken(
        Data.Entities.User user,
        List<Authorization.PositionClaim> positions,
        DateTime expiresAt)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Legacy claims (sub/email/role/schoolId) unchanged for the transition.
        // New: "identity" (Layer 1) and "pos" (positions + scopes + effective dates —
        // NEVER the derived permission set; permissions are resolved server-side).
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("schoolId", user.SchoolId.ToString()),
        };
        if (!string.IsNullOrEmpty(user.Identity))
            claims.Add(new Claim("identity", user.Identity));
        if (positions.Count > 0)
            claims.Add(new Claim("pos", Authorization.PositionClaim.Serialize(positions)));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
