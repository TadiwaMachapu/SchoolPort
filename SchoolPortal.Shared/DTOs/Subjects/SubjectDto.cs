namespace SchoolPortal.Shared.DTOs.Subjects;

public class SubjectDto
{
    public Guid SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? CapsPhase { get; set; }
}
