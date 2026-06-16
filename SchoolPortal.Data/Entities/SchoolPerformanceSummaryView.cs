namespace SchoolPortal.Data.Entities;

/// <summary>
/// Sprint 1.5.0.5 — read-only projection over <c>vw_school_performance_summary</c>: per-school /
/// per-term / per-subject stats (subject average, at-risk count, pass rate). Pass/at-risk boundary
/// is the CAPS minimum of 40%. (Smart Reports' at-risk FLAGGING uses a separate higher intervention
/// threshold — see CLAUDE.md.) Refreshed manually (end of term / before report generation).
/// </summary>
public class SchoolPerformanceSummaryView
{
    public Guid SchoolId { get; set; }
    public Guid TermId { get; set; }
    public int TermNumber { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public decimal SubjectAverage { get; set; }
    public int LearnerCount { get; set; }
    public int AtRiskCount { get; set; }
    public decimal PassRate { get; set; }
}
