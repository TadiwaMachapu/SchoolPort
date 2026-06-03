using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Data.Entities;

public class School
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
    public string? BrandingLogoUrl { get; set; }
    public string? BrandingPrimaryColor { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Per-school feature flags — controls which modules are enabled
    public SchoolFeatures Features { get; set; } = new();

    // Per-school branding / white-label theme
    public SchoolTheme Theme { get; set; } = new();

    // Per-school configuration settings
    public SchoolSettings Settings { get; set; } = new();

    // Navigation properties
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public virtual ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
}
