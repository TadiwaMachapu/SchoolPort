using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Schools;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SchoolPortal.Server.Services;

public interface ISuperAdminService
{
    Task<SuperAdminLoginResponse> LoginAsync(string email, string password);
    Task<List<SchoolSummaryDto>> GetAllSchoolsAsync();
    Task<SchoolSummaryDto> CreateSchoolAsync(CreateSchoolRequest request);
    Task<SchoolSummaryDto> UpdateFeaturesAsync(Guid schoolId, UpdateSchoolFeaturesRequest request);
    Task<SchoolSummaryDto> SetStatusAsync(Guid schoolId, bool isActive);
    Task<PlatformStatsDto> GetStatsAsync();
}

public class SuperAdminService : ISuperAdminService
{
    private readonly SchoolPortalDbContext _context;
    private readonly IConfiguration _config;
    private readonly IAuthService _authService;

    public SuperAdminService(SchoolPortalDbContext context, IConfiguration config, IAuthService authService)
    {
        _context = context;
        _config = config;
        _authService = authService;
    }

    public async Task<SuperAdminLoginResponse> LoginAsync(string email, string password)
    {
        var admin = await _context.SuperAdmins
            .FirstOrDefaultAsync(a => a.Email == email.ToLower() && a.IsActive)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        admin.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = GenerateToken(admin);
        return new SuperAdminLoginResponse
        {
            AccessToken = token,
            SuperAdmin = new SuperAdminDto
            {
                SuperAdminId = admin.SuperAdminId,
                Email = admin.Email,
                FirstName = admin.FirstName,
                LastName = admin.LastName
            }
        };
    }

    public async Task<List<SchoolSummaryDto>> GetAllSchoolsAsync()
    {
        return await _context.Schools
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SchoolSummaryDto
            {
                SchoolId = s.SchoolId,
                Name = s.Name,
                Domain = s.Domain,
                IsActive = s.IsActive,
                Features = s.Features,
                CreatedAt = s.CreatedAt,
                UserCount = s.Users.Count(u => u.IsActive),
                ClassCount = s.Classes.Count()
            })
            .ToListAsync();
    }

    public async Task<SchoolSummaryDto> CreateSchoolAsync(CreateSchoolRequest request)
    {
        var school = new School
        {
            Name = request.Name.Trim(),
            Domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Features = request.Features ?? new SchoolFeatures()
        };
        _context.Schools.Add(school);
        await _context.SaveChangesAsync();

        // Create the first admin user
        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword ?? "Admin@123");
            var user = new User
            {
                SchoolId = school.SchoolId,
                Email = request.AdminEmail.ToLower().Trim(),
                PasswordHash = hash,
                FirstName = request.AdminFirstName ?? "Admin",
                LastName = request.AdminLastName ?? "User",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        return new SchoolSummaryDto
        {
            SchoolId = school.SchoolId,
            Name = school.Name,
            Domain = school.Domain,
            IsActive = school.IsActive,
            Features = school.Features,
            CreatedAt = school.CreatedAt,
            UserCount = 0,
            ClassCount = 0
        };
    }

    public async Task<SchoolSummaryDto> UpdateFeaturesAsync(Guid schoolId, UpdateSchoolFeaturesRequest request)
    {
        var school = await _context.Schools.FindAsync(schoolId)
            ?? throw new KeyNotFoundException("School not found");

        school.Features.Gradebook = request.Gradebook;
        school.Features.VirtualClassroom = request.VirtualClassroom;
        school.Features.SmartReports = request.SmartReports;
        school.Features.SaSamsExport = request.SaSamsExport;
        school.Features.SkillsProfile = request.SkillsProfile;
        school.Features.Pathways = request.Pathways;
        school.Features.MatricHub = request.MatricHub;
        school.Features.SportsCulture = request.SportsCulture;
        school.Features.SchoolPay = request.SchoolPay;
        school.Features.SchoolChat = request.SchoolChat;
        school.Features.WhatsApp = request.WhatsApp;
        school.Features.PopiaCentre = request.PopiaCentre;
        school.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new SchoolSummaryDto
        {
            SchoolId = school.SchoolId,
            Name = school.Name,
            Domain = school.Domain,
            IsActive = school.IsActive,
            Features = school.Features,
            CreatedAt = school.CreatedAt,
            UserCount = await _context.Users.CountAsync(u => u.SchoolId == schoolId && u.IsActive),
            ClassCount = await _context.Classes.CountAsync(c => c.SchoolId == schoolId)
        };
    }

    public async Task<SchoolSummaryDto> SetStatusAsync(Guid schoolId, bool isActive)
    {
        var school = await _context.Schools.FindAsync(schoolId)
            ?? throw new KeyNotFoundException("School not found");
        school.IsActive = isActive;
        school.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new SchoolSummaryDto
        {
            SchoolId = school.SchoolId,
            Name = school.Name,
            Domain = school.Domain,
            IsActive = school.IsActive,
            Features = school.Features,
            CreatedAt = school.CreatedAt,
            UserCount = await _context.Users.CountAsync(u => u.SchoolId == schoolId && u.IsActive),
            ClassCount = await _context.Classes.CountAsync(c => c.SchoolId == schoolId)
        };
    }

    public async Task<PlatformStatsDto> GetStatsAsync()
    {
        return new PlatformStatsDto
        {
            TotalSchools = await _context.Schools.CountAsync(),
            ActiveSchools = await _context.Schools.CountAsync(s => s.IsActive),
            TotalUsers = await _context.Users.CountAsync(),
            TotalStudents = await _context.Students.CountAsync(),
            TotalTeachers = await _context.Teachers.CountAsync()
        };
    }

    private string GenerateToken(SuperAdmin admin)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.SuperAdminId.ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email),
            new(ClaimTypes.Role, "SuperAdmin"),
            new("firstName", admin.FirstName),
            new("lastName", admin.LastName),
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
