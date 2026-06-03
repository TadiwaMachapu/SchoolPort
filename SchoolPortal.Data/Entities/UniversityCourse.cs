namespace SchoolPortal.Data.Entities;

public class UniversityCourse
{
    public Guid UniversityCourseId { get; set; }
    public Guid UniversityId { get; set; }
    public Guid? CareerId { get; set; }
    public string Name { get; set; } = null!;
    public string? Faculty { get; set; }
    public int MinimumAps { get; set; }
    public string? ApsNotes { get; set; }

    public virtual University University { get; set; } = null!;
    public virtual Career? Career { get; set; }
    public virtual ICollection<CourseSubjectRequirement> SubjectRequirements { get; set; } = new List<CourseSubjectRequirement>();
    public virtual ICollection<LearnerCareerGoal> LearnerGoals { get; set; } = new List<LearnerCareerGoal>();
    public virtual ICollection<AiGapAnalysisCache> GapAnalysisCaches { get; set; } = new List<AiGapAnalysisCache>();
}
