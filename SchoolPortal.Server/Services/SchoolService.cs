using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Server.Services;

public interface ISchoolService
{
    Task<SchoolDto> GetCurrentSchoolAsync();
    Task<SchoolDto> UpdateInfoAsync(UpdateSchoolInfoRequest request);
    Task<SchoolDto> UpdateThemeAsync(UpdateSchoolThemeRequest request);
    Task<SchoolDto> UpdateFeaturesAsync(UpdateSchoolFeaturesRequest request);
    Task<SchoolSettings> GetSettingsAsync();
    Task<SchoolSettings> UpdateSettingsAsync(UpdateSchoolSettingsRequest request);
    Task<SchoolSettings> ApplySizePresetAsync(string preset);
}

public class SchoolService : ISchoolService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SchoolService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SchoolDto> GetCurrentSchoolAsync()
    {
        var school = await _context.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        return MapToDto(school);
    }

    public async Task<SchoolDto> UpdateInfoAsync(UpdateSchoolInfoRequest request)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        school.Name = request.Name.Trim();
        school.Domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim();
        school.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(school);
    }

    public async Task<SchoolDto> UpdateThemeAsync(UpdateSchoolThemeRequest request)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        school.Theme ??= new();
        school.Theme.PrimaryColor = request.PrimaryColor;
        school.Theme.LogoUrl = request.LogoUrl;
        school.Theme.FaviconUrl = request.FaviconUrl;
        school.Theme.FontFamily = request.FontFamily;
        school.Theme.WelcomeMessage = request.WelcomeMessage;
        school.Theme.SupportEmail = request.SupportEmail;
        school.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(school);
    }

    public async Task<SchoolDto> UpdateFeaturesAsync(UpdateSchoolFeaturesRequest request)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
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
        return MapToDto(school);
    }

    public async Task<SchoolSettings> GetSettingsAsync()
    {
        var school = await _context.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");
        return school.Settings ?? new SchoolSettings();
    }

    public async Task<SchoolSettings> UpdateSettingsAsync(UpdateSchoolSettingsRequest request)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");
        school.Settings ??= new();
        if (request.GradingScale != null) school.Settings.GradingScale = request.GradingScale;
        if (request.AcademicTerms != null) school.Settings.AcademicTerms = request.AcademicTerms;
        if (request.LatePolicy != null) school.Settings.LatePolicy = request.LatePolicy;
        if (request.StudentIdConfig != null) school.Settings.StudentIdConfig = request.StudentIdConfig;
        if (request.Timezone != null) school.Settings.Timezone = request.Timezone;
        if (request.Locale != null) school.Settings.Locale = request.Locale;
        school.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return school.Settings;
    }

    public async Task<SchoolSettings> ApplySizePresetAsync(string preset)
    {
        if (!Seeds.SizePresets.IsValid(preset))
            throw new ArgumentException($"Unknown size preset '{preset}'. Expected Compact, Standard, or Large.");

        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        school.Settings ??= new();
        school.Settings.SizePreset = preset.Trim();
        // ADVISORY default set (D2) — not a hard gate; the position UI/CSV may assign beyond this.
        school.Settings.EnabledPositionKeys = Seeds.SizePresets.KeysFor(preset).ToList();
        school.UpdatedAt = DateTime.UtcNow;

        // jsonb column: force the whole Settings document to persist (in-place nested mutation
        // isn't always picked up by change tracking).
        _context.Entry(school).Property(s => s.Settings).IsModified = true;
        await _context.SaveChangesAsync();
        return school.Settings;
    }

    private static SchoolDto MapToDto(Data.Entities.School school) => new()
    {
        SchoolId = school.SchoolId,
        Name = school.Name,
        Domain = school.Domain,
        BrandingLogoUrl = school.BrandingLogoUrl,
        BrandingPrimaryColor = school.BrandingPrimaryColor,
        IsActive = school.IsActive,
        Features = school.Features,
        Theme = school.Theme,
        Settings = school.Settings
    };
}
