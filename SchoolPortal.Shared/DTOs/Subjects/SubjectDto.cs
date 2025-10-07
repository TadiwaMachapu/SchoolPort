namespace SchoolPortal.Shared.DTOs.Subjects;

public class SubjectDto
{
    public int SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Description { get; set; }
}
