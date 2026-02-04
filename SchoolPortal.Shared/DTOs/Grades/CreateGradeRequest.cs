namespace SchoolPortal.Shared.DTOs.Grades;

public class CreateGradeRequest
{
    public Guid SubmissionId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
}
