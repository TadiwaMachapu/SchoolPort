using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Services;

// Sprint 1.5.3 — THE one authoritative "is this learner at risk, and why" computation. Both the
// Matric risk dashboard (MatricHubService) and Smart Reports (SmartReportsService) route through
// this; neither keeps its own at-risk logic, so they can never disagree (guarded by
// AtRisk_BothSurfaces_AgreeForSameLearner). Reads captured marks only — Sprint 1.5.2.5 Grade path,
// not absent, score present — computes per-subject average/trend/CAPS-risk + the 50% intervention
// band. Same pattern as PathwaysService.CalculateApsPoints: one calculation, many surfaces.

/// <summary>Per-subject risk (shared shape; lived in MatricHubService before 1.5.3).</summary>
public record SubjectRiskDto(
    string SubjectName,
    double Average,             // captured marks only
    int MissingAssessments,     // past-due assignments in the current term with no grade record
    string Trend,               // improving | declining | stable | no_data
    string Risk                 // red | amber | green
);

/// <summary>The authoritative per-learner at-risk judgment. Surfaces decorate it with identity,
/// attendance, aggregation — but never recompute the band or per-subject risk.</summary>
public record LearnerAtRiskResult(
    Guid StudentId,
    List<SubjectRiskDto> Subjects,   // red-first
    string OverallRisk,              // red | amber | green | no_data (worst subject)
    int RedCount,
    int AmberCount,
    int GreenCount,
    string? InterventionBand,        // Watch | AtRisk | Priority | null — 50% line, captured subjects only
    int SubjectsBelowFifty,
    int CapturedSubjectCount,
    int TotalSubjectCount,
    double? OverallAverage           // mean of captured-subject averages (null when none captured)
);

public static class AtRiskMarks
{
    /// <summary>THE one definition of "which marks count" — captured: not absent, score present,
    /// on the authoritative Grade.StudentId/AssignmentId path. Used by the primitive AND by
    /// SmartReports' AI-prompt reads, so both see the same real 1.5.2.5 marks. WEEK 3 FOLLOW-UP:
    /// gate approval here (append <c>&amp;&amp; …Status == ApprovalStatus.Approved</c>) and nowhere
    /// else. See CLAUDE.md "Marks Capture".</summary>
    public static Expression<Func<Grade, bool>> CapturedPredicate(Guid schoolId) =>
        g => g.SchoolId == schoolId && !g.IsAbsent && g.Score != null;
}

public interface IAtRiskService
{
    /// <summary>Evaluates every active-enrolled learner in the given classes for the given term.
    /// <paramref name="termId"/> is the "current" term and IS THE JUDGMENT WINDOW: per-subject
    /// average, red/amber/green risk, the below-50 count and the intervention band all use marks
    /// captured IN THAT TERM (the previous term is resolved only for the trend arrow). null → no
    /// term context (all captured marks count, no trend, missing = all past-due). A learner with no
    /// captured mark and nothing missing IN THE TERM → OverallRisk "no_data", band null,
    /// OverallAverage null (no-data ≠ zero). One set of queries for the whole cohort, keyed by
    /// StudentId; every enrolled learner gets an entry.</summary>
    Task<IReadOnlyDictionary<Guid, LearnerAtRiskResult>> EvaluateAsync(
        Guid schoolId, IReadOnlyCollection<Guid> classIds, Guid? termId);
}

public class AtRiskService : IAtRiskService
{
    private readonly SchoolPortalDbContext _context;

    public AtRiskService(SchoolPortalDbContext context) => _context = context;

    private IQueryable<Grade> GetCapturedGradesQuery(Guid schoolId) =>
        _context.Grades.AsNoTracking().Where(AtRiskMarks.CapturedPredicate(schoolId));

