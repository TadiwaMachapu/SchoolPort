using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Schools;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SchoolPortal.Server.Services;

public interface ISuperAdminService
{
    Task<SuperAdminLoginResponse> LoginAsync(string email, string password);
    Task<List<SchoolSummaryDto>> GetAllSchoolsAsync();
    Task<SchoolSummaryDto> CreateSchoolAsync(CreateSchoolRequest request);
    Task<SchoolSummaryDto> UpdateFeaturesAsync(Guid schoolId, UpdateSchoolFeaturesRequest request);
    Task<SchoolSummaryDto> SetStatusAsync(Guid schoolId, bool isActive, string? reason);
    Task<PlatformStatsDto> GetStatsAsync();
    Task<PagedResult<SuperAdminAuditLogDto>> GetAuditLogAsync(
        Guid? schoolId, string? actionType, DateTime? from, DateTime? to, int page, int pageSize);
}

public class SuperAdminService : ISuperAdminService
{
    private readonly SchoolPortalDbContext _context;
    private readonly IConfiguration _config;
    private readonly IAuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SuperAdminService(SchoolPortalDbContext context, IConfiguration config, IAuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _config = config;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
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
            SchoolId = Guid.NewGuid(),   // set client-side so the User + audit FKs are known pre-insert
            Name = request.Name.Trim(),
            Domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Features = request.Features ?? new SchoolFeatures()
        };
        _context.Schools.Add(school);

        // Create the first admin user (staged, not yet saved — see the single SaveChanges below).
        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword ?? "Admin@123");
            _context.Users.Add(new User
            {
                SchoolId = school.SchoolId,
                Email = request.AdminEmail.ToLower().Trim(),
                PasswordHash = hash,
                FirstName = request.AdminFirstName ?? "Admin",
                LastName = request.AdminLastName ?? "User",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Audit + school + first user commit in ONE SaveChanges → atomic creation. (Previously two
        // saves: if the user insert failed, the school was already committed as an orphan. Pinned by
        // CreateSchool_UserCreationFails_SchoolCreationRollsBackToo.)
        AddAudit(SuperAdminAuditActions.SchoolCreated, school.SchoolId,
            previousValue: null,
            newValue: JsonSerializer.Serialize(new
            {
                name = school.Name,
                domain = school.Domain,
                isActive = school.IsActive,
                features = school.Features,
                adminEmail = string.IsNullOrWhiteSpace(request.AdminEmail) ? null : request.AdminEmail.ToLower().Trim(),
            }));
        await _context.SaveChangesAsync();

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

        // Per-flag diff computed from the CURRENT features BEFORE overwriting them; audit only when
        // at least one flag actually changed (a no-op save writes no row).
        var (prevJson, newJson, changed) = DiffFeatures(school.Features, request);
        if (changed > 0)
            AddAudit(SuperAdminAuditActions.SchoolFeaturesUpdated, schoolId, prevJson, newJson);

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

    public async Task<SchoolSummaryDto> SetStatusAsync(Guid schoolId, bool isActive, string? reason)
    {
        var school = await _context.Schools.FindAsync(schoolId)
            ?? throw new KeyNotFoundException("School not found");

        if (school.IsActive != isActive)   // no-op guard: setting the same status writes no audit row
            AddAudit(SuperAdminAuditActions.SchoolStatusChanged, schoolId,
                JsonSerializer.Serialize(new { isActive = school.IsActive }),
                JsonSerializer.Serialize(new { isActive }),
                reason);

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

    public async Task<PagedResult<SuperAdminAuditLogDto>> GetAuditLogAsync(
        Guid? schoolId, string? actionType, DateTime? from, DateTime? to, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var query = _context.SuperAdminAuditLogs.AsNoTracking();
        if (schoolId is { } sid) query = query.Where(a => a.TargetSchoolId == sid);
        if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(a => a.ActionType == actionType);
        if (from is { } f) query = query.Where(a => a.CreatedAt >= f);
        if (to is { } t) query = query.Where(a => a.CreatedAt <= t);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new SuperAdminAuditLogDto
            {
                AuditId = a.AuditId,
                SuperAdminId = a.SuperAdminId,
                SuperAdminName = a.SuperAdmin.FirstName + " " + a.SuperAdmin.LastName,
                SuperAdminEmail = a.SuperAdmin.Email,
                ActionType = a.ActionType,
                TargetSchoolId = a.TargetSchoolId,
                TargetSchoolName = a.TargetSchool != null ? a.TargetSchool.Name : null,
                PreviousValue = a.PreviousValue,
                NewValue = a.NewValue,
                Reason = a.Reason,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync();

        return new PagedResult<SuperAdminAuditLogDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    // ── Audit helpers ──────────────────────────────────────────────
    private Guid CurrentSuperAdminId()
    {
        var sub = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("No super-admin identity on the request.");
    }

    // Stages an audit row on the context — committed by the CALLER's own SaveChangesAsync, so the
    // log row and its effect are one atomic transaction (the MarkCaptureService pattern). Adding a
    // new mutating SuperAdmin action = one AddAudit(...) line before its SaveChanges.
    private void AddAudit(string actionType, Guid? targetSchoolId, string? previousValue, string? newValue, string? reason = null)
        => _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
        {
            AuditId = Guid.NewGuid(),
            SuperAdminId = CurrentSuperAdminId(),
            ActionType = actionType,
            TargetSchoolId = targetSchoolId,
            PreviousValue = previousValue,
            NewValue = newValue,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
        });

    // The 12 feature flags with their camelCase audit names + before/after getters.
    private static readonly (string Name, Func<SchoolFeatures, bool> Before, Func<UpdateSchoolFeaturesRequest, bool> After)[] FeatureFlags =
    {
        ("gradebook",        f => f.Gradebook,        r => r.Gradebook),
        ("virtualClassroom", f => f.VirtualClassroom, r => r.VirtualClassroom),
        ("smartReports",     f => f.SmartReports,     r => r.SmartReports),
        ("saSamsExport",     f => f.SaSamsExport,     r => r.SaSamsExport),
        ("skillsProfile",    f => f.SkillsProfile,    r => r.SkillsProfile),
        ("pathways",         f => f.Pathways,         r => r.Pathways),
        ("matricHub",        f => f.MatricHub,        r => r.MatricHub),
        ("sportsCulture",    f => f.SportsCulture,    r => r.SportsCulture),
        ("schoolPay",        f => f.SchoolPay,        r => r.SchoolPay),
        ("schoolChat",       f => f.SchoolChat,       r => r.SchoolChat),
        ("whatsApp",         f => f.WhatsApp,         r => r.WhatsApp),
        ("popiaCentre",      f => f.PopiaCentre,      r => r.PopiaCentre),
    };

    // Compact per-flag diff: PreviousValue/NewValue carry ONLY the flags that changed, so a reader
    // sees exactly "virtualClassroom: false → true", not a 12-field blob to diff by hand.
    private static (string? Prev, string? Next, int Changed) DiffFeatures(SchoolFeatures before, UpdateSchoolFeaturesRequest after)
    {
        var prev = new Dictionary<string, bool>();
        var next = new Dictionary<string, bool>();
        foreach (var (name, getBefore, getAfter) in FeatureFlags)
        {
            bool b = getBefore(before), a = getAfter(after);
            if (b != a) { prev[name] = b; next[name] = a; }
        }
        return prev.Count == 0
            ? (null, null, 0)
            : (JsonSerializer.Serialize(prev), JsonSerializer.Serialize(next), prev.Count);
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
