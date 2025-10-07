namespace SchoolPortal.Shared.DTOs.Announcements;

public class CreateAnnouncementRequest
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Audience { get; set; } = null!; // All, Grade, Class
    public string? AudienceValue { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
