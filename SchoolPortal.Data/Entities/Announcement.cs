namespace SchoolPortal.Data.Entities;

public class Announcement
{
    public int AnnouncementId { get; set; }
    public int SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Audience { get; set; } = null!; // All, Grade, Class
    public string? AudienceValue { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
