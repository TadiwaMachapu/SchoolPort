namespace SchoolPortal.Data.Entities;

/// <summary>
/// Sprint 1.5.0.5 — read-only projection over the materialized view <c>vw_subject_term_averages</c>
/// (per-learner / per-subject / per-term assignment average). Refreshed manually via the
/// refresh-views admin endpoint, not on every grade save. Columns map snake_case by convention.
/// </summary>
public class SubjectTermAverageView
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public Guid TermId { get; set; }
    public int TermNumber { get; set; }
    public decimal AveragePercent { get; set; }
    public int TasksAssessed { get; set; }
}
