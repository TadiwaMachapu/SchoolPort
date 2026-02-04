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

            _logger.LogInformation("User found: {Found}, Email: {Email}", user != null, request.Email);
            
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Invalid credentials for email: {Email}", request.Email);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            _logger.LogInformation("Password verified for email: {Email}", request.Email);
            
            _logger.LogInformation("Generating access token...");
            var accessToken = GenerateAccessToken(user);
            _logger.LogInformation("Access token generated");
            
            var refreshToken = GenerateRefreshToken();
            _logger.LogInformation("Refresh token generated");

            // Update last login
            _logger.LogInformation("Updating last login time...");
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Last login time updated");

            _logger.LogInformation("Creating login response...");
            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(8),
                User = new UserInfo
                {
                    UserId = user.UserId,
                    SchoolId = user.SchoolId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
            throw;
        }
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        // For MVP, we'll implement a simplified refresh mechanism
        // In production, store refresh tokens in database
        throw new NotImplementedException("Refresh token not implemented in MVP");
    }

    private string GenerateAccessToken(Data.Entities.User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("schoolId", user.SchoolId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        // Allow plain-text match for seeded demo users
        if (password == passwordHash)
        {
            return true;
        }

        // For other users, verify BCrypt hash
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
