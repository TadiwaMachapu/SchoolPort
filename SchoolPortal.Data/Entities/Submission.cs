namespace SchoolPortal.Data.Entities;

public class Submission
{
    public int SubmissionId { get; set; }
    public int AssignmentId { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
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
