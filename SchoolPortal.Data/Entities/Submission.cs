namespace SchoolPortal.Data.Entities;

public class Submission
{
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? Comments { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual Assignment Assignment { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual Grade? Grade { get; set; }
}
