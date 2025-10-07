namespace SchoolPortal.Shared.DTOs.Subjects;

public class CreateSubjectRequest
{
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Description { get; set; }
}
