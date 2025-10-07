namespace SchoolPortal.Shared.DTOs.Assignments;

public class CreateAssignmentRequest
{
    public int ClassSubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public decimal MaxMarks { get; set; }
}
