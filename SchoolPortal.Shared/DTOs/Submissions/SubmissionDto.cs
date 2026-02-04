namespace SchoolPortal.Shared.DTOs.Submissions;

public class SubmissionDto
{
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentNumber { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? Comments { get; set; }
    public GradeInfo? Grade { get; set; }
}

public class GradeInfo
{
    public Guid GradeId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime GradedAt { get; set; }
}
