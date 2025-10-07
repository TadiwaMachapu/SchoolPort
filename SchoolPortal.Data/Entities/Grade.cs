namespace SchoolPortal.Data.Entities;

public class Grade
{
    public int GradeId { get; set; }
    public int SubmissionId { get; set; }
    public int SchoolId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
    public int GradedByUserId { get; set; }
    public DateTime GradedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual Submission Submission { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User GradedByUser { get; set; } = null!;
}
