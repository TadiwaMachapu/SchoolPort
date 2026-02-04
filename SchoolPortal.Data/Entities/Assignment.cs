namespace SchoolPortal.Data.Entities;

public class Assignment
{
    public Guid AssignmentId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public decimal MaxMarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual ClassSubject ClassSubject { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
