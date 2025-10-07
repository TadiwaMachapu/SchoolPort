namespace SchoolPortal.Shared.DTOs.Announcements;

public class AnnouncementDto
{
    public int AnnouncementId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string? AudienceValue { get; set; }
    public string CreatedByName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}
