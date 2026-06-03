namespace SchoolPortal.Data.Entities;

public class PersistedNotification
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
