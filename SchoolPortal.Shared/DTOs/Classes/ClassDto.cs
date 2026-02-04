namespace SchoolPortal.Shared.DTOs.Classes;

public class ClassDto
{
    public Guid ClassId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? GradeLevel { get; set; }
    public int? AcademicYear { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int? MaxCapacity { get; set; }
    public int StudentCount { get; set; }
    public int EnrollmentCount { get; set; }
    public List<SubjectInfo>? Subjects { get; set; }
}

public class SubjectInfo
{
    public Guid SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public string? TeacherName { get; set; }
}
