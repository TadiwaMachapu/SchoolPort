namespace SchoolPortal.Shared.DTOs.Assignments;

public class AssignmentDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public decimal MaxMarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ClassName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public string CreatedByName { get; set; } = null!;
    public byte[] RowVersion { get; set; } = null!;
}
