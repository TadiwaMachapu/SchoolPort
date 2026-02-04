namespace SchoolPortal.Data.Entities;

public class Announcement
{
    public Guid AnnouncementId { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Audience { get; set; } = null!; // All, Grade, Class
    public string? AudienceValue { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
