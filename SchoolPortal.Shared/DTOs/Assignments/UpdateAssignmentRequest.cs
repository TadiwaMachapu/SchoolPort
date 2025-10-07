namespace SchoolPortal.Shared.DTOs.Assignments;

public class UpdateAssignmentRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public decimal MaxMarks { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