    public async Task<IReadOnlyDictionary<Guid, LearnerAtRiskResult>> EvaluateAsync(
        Guid schoolId, IReadOnlyCollection<Guid> classIds, Guid? termId)
    {
        var result = new Dictionary<Guid, LearnerAtRiskResult>();
        if (classIds.Count == 0) return result;
        var classIdList = classIds.ToList();

        // Term windows. termId given → that term is "current"; previous = the term ending before it.
        // null (or unknown id) → no term context: all captured marks count, no trend, missing = all
        // past-due (graceful degradation, matches the dashboard's no-current-term behaviour).
        Term? current = termId is null ? null : await _context.Terms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TermId == termId.Value && t.SchoolId == schoolId);
        var previous = current == null ? null : await _context.Terms.AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.EndDate < current.StartDate)
            .OrderByDescending(t => t.EndDate)
            .FirstOrDefaultAsync();

        var students = await _context.Enrollments.AsNoTracking()
            .Where(e => classIdList.Contains(e.ClassId) && e.IsActive && e.SchoolId == schoolId)
            .Select(e => new { e.StudentId, e.ClassId })
            .ToListAsync();
        if (students.Count == 0) return result;
        var studentIds = students.Select(s => s.StudentId).ToHashSet();

        var now = DateTime.UtcNow;
        var assignments = await _context.Assignments.AsNoTracking()
            .Where(a => a.SchoolId == schoolId && classIdList.Contains(a.ClassSubject.ClassId) && a.DueAt <= now)
            .Select(a => new
            {
                a.AssignmentId,
                a.ClassSubject.ClassId,
                a.ClassSubject.SubjectId,
                SubjectName = a.ClassSubject.Subject.Name,
                a.DueAt,
                a.MaxMarks,
            })
            .ToListAsync();
        var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();

        // THE SEAM — captured marks keyed by (student, assignment).
        var capturedLookup = (await GetCapturedGradesQuery(schoolId)
                .Where(g => studentIds.Contains(g.StudentId) && assignmentIds.Contains(g.AssignmentId))
                .Select(g => new { g.StudentId, g.AssignmentId, Score = g.Score!.Value })
                .ToListAsync())
            .ToDictionary(x => (x.StudentId, x.AssignmentId), x => x.Score);

        // "Missing" = a past-due assignment the learner takes with NO grade record at all. An absent
        // mark IS a record, so absent counts as neither a mark nor missing.
        var recordedKeys = (await _context.Grades.AsNoTracking()
                .Where(g => g.SchoolId == schoolId && studentIds.Contains(g.StudentId) && assignmentIds.Contains(g.AssignmentId))
                .Select(g => new { g.StudentId, g.AssignmentId })
                .ToListAsync())
            .Select(x => (x.StudentId, x.AssignmentId)).ToHashSet();

        // FET learners don't all take every subject in their register class — count "missing" only
        // for subjects the learner takes (LearnerSubjects; fall back to subjects they have a record in).
        var learnerSubjects = (await _context.LearnerSubjects.AsNoTracking()
                .Where(ls => ls.SchoolId == schoolId && studentIds.Contains(ls.StudentId))
                .Select(ls => new { ls.StudentId, ls.SubjectId })
                .ToListAsync())
            .GroupBy(ls => ls.StudentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SubjectId).ToHashSet());

        bool InWindow(DateTime dueAt, Term term) => dueAt >= term.StartDate && dueAt <= term.EndDate;
        decimal? CapturedScore(Guid sid, Guid aid) => capturedLookup.TryGetValue((sid, aid), out var sc) ? sc : null;

        foreach (var st in students)
        {
            var enrolled = learnerSubjects.GetValueOrDefault(st.StudentId)
                ?? assignments.Where(a => recordedKeys.Contains((st.StudentId, a.AssignmentId)))
                    .Select(a => a.SubjectId).ToHashSet();
            var mine = assignments
                .Where(a => a.ClassId == st.ClassId && enrolled.Contains(a.SubjectId))
                .ToList();

            var subjectData = mine
                .GroupBy(a => a.SubjectName)
                .Select(g =>
                {
                    // All captured marks for the subject (all-time) — used ONLY for the term-over-term
                    // trend (current vs previous window).
                    var graded = g
                        .Select(a => new { a.DueAt, Score = CapturedScore(st.StudentId, a.AssignmentId), a.MaxMarks })
                        .Where(m => m.Score != null && m.MaxMarks > 0)
                        .Select(m => new { m.DueAt, Pct = (double)(m.Score!.Value / m.MaxMarks * 100) })
                        .ToList();

                    // The AVERAGE, risk and below-50 judgment use the SELECTED TERM only — the
                    // actionable window (Sprint 1.5.3): a learner strong last term but failing now must
                    // be judged on now, not have the present masked by the past. No term context
                    // (termId null) → all captured marks count (graceful degradation).
                    var gradedInTerm = current == null
                        ? graded
                        : graded.Where(m => InWindow(m.DueAt, current)).ToList();

                    var missing = g.Count(a => !recordedKeys.Contains((st.StudentId, a.AssignmentId)) &&
                        (current == null || InWindow(a.DueAt, current)));

                    // No captured mark AND no missing IN THIS TERM → the subject has no signal this
                    // term (a prior-term-only subject is dropped, never read as 0%).
                    if (gradedInTerm.Count == 0 && missing == 0) return null;

                    var hasMarks = gradedInTerm.Count > 0;
                    var average = hasMarks ? Math.Round(gradedInTerm.Average(m => m.Pct), 1) : 0.0;

                    string trend = "no_data";
                    double? trendDiff = null;
                    if (current != null && previous != null)
                    {
                        var cur = graded.Where(m => InWindow(m.DueAt, current)).Select(m => m.Pct).ToList();
                        var prev = graded.Where(m => InWindow(m.DueAt, previous)).Select(m => m.Pct).ToList();
                        if (cur.Count > 0 && prev.Count > 0)
                        {
                            trendDiff = cur.Average() - prev.Average();
                            trend = trendDiff > 5 ? "improving" : trendDiff < -5 ? "declining" : "stable";
                        }
                    }

                    var risk =
                        (hasMarks && average < 40) || missing >= 3 || (trend == "declining" && average < 60 && hasMarks) ? "red"
                        : (hasMarks && average < 50) || missing >= 1 ? "amber"
                        : "green";

                    return new { SubjectName = g.Key, Average = average, Missing = missing, Trend = trend, TrendDiff = trendDiff, Risk = risk, HasMarks = hasMarks };
                })
                .Where(s => s != null).Select(s => s!)
                .OrderBy(s => s.Risk switch { "red" => 0, "amber" => 1, _ => 2 })
                .ThenBy(s => s.SubjectName)
                .ToList();

            var subjects = subjectData
                .Select(s => new SubjectRiskDto(s.SubjectName, s.Average, s.Missing, s.Trend, s.Risk))
                .ToList();

            var overall = subjects.Count == 0 ? "no_data"
                : subjects.Any(s => s.Risk == "red") ? "red"
                : subjects.Any(s => s.Risk == "amber") ? "amber"
                : "green";

            var capturedSubjects = subjectData.Where(s => s.HasMarks).ToList();
            var below50 = capturedSubjects.Count(s => s.Average < 50);
            var decliningSharp = capturedSubjects.Any(s => s.TrendDiff is < -10);
            string? band =
                below50 >= 3 || decliningSharp ? "Priority"
                : below50 == 2 ? "AtRisk"
                : below50 == 1 ? "Watch"
                : null;
            double? overallAverage = capturedSubjects.Count > 0
                ? Math.Round(capturedSubjects.Average(s => s.Average), 1) : null;

            result[st.StudentId] = new LearnerAtRiskResult(
                st.StudentId, subjects, overall,
                subjects.Count(s => s.Risk == "red"),
                subjects.Count(s => s.Risk == "amber"),
                subjects.Count(s => s.Risk == "green"),
                band, below50, capturedSubjects.Count, subjectData.Count, overallAverage);
        }

        return result;
    }
}
