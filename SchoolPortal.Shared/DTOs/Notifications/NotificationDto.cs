namespace SchoolPortal.Shared.DTOs.Notifications;

public class NotificationDto
{
    public Guid NotificationId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationsResponse
{
    public List<NotificationDto> Items { get; set; } = new();
    public int UnreadCount { get; set; }
}
