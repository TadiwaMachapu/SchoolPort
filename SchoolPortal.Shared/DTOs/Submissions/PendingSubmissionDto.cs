namespace SchoolPortal.Shared.DTOs.Submissions;

public class PendingSubmissionDto
{
    public Guid SubmissionId { get; set; }
    public Guid AssignmentId { get; set; }
    public string AssignmentTitle { get; set; } = null!;
    public decimal MaxMarks { get; set; }
    public string StudentName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public bool HasFile { get; set; }
}
