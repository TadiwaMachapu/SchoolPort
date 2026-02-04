namespace SchoolPortal.Shared.DTOs.Submissions;

public class CreateSubmissionRequest
{
    public Guid AssignmentId { get; set; }
    public string? Comments { get; set; }
}
