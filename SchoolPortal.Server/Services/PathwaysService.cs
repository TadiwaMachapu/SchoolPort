using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Services;

public interface IPathwaysService
{
    Task<LearnerApsResult> GetLearnerApsAsync(Guid studentId, Guid schoolId);
    Task<List<GoalWithTrackingDto>> GetLearnerGoalsAsync(Guid studentId, Guid schoolId);
    Task<GoalWithTrackingDto> AddGoalAsync(Guid studentId, Guid schoolId, Guid universityCourseId);
    Task DeleteGoalAsync(Guid goalId, Guid studentId, Guid schoolId);
    Task<GoalTrackingDto> GetGoalTrackingAsync(Guid goalId, Guid studentId, Guid schoolId);
    Task<ParentPathwaysDto> GetParentPathwaysAsync(Guid parentUserId, Guid schoolId);
}

public record SubjectScoreDto(
    Guid SubjectId,
    string SubjectName,
    string? CapsPhase,
    double? AveragePercent,
    int? ApsPoints
);

public record LearnerApsResult(
    int StandardAps,
    int TotalAps,
    List<SubjectScoreDto> SubjectScores
);

public record SubjectGapDto(
    string SubjectName,
    double? CurrentPercent,
    int RequiredPercent,
    double GapPercent,
    bool Met
);

public record GoalTrackingDto(
    Guid LearnerCareerGoalId,
    Guid UniversityCourseId,
    string CourseName,
    string UniversityName,
    string UniversityAbbreviation,
    string? Faculty,
    int MinimumAps,
    string? ApsNotes,
    int CurrentAps,
    int ApsGap,
    string Status,
    List<SubjectGapDto> SubjectGaps
);

public record GoalWithTrackingDto(
    Guid LearnerCareerGoalId,
    Guid UniversityCourseId,
    string CourseName,
    string UniversityName,
    string UniversityAbbreviation,
    string? Faculty,
    int MinimumAps,
    string Status,
    int CurrentAps,
    int Priority
);

public record ParentPathwaysDto(
    Guid StudentId,
    string StudentName,
    int CurrentAps,
    List<GoalWithTrackingDto> Goals
);

