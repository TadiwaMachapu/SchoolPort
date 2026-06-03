namespace SchoolPortal.Data.Entities;

public class CourseSubjectRequirement
{
    public Guid CourseSubjectRequirementId { get; set; }
    public Guid UniversityCourseId { get; set; }
    public string SubjectName { get; set; } = null!;
    public int? MinimumPercent { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Notes { get; set; }

    public virtual UniversityCourse UniversityCourse { get; set; } = null!;
}
