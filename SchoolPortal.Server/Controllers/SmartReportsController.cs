using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

// Sprint 1.5.3 Smart Reports v1 — role views on top of the ONE shared at-risk primitive
// (IAtRiskService). No endpoint here computes at-risk logic; each resolves a scoped set of
// classes, calls AtRiskService.EvaluateAsync, and aggregates/presents for its role.
[ApiController]
[Route("api/smart-reports")]
public class SmartReportsController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IScopeService _scope;
    private readonly IAtRiskService _atRisk;

    public SmartReportsController(
        SchoolPortalDbContext context, ICurrentUserService currentUser,
        IScopeService scope, IAtRiskService atRisk)
    {
        _context = context;
        _currentUser = currentUser;
        _scope = scope;
        _atRisk = atRisk;
    }

    // GET /api/smart-reports/grade/{grade} — Grade Head: all learners in one grade, cross-subject,
    // Priority-first. Scoped: only classes at that grade the caller can access.
    [HttpGet("grade/{grade:int}")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetGradeView(int grade)
    {
        // Oversight-position gate (Sprint 1.5.3): the grade view is the Grade Head's surface. A plain
        // SubjectTeacher holds marks.view_class (passes the attribute) but is NOT an oversight holder →
        // 403, not an empty 200 (their surface is the teacher risk dashboard). Principal/Deputy qualify
        // school-wide; PhaseHead oversees the grade's phase.
        if (!HoldsAnyOf(PositionKeys.GradeHead, PositionKeys.PhaseHead, PositionKeys.Principal, PositionKeys.DeputyPrincipal))
            return Forbid();

        // Per-grade scope — holding the position is not enough; the requested grade must be in the
        // caller's scope. A Gr-12 head asking for Gr-11 → 403, identical in shape to the subject view's
        // wrong-subject 403 (a legitimately empty grade is a 200, not a 403). Principal/Deputy school-wide.
        if (!await _scope.CanAccessGradeAsync(grade)) return Forbid();

        var schoolId = _currentUser.SchoolId;
        var accessible = await _scope.GetOversightClassIdsAsync(); // null = unrestricted (school-wide)

        var classQuery = _context.Classes.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && c.GradeLevel == grade);
        var classIds = (await classQuery.Select(c => c.ClassId).ToListAsync())
            .Where(id => accessible is null || accessible.Contains(id))
            .ToList();
        if (classIds.Count == 0) return Ok(new GradeViewDto(grade, new List<GradeViewLearnerDto>()));

        var risk = await _atRisk.EvaluateAsync(schoolId, classIds, await CurrentTermIdAsync(schoolId));

        var roster = await RosterAsync(classIds);
        var learners = roster.Select(st =>
        {
            risk.TryGetValue(st.StudentId, out var r);
            return new GradeViewLearnerDto(
                st.StudentId, st.Name, st.StudentNumber, st.ClassName,
                r?.InterventionBand, r?.OverallRisk ?? "no_data",
                r?.SubjectsBelowFifty ?? 0, r?.CapturedSubjectCount ?? 0, r?.TotalSubjectCount ?? 0,
                r?.Subjects ?? new List<SubjectRiskDto>());
        })
        .OrderBy(l => BandRank(l.InterventionBand)).ThenBy(l => l.Name)
        .ToList();

        return Ok(new GradeViewDto(grade, learners));
    }

    // GET /api/smart-reports/subject/{subjectId} — HOD: all learners taking one subject across the
    // caller's accessible classes, plus a teacher comparison. Scoped + tenant-guarded on subjectId.
    [HttpGet("subject/{subjectId:guid}")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetSubjectView(Guid subjectId)
    {
        var schoolId = _currentUser.SchoolId;

        // Tenancy: a foreign-school subjectId dead-ends here (CrossTenantGuard test) — before any
        // authorization signal leaks.
        var subject = await _context.Subjects.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId && s.SchoolId == schoolId);
        if (subject == null) return NotFound();

        // Oversight-position gate: the subject view is the HOD's surface. A SubjectTeacher who teaches
        // this subject (holds marks.view_class/marks.view_subject) is NOT an HOD → 403 (their surface is
        // the teacher risk dashboard). Principal/Deputy qualify school-wide.
        if (!HoldsAnyOf(PositionKeys.HOD, PositionKeys.Principal, PositionKeys.DeputyPrincipal))
            return Forbid();

        // Subject-level scope — a Maths HOD must not open the Physical Sciences view even though a
        // shared class makes the class-level accessible set overlap. Class filtering below is not
        // sufficient on its own; this is the authoritative per-subject bound (Principal/Deputy school-wide).
        if (!await _scope.CanAccessSubjectAsync(subjectId)) return Forbid();

        var accessible = await _scope.GetOversightClassIdsAsync();
        var classSubjects = (await _context.ClassSubjects.AsNoTracking()
                .Where(cs => cs.SubjectId == subjectId && cs.SchoolId == schoolId)
                .Select(cs => new
                {
                    cs.ClassSubjectId, cs.ClassId, cs.TeacherId,
                    ClassName = cs.Class.Name,
                    TeacherName = cs.Teacher != null ? cs.Teacher.User.FirstName + " " + cs.Teacher.User.LastName : null,
                })
                .ToListAsync())
            .Where(cs => accessible is null || accessible.Contains(cs.ClassId))
            .ToList();
        if (classSubjects.Count == 0)
            return Ok(new SubjectViewDto(subjectId, subject.Name, new List<TeacherComparisonDto>(), new List<SubjectViewLearnerDto>()));

        var classIds = classSubjects.Select(cs => cs.ClassId).Distinct().ToList();
        var termId = await CurrentTermIdAsync(schoolId);
        var risk = await _atRisk.EvaluateAsync(schoolId, classIds, termId);

        // Which class-subjects have at least one captured mark this term (else the teacher hasn't
        // captured yet). Uses the shared captured predicate — same "captured" definition.
        var capturedCsIds = await CapturedClassSubjectIdsAsync(schoolId, classSubjects.Select(cs => cs.ClassSubjectId).ToList(), termId);

        // Per-learner: this subject's standing (from the primitive), for learners who take it.
        var roster = await _context.Enrollments.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.IsActive && e.SchoolId == schoolId)
            .Select(e => new { e.StudentId, Name = e.Student.User.FirstName + " " + e.Student.User.LastName, e.ClassId, ClassName = e.Class.Name })
            .ToListAsync();

        var learners = new List<SubjectViewLearnerDto>();
        foreach (var st in roster)
        {
            if (!risk.TryGetValue(st.StudentId, out var r)) continue;
            var subj = r.Subjects.FirstOrDefault(s => s.SubjectName == subject.Name);
            if (subj == null) continue; // learner in the class but doesn't take this subject
            learners.Add(new SubjectViewLearnerDto(
                st.StudentId, st.Name, st.ClassName, subj.Risk, subj.Average, subj.Trend, subj.MissingAssessments));
        }

        // Teacher comparison — group the scoped class-subjects by teacher.
        var byTeacher = classSubjects
            .GroupBy(cs => new { cs.TeacherId, cs.TeacherName })
            .Select(g =>
            {
                var classNames = g.Select(x => x.ClassName).Distinct().OrderBy(n => n).ToList();
                var teacherClassIds = g.Select(x => x.ClassId).ToHashSet();
                var subjectLearners = learners.Where(l => roster.Any(rr => rr.StudentId == l.StudentId && teacherClassIds.Contains(rr.ClassId))).ToList();
                var atRiskCount = subjectLearners.Count(l => l.Risk is "red" or "amber");
                var notCapturedYet = g.All(x => !capturedCsIds.Contains(x.ClassSubjectId));
                return new TeacherComparisonDto(
                    g.Key.TeacherId, g.Key.TeacherName ?? "Unassigned", classNames,
                    subjectLearners.Count, atRiskCount, notCapturedYet);
            })
            .OrderByDescending(t => t.AtRiskCount).ThenBy(t => t.TeacherName)
            .ToList();

        return Ok(new SubjectViewDto(subjectId, subject.Name, byTeacher,
            learners.OrderBy(l => l.Risk switch { "red" => 0, "amber" => 1, _ => 2 }).ThenBy(l => l.Name).ToList()));
    }

    // GET /api/smart-reports/school-overview — Principal: school-wide band totals, per-grade and
    // per-subject breakdowns. School-wide read → analytics.view_school (Sensitive).
    [HttpGet("school-overview")]
    [RequirePermission(PermissionKeys.AnalyticsViewSchool)]
    public async Task<IActionResult> GetSchoolOverview()
    {
        // School-wide overview is SMT-only. analytics.view_school (the attribute) also reaches
        // HOD/PhaseHead/GradeHead — gate tighter here so those oversight roles use their own
        // grade/subject views, not the school-wide roll-up.
        if (!HoldsAnyOf(PositionKeys.Principal, PositionKeys.DeputyPrincipal))
            return Forbid();

        var schoolId = _currentUser.SchoolId;
        var classIds = await _context.Classes.AsNoTracking()
            .Where(c => c.SchoolId == schoolId).Select(c => c.ClassId).ToListAsync();
        if (classIds.Count == 0)
            return Ok(new SchoolOverviewDto(new List<GradeBreakdownDto>(), new List<SubjectBreakdownDto>(), new BandTotalsDto(0, 0, 0, 0)));

        var risk = await _atRisk.EvaluateAsync(schoolId, classIds, await CurrentTermIdAsync(schoolId));

        var roster = await _context.Enrollments.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.IsActive && e.SchoolId == schoolId)
            .Select(e => new { e.StudentId, GradeLevel = e.Class.GradeLevel })
            .ToListAsync();

        var byGrade = roster
            .Where(r => r.GradeLevel != null)
            .GroupBy(r => r.GradeLevel!.Value)
            .Select(g =>
            {
                var bands = g.Select(x => risk.GetValueOrDefault(x.StudentId)?.InterventionBand).ToList();
                return new GradeBreakdownDto(g.Key,
                    bands.Count(b => b == "Priority"), bands.Count(b => b == "AtRisk"),
                    bands.Count(b => b == "Watch"), g.Count());
            })
            .OrderBy(x => x.Grade).ToList();

        var bySubject = risk.Values
            .SelectMany(r => r.Subjects.Where(s => s.Risk is "red" or "amber").Select(s => s.SubjectName))
            .GroupBy(n => n)
            .Select(g => new SubjectBreakdownDto(g.Key, g.Count()))
            .OrderByDescending(x => x.AtRiskLearners).ThenBy(x => x.SubjectName)
            .ToList();

        var totals = new BandTotalsDto(
            risk.Values.Count(r => r.InterventionBand == "Priority"),
            risk.Values.Count(r => r.InterventionBand == "AtRisk"),
            risk.Values.Count(r => r.InterventionBand == "Watch"),
            roster.Count);

        return Ok(new SchoolOverviewDto(byGrade, bySubject, totals));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    // True if the caller holds any of the given oversight positions (active, in-window). The role
    // views gate on POSITION, not merely marks.view_class: teaching scope → teacher dashboard;
    // oversight scope → these role views. A holder of BOTH (most HODs also teach) gets both surfaces.
    private bool HoldsAnyOf(params string[] positionKeys) => positionKeys.Any(_currentUser.HasPosition);

    private static int BandRank(string? band) => band switch { "Priority" => 0, "AtRisk" => 1, "Watch" => 2, _ => 3 };

    private async Task<Guid?> CurrentTermIdAsync(Guid schoolId) =>
        await _context.Terms.AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.IsCurrent)
            .Select(t => (Guid?)t.TermId)
            .FirstOrDefaultAsync();

    private async Task<List<RosterRow>> RosterAsync(IReadOnlyCollection<Guid> classIds) =>
        await _context.Enrollments.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.IsActive && e.SchoolId == _currentUser.SchoolId)
            .Select(e => new RosterRow(
                e.StudentId,
                e.Student.User.FirstName + " " + e.Student.User.LastName,
                e.Student.StudentNumber,
                e.Class.Name))
            .ToListAsync();

    private async Task<HashSet<Guid>> CapturedClassSubjectIdsAsync(Guid schoolId, List<Guid> classSubjectIds, Guid? termId)
    {
        var q = _context.Grades.AsNoTracking()
            .Where(AtRiskMarks.CapturedPredicate(schoolId))
            .Where(g => classSubjectIds.Contains(g.Assignment.ClassSubjectId));
        if (termId is not null)
        {
            var term = await _context.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.TermId == termId.Value);
            if (term != null)
                q = q.Where(g => g.Assignment.DueAt >= term.StartDate && g.Assignment.DueAt <= term.EndDate);
        }
        return (await q.Select(g => g.Assignment.ClassSubjectId).Distinct().ToListAsync()).ToHashSet();
    }

    private record RosterRow(Guid StudentId, string Name, string StudentNumber, string ClassName);
}

