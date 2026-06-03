namespace SchoolPortal.Data.Entities;

public class Activity
{
    public Guid ActivityId { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string ActivityType { get; set; } = null!;
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual ICollection<ActivityParticipant> Participants { get; set; } = new List<ActivityParticipant>();
}
