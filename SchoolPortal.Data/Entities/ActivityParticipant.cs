namespace SchoolPortal.Data.Entities;

public class ActivityParticipant
{
    public Guid ActivityParticipantId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Activity Activity { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
