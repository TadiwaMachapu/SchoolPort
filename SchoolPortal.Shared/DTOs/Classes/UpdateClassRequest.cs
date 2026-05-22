namespace SchoolPortal.Shared.DTOs.Classes;

public class UpdateClassRequest
{
    public string Name { get; set; } = null!;
    public int? GradeLevel { get; set; }
    public int? AcademicYear { get; set; }
    public Guid? TeacherId { get; set; }
    public int? MaxCapacity { get; set; }
}
