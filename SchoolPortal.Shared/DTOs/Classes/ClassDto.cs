namespace SchoolPortal.Shared.DTOs.Classes;

public class ClassDto
{
    public int ClassId { get; set; }
    public string Name { get; set; } = null!;
    public int? GradeLevel { get; set; }
    public int? AcademicYear { get; set; }
    public int? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int? MaxCapacity { get; set; }
    public int EnrollmentCount { get; set; }
    public List<SubjectInfo>? Subjects { get; set; }
}

public class SubjectInfo
{
    public int SubjectId { get; set; }
    public string Name { get; set; } = null!;
    public string? TeacherName { get; set; }
}
