namespace SchoolPortal.Data.Entities;

public class LearnerCareerGoal
{
    public Guid LearnerCareerGoalId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UniversityCourseId { get; set; }
    public int Priority { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Student Student { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual UniversityCourse UniversityCourse { get; set; } = null!;
}