// ── response DTOs ────────────────────────────────────────────────────────────────

public record GradeViewDto(int Grade, List<GradeViewLearnerDto> Learners);
public record GradeViewLearnerDto(
    Guid StudentId, string Name, string StudentNumber, string ClassName,
    string? InterventionBand, string OverallRisk,
    int SubjectsBelowFifty, int CapturedSubjectCount, int TotalSubjectCount,
    List<SubjectRiskDto> Subjects);

public record SubjectViewDto(Guid SubjectId, string SubjectName, List<TeacherComparisonDto> ByTeacher, List<SubjectViewLearnerDto> Learners);
public record TeacherComparisonDto(Guid? TeacherId, string TeacherName, List<string> Classes, int LearnerCount, int AtRiskCount, bool NotCapturedYet);
public record SubjectViewLearnerDto(Guid StudentId, string Name, string ClassName, string Risk, double? Average, string Trend, int MissingAssessments);

public record SchoolOverviewDto(List<GradeBreakdownDto> ByGrade, List<SubjectBreakdownDto> BySubject, BandTotalsDto Totals);
public record GradeBreakdownDto(int Grade, int Priority, int AtRisk, int Watch, int TotalLearners);
public record SubjectBreakdownDto(string SubjectName, int AtRiskLearners);
public record BandTotalsDto(int Priority, int AtRisk, int Watch, int TotalLearners);
