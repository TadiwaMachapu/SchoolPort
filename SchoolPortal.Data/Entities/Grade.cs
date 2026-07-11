namespace SchoolPortal.Data.Entities;

/// <summary>
/// A learner's mark on an assessment task. Sprint 1.5.2.5 decoupled marks from submissions:
/// StudentId + AssignmentId are authoritative (unique per pair); SubmissionId is set only for
/// grades that originated from the LMS submission-grading flow, null for capture-grid marks.
/// Absent semantics (Henco markbook): IsAbsent=true REQUIRES Score=null (service-enforced) —
/// absent is not zero; zero is present-scored-nothing. Null scores fall out of SQL AVG()
/// naturally, so absents never drag averages.
/// </summary>
public class Grade
{
    public Guid GradeId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid SchoolId { get; set; }
    public decimal? Score { get; set; }
    public bool IsAbsent { get; set; }
    public string? Feedback { get; set; }
    public Guid GradedByUserId { get; set; }
    public DateTime GradedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual Submission? Submission { get; set; }
    public virtual Student Student { get; set; } = null!;
    public virtual Assignment Assignment { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User GradedByUser { get; set; } = null!;
    public virtual ICollection<CriteriaScore> CriteriaScores { get; set; } = new List<CriteriaScore>();
}
