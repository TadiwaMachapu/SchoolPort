namespace SchoolPortal.Data.Entities;

public class Assignment
{
    public int AssignmentId { get; set; }
    public int ClassSubjectId { get; set; }
    public int SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public decimal MaxMarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual ClassSubject ClassSubject { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
