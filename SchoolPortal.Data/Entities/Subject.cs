namespace SchoolPortal.Data.Entities;

public class Subject
{
    public Guid SubjectId { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Description { get; set; }
    // "SeniorPhase" (Gr 7–9) | "FET" (Gr 10–12) | null (applies to both)
    public string? CapsPhase { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
}
