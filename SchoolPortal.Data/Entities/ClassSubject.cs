namespace SchoolPortal.Data.Entities;

public class ClassSubject
{
    public Guid ClassSubjectId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid SchoolId { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual Class Class { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual Teacher? Teacher { get; set; }
    public virtual School School { get; set; } = null!;
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
