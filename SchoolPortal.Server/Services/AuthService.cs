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
        _logger.LogInformation("Login attempt for email: {Email}", request.Email);
        
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("User not found or inactive: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid credentials");
        }
        
        _logger.LogInformation("User found: {UserId}, verifying password...", user.UserId);
        
        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Password verification failed for user: {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        _context.Entry(user).State = EntityState.Modified;
        _context.Entry(user).Property(x => x.LastLoginAt).IsModified = true;
        await _context.SaveChangesAsync();

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            Role = user.Role,
            UserId = user.UserId,
            SchoolId = user.SchoolId
        };
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
        // For MVP, using simple hash comparison
        // In production, use BCrypt or Argon2
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
