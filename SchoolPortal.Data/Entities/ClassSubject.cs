namespace SchoolPortal.Data.Entities;

public class ClassSubject
{
    public int ClassSubjectId { get; set; }
    public int ClassId { get; set; }
    public int SubjectId { get; set; }
    public int? TeacherId { get; set; }
    public int SchoolId { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual Class Class { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual Teacher? Teacher { get; set; }
    public virtual School School { get; set; } = null!;
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
