namespace SchoolPortal.Shared.DTOs.Schools;

public class SchoolDto
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
    public string? BrandingLogoUrl { get; set; }
    public string? BrandingPrimaryColor { get; set; }
    public bool IsActive { get; set; }
    public SchoolFeatures Features { get; set; } = new();
    public SchoolTheme Theme { get; set; } = new();
    public SchoolSettings? Settings { get; set; }
}

public class SchoolFeatures
{
    // Classroom pillar
    public bool Gradebook { get; set; } = false;
    public bool VirtualClassroom { get; set; } = false;

    // Reports & Insights pillar
    public bool SmartReports { get; set; } = false;
    public bool SaSamsExport { get; set; } = false;

    // Pathways pillar
    public bool SkillsProfile { get; set; } = false;
    public bool Pathways { get; set; } = false;
    public bool MatricHub { get; set; } = false;

    // Life at School pillar
    public bool SportsCulture { get; set; } = false;
    public bool SchoolPay { get; set; } = false;

    // Connect pillar
    public bool SchoolChat { get; set; } = false;
    public bool WhatsApp { get; set; } = false;
    public bool PopiaCentre { get; set; } = false;
}

public class SchoolTheme
{
    public string PrimaryColor { get; set; } = "#1E40AF";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string FontFamily { get; set; } = "Inter";
    public string? CustomDomain { get; set; }
    public string? WelcomeMessage { get; set; }
    public string? SupportEmail { get; set; }
}

public class UpdateSchoolThemeRequest
{
    public string PrimaryColor { get; set; } = "#1E40AF";
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string FontFamily { get; set; } = "Inter";
    public string? WelcomeMessage { get; set; }
    public string? SupportEmail { get; set; }
}

public class UpdateSchoolInfoRequest
{
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
}

public class UpdateSchoolFeaturesRequest
{
    // Classroom pillar
    public bool Gradebook { get; set; }
    public bool VirtualClassroom { get; set; }

    // Reports & Insights pillar
    public bool SmartReports { get; set; }
    public bool SaSamsExport { get; set; }

    // Pathways pillar
    public bool SkillsProfile { get; set; }
    public bool Pathways { get; set; }
    public bool MatricHub { get; set; }

    // Life at School pillar
    public bool SportsCulture { get; set; }
    public bool SchoolPay { get; set; }

    // Connect pillar
    public bool SchoolChat { get; set; }
    public bool WhatsApp { get; set; }
    public bool PopiaCentre { get; set; }
}

public class SchoolSettings
{
    public List<GradeScaleEntry> GradingScale { get; set; } = new()
    {
        new("A+", 97, 100), new("A", 93, 96), new("A-", 90, 92),
        new("B+", 87, 89), new("B", 83, 86), new("B-", 80, 82),
        new("C+", 77, 79), new("C", 73, 76), new("C-", 70, 72),
        new("D", 60, 69), new("F", 0, 59)
    };
    public List<AcademicTerm> AcademicTerms { get; set; } = new();
    public LatePolicy LatePolicy { get; set; } = new();
    public StudentIdConfig StudentIdConfig { get; set; } = new();
    public string Timezone { get; set; } = "UTC";
    public string Locale { get; set; } = "en-US";
    public WhatsAppConfig WhatsApp { get; set; } = new();
    public decimal AiMonthlyCostCapZar { get; set; } = 100.00m;

    // Sprint 1.5.0 Step 9 — onboarding size preset. SizePreset records the chosen starting point;
    // EnabledPositionKeys is the preset's DEFAULT seeded set (ADVISORY ONLY — the position UI/CSV
    // may still assign any catalogue position; the preset just drives the suggested set). Stored in
    // the Settings jsonb (no migration).
    public string? SizePreset { get; set; }                          // "Compact" | "Standard" | "Large"
    public List<string> EnabledPositionKeys { get; set; } = new();
}

public class GradeScaleEntry
{
    public string Letter { get; set; } = null!;
    public int MinPercent { get; set; }
    public int MaxPercent { get; set; }
    public GradeScaleEntry() { }
    public GradeScaleEntry(string letter, int min, int max)
    { Letter = letter; MinPercent = min; MaxPercent = max; }
}

public class AcademicTerm
{
    public Guid TermId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class LatePolicy
{
    public bool AcceptLate { get; set; } = true;
    public int GracePeriodHours { get; set; } = 24;
    public double PenaltyPercentPerDay { get; set; } = 10;
    public double MaxPenaltyPercent { get; set; } = 50;
    public bool BlockAfterMaxPenalty { get; set; } = false;
}

public class StudentIdConfig
{
    public string Prefix { get; set; } = "STU";
    public int NextNumber { get; set; } = 1001;
    public int PaddingDigits { get; set; } = 4;
    public bool IncludeYear { get; set; } = true;
}

public class WhatsAppConfig
{
    public string Provider { get; set; } = "None"; // None, Twilio, 360dialog
    public string? ApiKey { get; set; }
    public string? PhoneNumberId { get; set; }
    public bool TriggerAbsence { get; set; } = true;
    public bool TriggerFeeReminder { get; set; } = true;
    public bool TriggerAnnouncement { get; set; } = false;
    public string AbsenceTemplate { get; set; } = "Hi {ParentName}, {LearnerName} was marked absent on {Date}. Please contact the school if this is incorrect.";
    public string FeeReminderTemplate { get; set; } = "Hi {ParentName}, a reminder that {FeeName} of R{Amount} is due on {DueDate} for {LearnerName}.";
    public string AnnouncementTemplate { get; set; } = "Message from {SchoolName}: {Title}. {Body}";
}

public class UpdateSchoolSettingsRequest
{
    public List<GradeScaleEntry>? GradingScale { get; set; }
    public List<AcademicTerm>? AcademicTerms { get; set; }
    public LatePolicy? LatePolicy { get; set; }
    public StudentIdConfig? StudentIdConfig { get; set; }
    public string? Timezone { get; set; }
    public string? Locale { get; set; }
}

public class ApplySizePresetRequest
{
    public string Preset { get; set; } = null!; // Compact | Standard | Large
}

public class SchoolSummaryDto
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
    public bool IsActive { get; set; }
    public SchoolFeatures Features { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
    public int ClassCount { get; set; }
}

public class CreateSchoolRequest
{
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string? AdminFirstName { get; set; }
    public string? AdminLastName { get; set; }
    public SchoolFeatures? Features { get; set; }
}

public class SuperAdminLoginResponse
{
    public string AccessToken { get; set; } = null!;
    public SuperAdminDto SuperAdmin { get; set; } = null!;
}

public class SuperAdminDto
{
    public Guid SuperAdminId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}

public class PlatformStatsDto
{
    public int TotalSchools { get; set; }
    public int ActiveSchools { get; set; }
    public int TotalUsers { get; set; }
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
}

public class SetSchoolStatusRequest
{
    public bool IsActive { get; set; }
}

public class SuperAdminLoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}
