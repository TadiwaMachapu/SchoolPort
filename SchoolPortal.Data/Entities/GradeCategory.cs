namespace SchoolPortal.Data.Entities;

public class GradeCategory
{
    public Guid CategoryId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public string Name { get; set; } = null!; // e.g. "Tests", "Homework", "Projects"
    public decimal Weight { get; set; }         // e.g. 0.4 = 40%
    public DateTime CreatedAt { get; set; }

    public virtual ClassSubject ClassSubject { get; set; } = null!;
}
