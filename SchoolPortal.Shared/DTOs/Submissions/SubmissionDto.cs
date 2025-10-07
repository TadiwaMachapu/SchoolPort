namespace SchoolPortal.Shared.DTOs.Submissions;

public class SubmissionDto
{
    public int SubmissionId { get; set; }
    public int AssignmentId { get; set; }
    public int StudentId { get; set; }
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
    public int GradeId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime GradedAt { get; set; }
}
