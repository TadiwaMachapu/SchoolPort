namespace SchoolPortal.Data.Entities;

public class WhatsAppLog
{
    public Guid WhatsAppLogId { get; set; }
    public Guid SchoolId { get; set; }
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public string TriggerType { get; set; } = null!; // Absence, FeeReminder, Announcement, Manual, Test
    public string MessageBody { get; set; } = null!;
    public string Status { get; set; } = null!; // Queued, Simulated, Sent, Failed
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual School School { get; set; } = null!;
}
