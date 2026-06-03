namespace SchoolPortal.Data.Entities;

public class SkillEntry
{
    public Guid SkillEntryId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public Guid? EndorsedByUserId { get; set; }
    public DateTime? EndorsedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Student Student { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User? EndorsedByUser { get; set; }
}
