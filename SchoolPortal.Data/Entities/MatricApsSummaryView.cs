namespace SchoolPortal.Data.Entities;

/// <summary>
/// Read-only projection over <c>vw_matric_aps_summary</c>: PROJECTED APS per Grade-12 learner per
/// academic year. Sprint 1.5.1 (FixMatricApsWeighting): <see cref="ProjectedAps"/> is the STANDARD
/// APS — best 6 subjects excluding Life Orientation — and <see cref="TotalAps"/> is all subjects
/// with LO capped at 4 points, matching PathwaysService.GetLearnerApsAsync semantics exactly.
/// Still a projection from year-averages (no promotion-mark weighting) and only as fresh as the
/// last manual refresh — dashboards needing current marks use the live calculator instead.
/// </summary>
public class MatricApsSummaryView
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public int ProjectedAps { get; set; }
    public int TotalAps { get; set; }
    public int SubjectCount { get; set; }
}
