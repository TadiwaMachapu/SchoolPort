using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/auth/sso")]
public class SsoController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<SsoController> _logger;

    public SsoController(SchoolPortalDbContext context, IConfiguration config, ILogger<SsoController> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Sso", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (!result.Succeeded) return Redirect("/login?error=sso_failed");

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var firstName = result.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
        var lastName = result.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";

        return await HandleSsoLoginAsync(email!, firstName, lastName, "Google", returnUrl);
    }

    [HttpGet("microsoft")]
    public IActionResult MicrosoftLogin([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(MicrosoftCallback), "Sso", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, MicrosoftAccountDefaults.AuthenticationScheme);
    }

    [HttpGet("microsoft/callback")]
    public async Task<IActionResult> MicrosoftCallback([FromQuery] string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);
        if (!result.Succeeded) return Redirect("/login?error=sso_failed");

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? "";
        var nameParts = name.Split(' ', 2);

        return await HandleSsoLoginAsync(email!, nameParts[0], nameParts.Length > 1 ? nameParts[1] : "", "Microsoft", returnUrl);
    }

    private async Task<IActionResult> HandleSsoLoginAsync(
        string email, string firstName, string lastName, string provider, string? returnUrl)
    {
        if (string.IsNullOrEmpty(email))
            return Redirect("/login?error=no_email");

        // Find the school by email domain
        var domain = email.Split('@').Last();
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Domain == domain && s.IsActive);

        if (school == null)
        {
            _logger.LogWarning("SSO login attempt for unknown domain: {Domain}", domain);
            return Redirect("/login?error=school_not_found");
        }

        // Find or create user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.SchoolId == school.SchoolId);

        if (user == null)
        {
            // Auto-create user with Student role (admin can change it)
            user = new User
            {
                SchoolId = school.SchoolId,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // random password - SSO only
                FirstName = firstName,
                LastName = lastName,
                Role = "Student",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("SSO auto-created user {Email} via {Provider}", email, provider);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = GenerateJwt(user);
        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:3000";
        var redirect = string.IsNullOrEmpty(returnUrl) ? "/dashboard" : returnUrl;

        // Redirect to frontend with token in query (frontend stores it in cookie)
        return Redirect($"{frontendUrl}/auth/sso-callback?token={Uri.EscapeDataString(token)}&redirect={Uri.EscapeDataString(redirect)}");
    }

    private string GenerateJwt(User user)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("schoolId", user.SchoolId.ToString())
            },
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
