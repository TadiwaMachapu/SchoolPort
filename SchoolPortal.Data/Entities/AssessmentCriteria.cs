namespace SchoolPortal.Data.Entities;

/// <summary>
/// Sprint 1.5.2.5 — one rubric criterion on an assessment task (e.g. "Evidence of research
/// and experimentation /10"). Only present when <see cref="Assignment.HasRubric"/>; a task's
/// total auto-calculates from its criteria scores. IsActive=false soft-retires a criterion
/// without orphaning captured CriteriaScore rows.
/// </summary>
public class AssessmentCriteria
{
    public Guid CriteriaId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public decimal MaxMark { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Assignment Assignment { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual ICollection<CriteriaScore> Scores { get; set; } = new List<CriteriaScore>();
}
