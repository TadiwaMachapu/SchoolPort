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
}

public class SchoolFeatures
{
    public bool Quizzes { get; set; } = true;
    public bool Attendance { get; set; } = true;
    public bool ParentPortal { get; set; } = true;
    public bool Messaging { get; set; } = true;
    public bool Courses { get; set; } = true;
    public bool Analytics { get; set; } = true;
    public bool AiGrading { get; set; } = false;
    public bool PlagiarismDetection { get; set; } = false;
    public bool Sso { get; set; } = false;
    public bool CustomReports { get; set; } = false;
    public bool WhiteLabel { get; set; } = false;
    public bool PluginApi { get; set; } = false;
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

public class UpdateSchoolFeaturesRequest
{
    public bool Quizzes { get; set; }
    public bool Attendance { get; set; }
    public bool ParentPortal { get; set; }
    public bool Messaging { get; set; }
    public bool Courses { get; set; }
    public bool Analytics { get; set; }
    public bool AiGrading { get; set; }
    public bool PlagiarismDetection { get; set; }
    public bool Sso { get; set; }
    public bool CustomReports { get; set; }
    public bool WhiteLabel { get; set; }
    public bool PluginApi { get; set; }
}
