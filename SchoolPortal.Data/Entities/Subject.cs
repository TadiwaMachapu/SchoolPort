namespace SchoolPortal.Data.Entities;

public class Subject
{
    public int SubjectId { get; set; }
    public int SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
}
