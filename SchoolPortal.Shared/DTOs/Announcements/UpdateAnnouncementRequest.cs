namespace SchoolPortal.Shared.DTOs.Announcements;

public class UpdateAnnouncementRequest
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string? AudienceValue { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}