public class PathwaysService : IPathwaysService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ILogger<PathwaysService> _logger;

    public PathwaysService(SchoolPortalDbContext context, ILogger<PathwaysService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LearnerApsResult> GetLearnerApsAsync(Guid studentId, Guid schoolId)
    {
        // Sprint 1.5.1 Gap 2: APS is a CURRENT-year calculation. Unscoped, the query averaged
        // every grade ever recorded AND duplicated subjects enrolled across years (LearnerSubjects
        // is per-year). No academic year configured → fail closed with an empty result.
        var currentYear = await GetCurrentAcademicYearAsync(schoolId);
        if (currentYear == null)
            return new LearnerApsResult(0, 0, new List<SubjectScoreDto>());

        var subjectAverages = await (
            from ls in _context.LearnerSubjects
            where ls.StudentId == studentId && ls.SchoolId == schoolId
                  && ls.AcademicYearId == currentYear.AcademicYearId
            join s in _context.Subjects on ls.SubjectId equals s.SubjectId
            select new
            {
                s.SubjectId,
                s.Name,
                s.CapsPhase,
                Average = (double?)_context.Grades
                    .Where(g =>
                        g.SchoolId == schoolId &&
                        g.Submission.StudentId == studentId &&
                        g.Submission.Assignment.ClassSubject.SubjectId == ls.SubjectId &&
                        g.Submission.Assignment.MaxMarks > 0 &&
                        g.Submission.Assignment.DueAt >= currentYear.StartDate &&
                        g.Submission.Assignment.DueAt <= currentYear.EndDate)
                    .Average(g => (double?)((double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100))
            }
        ).ToListAsync();

        var scores = subjectAverages.Select(s =>
        {
            var points = s.Average.HasValue ? (int?)CalculateApsPoints(s.Average.Value) : null;
            return new SubjectScoreDto(s.SubjectId, s.Name, s.CapsPhase, s.Average, points);
        }).ToList();

        // Standard APS: best 6 subjects excluding Life Orientation, on 7-point scale
        var nonLo = scores.Where(s => !IsLifeOrientation(s.SubjectName) && s.ApsPoints.HasValue);
        var standardAps = nonLo.OrderByDescending(s => s.ApsPoints).Take(6).Sum(s => s.ApsPoints ?? 0);

        // Total APS: all subjects including Life Orientation (capped at 4 pts for LO)
        var totalAps = scores
            .Where(s => s.ApsPoints.HasValue)
            .Sum(s => IsLifeOrientation(s.SubjectName) ? Math.Min(s.ApsPoints!.Value, 4) : s.ApsPoints!.Value);

        return new LearnerApsResult(standardAps, totalAps, scores);
    }

    public async Task<List<GoalWithTrackingDto>> GetLearnerGoalsAsync(Guid studentId, Guid schoolId)
        => await GetLearnerGoalsAsync(studentId, schoolId, aps: null);

    // Overload that accepts a pre-computed APS so callers which already have it (e.g. the parent
    // dashboard) don't recompute the expensive per-subject grade-average query. Pass null to
    // compute it here. (APS stays a LIVE calculation — NOT vw_matric_aps_summary; the parent
    // dashboard needs current marks and the matview only refreshes manually. See CLAUDE.md.)
    private async Task<List<GoalWithTrackingDto>> GetLearnerGoalsAsync(
        Guid studentId, Guid schoolId, LearnerApsResult? aps)
    {
        var goals = await _context.LearnerCareerGoals
            .AsNoTracking()
            .Where(g => g.StudentId == studentId && g.SchoolId == schoolId)
            .Include(g => g.UniversityCourse).ThenInclude(c => c.University)
            .Include(g => g.UniversityCourse).ThenInclude(c => c.SubjectRequirements)
            .OrderBy(g => g.Priority)
            .ToListAsync();

        if (!goals.Any()) return new List<GoalWithTrackingDto>();

        aps ??= await GetLearnerApsAsync(studentId, schoolId);

        return goals.Select(g => ToGoalWithTrackingDto(g, aps)).ToList();
    }

    public async Task<GoalWithTrackingDto> AddGoalAsync(Guid studentId, Guid schoolId, Guid universityCourseId)
    {
        var existingCount = await _context.LearnerCareerGoals
            .CountAsync(g => g.StudentId == studentId && g.SchoolId == schoolId);

        if (existingCount >= 5)
            throw new InvalidOperationException("A maximum of 5 career goals can be saved.");

        var duplicate = await _context.LearnerCareerGoals
            .AnyAsync(g => g.StudentId == studentId && g.UniversityCourseId == universityCourseId);
        if (duplicate)
            throw new InvalidOperationException("This course is already in your saved goals.");

        var course = await _context.UniversityCourses
            .Include(c => c.University)
            .FirstOrDefaultAsync(c => c.UniversityCourseId == universityCourseId)
            ?? throw new KeyNotFoundException("University course not found.");

        var goal = new LearnerCareerGoal
        {
            LearnerCareerGoalId = Guid.NewGuid(),
            StudentId = studentId,
            SchoolId = schoolId,
            UniversityCourseId = universityCourseId,
            Priority = existingCount + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.LearnerCareerGoals.Add(goal);
        await _context.SaveChangesAsync();

        var aps = await GetLearnerApsAsync(studentId, schoolId);
        goal.UniversityCourse = course;
        return ToGoalWithTrackingDto(goal, aps);
    }

    public async Task DeleteGoalAsync(Guid goalId, Guid studentId, Guid schoolId)
    {
        var goal = await _context.LearnerCareerGoals
            .FirstOrDefaultAsync(g =>
                g.LearnerCareerGoalId == goalId &&
                g.StudentId == studentId &&
                g.SchoolId == schoolId)
            ?? throw new KeyNotFoundException("Goal not found.");

        _context.LearnerCareerGoals.Remove(goal);

        // Delete any cached gap analyses for this learner/course pair
        var caches = await _context.AiGapAnalysisCaches
            .Where(c => c.StudentId == studentId && c.UniversityCourseId == goal.UniversityCourseId)
            .ToListAsync();
        _context.AiGapAnalysisCaches.RemoveRange(caches);

        await _context.SaveChangesAsync();

        // Re-number remaining priorities
        var remaining = await _context.LearnerCareerGoals
            .Where(g => g.StudentId == studentId && g.SchoolId == schoolId)
            .OrderBy(g => g.Priority)
            .ToListAsync();

        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].Priority = i + 1;
            remaining[i].UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<GoalTrackingDto> GetGoalTrackingAsync(Guid goalId, Guid studentId, Guid schoolId)
    {
        var goal = await _context.LearnerCareerGoals
            .AsNoTracking()
            .Where(g =>
                g.LearnerCareerGoalId == goalId &&
                g.StudentId == studentId &&
                g.SchoolId == schoolId)
            .Include(g => g.UniversityCourse).ThenInclude(c => c.University)
            .Include(g => g.UniversityCourse).ThenInclude(c => c.SubjectRequirements)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Goal not found.");

        var aps = await GetLearnerApsAsync(studentId, schoolId);
        var course = goal.UniversityCourse;

        var subjectGaps = course.SubjectRequirements
            .Where(r => r.IsRequired && r.MinimumPercent.HasValue)
            .Select(req =>
            {
                // Gap 3: CAPS-aware matching — a school subject renamed "Maths" still matches
                // a "Mathematics" requirement (normalisation + alias/code, never fuzzy).
                var score = aps.SubjectScores.FirstOrDefault(s =>
                    CapsSubjects.Matches(s.SubjectName, req.SubjectName));
                var current = score?.AveragePercent;
                var gap = current.HasValue ? req.MinimumPercent!.Value - current.Value : (double)req.MinimumPercent!.Value;
                return new SubjectGapDto(
                    req.SubjectName,
                    current,
                    req.MinimumPercent!.Value,
                    Math.Max(0, gap),
                    current.HasValue && current.Value >= req.MinimumPercent!.Value
                );
            })
            .ToList();

        // Gap 3: distinguish "learner isn't enrolled" from "school subject names don't match the
        // CAPS requirement name at all" — the latter is a configuration bug and must be ops-visible.
        await WarnOnSchoolLevelNameMismatchAsync(schoolId,
            subjectGaps.Where(g => g.CurrentPercent == null).Select(g => g.SubjectName));

        var status = DetermineStatus(aps.StandardAps, course.MinimumAps, subjectGaps);

        return new GoalTrackingDto(
            goal.LearnerCareerGoalId,
            course.UniversityCourseId,
            course.Name,
            course.University.Name,
            course.University.Abbreviation,
            course.Faculty,
            course.MinimumAps,
            course.ApsNotes,
            aps.StandardAps,
            Math.Max(0, course.MinimumAps - aps.StandardAps),
            status,
            subjectGaps
        );
    }

    public async Task<ParentPathwaysDto> GetParentPathwaysAsync(Guid parentUserId, Guid schoolId)
    {
        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.ParentUserId == parentUserId && s.SchoolId == schoolId)
            .Include(s => s.User)
            .FirstOrDefaultAsync();

        if (student == null)
            return new ParentPathwaysDto(Guid.Empty, "", 0, new List<GoalWithTrackingDto>());

        // Compute APS ONCE and thread it into the goals builder (was computed twice — Sprint 1.5.0.5).
        var aps = await GetLearnerApsAsync(student.StudentId, schoolId);
        var goals = await GetLearnerGoalsAsync(student.StudentId, schoolId, aps);

        return new ParentPathwaysDto(
            student.StudentId,
            $"{student.User.FirstName} {student.User.LastName}",
            aps.StandardAps,
            goals
        );
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Gap 3 ops signal: for requirement names where the learner has no matching mark,
    /// checks whether ANY subject in the school matches the name. None → a subject-naming
    /// configuration bug (school renamed a subject beyond alias coverage), logged as a structured
    /// warning with the school, the requirement name, and its canonical CAPS resolution. Also
    /// surfaced on demand by GET /api/pathways/subject-match-report.</summary>
    private async Task WarnOnSchoolLevelNameMismatchAsync(Guid schoolId, IEnumerable<string> unmatchedRequirementNames)
    {
        var names = unmatchedRequirementNames.Distinct().ToList();
        if (names.Count == 0) return;

        var schoolSubjectNames = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .Select(s => s.Name)
            .ToListAsync();

        foreach (var requirementName in names)
        {
            if (schoolSubjectNames.Any(n => CapsSubjects.Matches(n, requirementName))) continue;
            _logger.LogWarning(
                "Pathways subject-name mismatch: school {SchoolId} has no subject matching requirement '{RequirementName}' (canonical CAPS name: '{CanonicalName}'). Goal tracking cannot find marks for it; see /api/pathways/subject-match-report.",
                schoolId, requirementName, CapsSubjects.FindCanonical(requirementName) ?? "<none>");
        }
    }

    private sealed record AcademicYearWindow(Guid AcademicYearId, DateTime StartDate, DateTime EndDate);

    /// <summary>"Current academic year" = the year of the school's IsCurrent term (exactly one per
    /// school by domain rule); fallback = latest year by Year (the GetClassMatrix rule) for schools
    /// without a current term set. Null when the school has no academic years at all.</summary>
    private async Task<AcademicYearWindow?> GetCurrentAcademicYearAsync(Guid schoolId)
    {
        var fromCurrentTerm = await _context.Terms
            .AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.IsCurrent)
            .Select(t => new AcademicYearWindow(
                t.AcademicYear.AcademicYearId, t.AcademicYear.StartDate, t.AcademicYear.EndDate))
            .FirstOrDefaultAsync();
        if (fromCurrentTerm != null) return fromCurrentTerm;

        return await _context.AcademicYears
            .AsNoTracking()
            .Where(y => y.SchoolId == schoolId)
            .OrderByDescending(y => y.Year)
            .Select(y => new AcademicYearWindow(y.AcademicYearId, y.StartDate, y.EndDate))
            .FirstOrDefaultAsync();
    }

    private GoalWithTrackingDto ToGoalWithTrackingDto(LearnerCareerGoal goal, LearnerApsResult aps)
    {
        var course = goal.UniversityCourse;
        var subjectGaps = course.SubjectRequirements
            .Where(r => r.IsRequired && r.MinimumPercent.HasValue)
            .Select(req =>
            {
                // Gap 3: CAPS-aware matching (see GetGoalTrackingAsync). This sync list-path
                // builder does not do the school-level mismatch check — the detail read and the
                // subject-match report carry the ops signal.
                var score = aps.SubjectScores.FirstOrDefault(s =>
                    CapsSubjects.Matches(s.SubjectName, req.SubjectName));
                var current = score?.AveragePercent;
                var gap = current.HasValue ? req.MinimumPercent!.Value - current.Value : (double)req.MinimumPercent!.Value;
                return new SubjectGapDto(req.SubjectName, current, req.MinimumPercent!.Value, Math.Max(0, gap), current.HasValue && current.Value >= req.MinimumPercent!.Value);
            }).ToList();

        var status = DetermineStatus(aps.StandardAps, course.MinimumAps, subjectGaps);

        return new GoalWithTrackingDto(
            goal.LearnerCareerGoalId,
            course.UniversityCourseId,
            course.Name,
            course.University.Name,
            course.University.Abbreviation,
            course.Faculty,
            course.MinimumAps,
            status,
            aps.StandardAps,
            goal.Priority
        );
    }

    private static string DetermineStatus(int currentAps, int requiredAps, List<SubjectGapDto> subjectGaps)
    {
        var apsGap = requiredAps - currentAps;

        // Red: APS gap > 3 or any required subject more than 10% below minimum
        if (apsGap > 3) return "Red";
        if (subjectGaps.Any(g => !g.Met && g.GapPercent > 10)) return "Red";

        // Amber: APS gap 1-3 or any required subject 1-10% below minimum
        if (apsGap > 0) return "Amber";
        if (subjectGaps.Any(g => !g.Met)) return "Amber";

        return "Green";
    }

    public static int CalculateApsPoints(double percent) => percent switch
    {
        >= 80 => 7,
        >= 70 => 6,
        >= 60 => 5,
        >= 50 => 4,
        >= 40 => 3,
        >= 30 => 2,
        _ => 1
    };

    private static bool IsLifeOrientation(string name) =>
        name.Contains("Life Orientation", StringComparison.OrdinalIgnoreCase);
}
