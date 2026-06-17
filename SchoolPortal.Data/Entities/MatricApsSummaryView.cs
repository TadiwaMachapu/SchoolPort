namespace SchoolPortal.Data.Entities;

/// <summary>
/// Sprint 1.5.0.5 — read-only projection over <c>vw_matric_aps_summary</c>: a PROJECTED APS per
/// Grade-12 learner per academic year. <see cref="ProjectedAps"/> is a flat sum of CAPS-code points
/// from each subject's year-average — NOT an official APS (which weights promotion/final marks).
/// Sprint 1.5.1 adds proper promotion-mark weighting; downstream must treat this as a projection.
/// </summary>
public class MatricApsSummaryView
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public int ProjectedAps { get; set; }
    public int SubjectCount { get; set; }
}
