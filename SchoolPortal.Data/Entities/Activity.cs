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

    /// <summary>The SportCultureMIC who owns/runs this activity (Sprint 1.5.0 Step 7). Null =
    /// unassigned (transitional): null-owner activities are visible to all the school's MICs until
    /// an owner is set. New activities are owned by their creator.</summary>
    public Guid? OwnerUserId { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual User? OwnerUser { get; set; }
    public virtual ICollection<ActivityParticipant> Participants { get; set; } = new List<ActivityParticipant>();
}
