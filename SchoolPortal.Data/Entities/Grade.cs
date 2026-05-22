namespace SchoolPortal.Data.Entities;

public class Grade
{
    public Guid GradeId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SchoolId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
    public Guid GradedByUserId { get; set; }
    public DateTime GradedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual Submission Submission { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User GradedByUser { get; set; } = null!;
}
