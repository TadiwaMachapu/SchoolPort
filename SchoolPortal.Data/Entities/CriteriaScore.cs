namespace SchoolPortal.Data.Entities;

/// <summary>
/// Sprint 1.5.2.5 — a learner's score on one rubric criterion of a task. Score is nullable:
/// null = not yet entered, which is distinct from 0 (present, scored nothing). Unique per
/// (GradeId, CriteriaId).
/// </summary>
public class CriteriaScore
{
    public Guid CriteriaScoreId { get; set; }
    public Guid GradeId { get; set; }
    public Guid CriteriaId { get; set; }
    public Guid SchoolId { get; set; }
    public decimal? Score { get; set; }

    // Navigation properties
    public virtual Grade Grade { get; set; } = null!;
    public virtual AssessmentCriteria Criteria { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
