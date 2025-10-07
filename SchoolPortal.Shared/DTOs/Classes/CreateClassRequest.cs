namespace SchoolPortal.Shared.DTOs.Classes;

public class CreateClassRequest
{
    public string Name { get; set; } = null!;
    public int? GradeLevel { get; set; }
    public int? AcademicYear { get; set; }
    public int? TeacherId { get; set; }
    public int? MaxCapacity { get; set; }
}
