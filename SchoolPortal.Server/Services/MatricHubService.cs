using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Services;

public interface IMatricHubService
{
    Task<List<string>> GetSubjectsAsync();
    Task<List<PastPaperDto>> GetPastPapersAsync(string? subject);
    Task<List<MatricQuizQuestionDto>> GetQuizQuestionsAsync(string subject, int count);
    Task<StudyPlanDto> GetStudyPlanAsync(Guid studentId, Guid schoolId);
    Task<RiskDashboardDto> GetRiskDashboardAsync(Guid schoolId, IReadOnlySet<Guid>? accessibleClassIds, Guid? classId);
    Task<GradeOverviewDto> GetGradeOverviewAsync(Guid schoolId, IReadOnlySet<Guid>? accessibleClassIds);
}

// ── Sprint 1.5.2 Week 2 — staff risk views ──────────────────────────────────────
// Risk bands (spec): green = average >= 50% AND 0 missing; amber = average 40–49% OR 1–2
// missing; red = average < 40% OR 3+ missing OR declining trend with average < 60%.
// Red is evaluated first, then amber, then green. Trend compares the current term's average
// to the previous term's for the same subject (±5% band; "no_data" with only one term).
// SubjectRiskDto moved to AtRiskService.cs (Sprint 1.5.3 — shared with SmartReportsService).

public record LearnerRiskDto(
    Guid StudentId,
    string Name,
    string StudentNumber,
    string ClassName,
    string OverallRisk,         // worst subject risk; no_data when no subjects
    List<SubjectRiskDto> Subjects,
    int RedCount,
    int AmberCount,
    int GreenCount,
    // Sprint 1.5.3 — intervention band on the 50% line (Watch/AtRisk/Priority; null = none),
    // counting only CAPTURED subjects. Fraction context: {SubjectsBelowFifty} of
    // {CapturedSubjectCount} captured, {CapturedSubjectCount} of {TotalSubjectCount} captured
    // so far — so the frontend can show "below 50% in 2 of 3 captured subjects" (partial picture).
    string? InterventionBand,
    int SubjectsBelowFifty,
    int CapturedSubjectCount,
    int TotalSubjectCount
);

public record RiskSummaryDto(int Red, int Amber, int Green, int NoData);

public record RiskDashboardDto(
    List<ClassRefDto> Classes,
    RiskSummaryDto Summary,     // learners by overall risk
    List<LearnerRiskDto> Learners
);

public record ClassRefDto(Guid ClassId, string Name);

public record GradeOverviewLearnerDto(
    Guid StudentId,
    string Name,
    string StudentNumber,
    string ClassName,
    string OverallRisk,
    List<string> RedSubjects,
    List<string> AmberSubjects,
    int MissingAssessments,
    List<string> PriorityFlags,
    string? InterventionBand,
    int SubjectsBelowFifty,
    int CapturedSubjectCount,
    int TotalSubjectCount
);

public record GradeOverviewDto(
    int TotalLearners,
    RiskSummaryDto Summary,
    List<GradeOverviewLearnerDto> Learners
);

public record PastPaperDto(
    Guid MatricPastPaperId,
    string Subject,
    int Year,
    int PaperNumber,
    string PaperType,
    string Language,
    string Url,
    bool HasMemo,
    string? MemoUrl,
    string? Notes
);

/// <summary>Sprint 1.5.2 Step 4 — study planner: countdown to the November NSC exams plus
/// per-subject suggested weekly goals derived from the learner's current averages.</summary>
public record StudyPlanDto(
    bool IsGrade12,
    DateOnly ExamStart,
    int DaysToExams,
    int WeeksToExams,
    int SuggestedWeeklySessions,
    List<SubjectStudyGoalDto> Subjects
);

public record SubjectStudyGoalDto(
    string SubjectName,
    double Average,
    string Status,          // Fail | AtRisk | Pass — same thresholds as the NSC status views
    int WeeklySessions,     // suggested ~1-hour sessions per week
    string FocusHint
);

public record MatricQuizQuestionDto(
    Guid MatricQuizQuestionId,
    string Subject,
    string Difficulty,
    string QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD
);

public class MatricHubService : IMatricHubService
{
    private readonly SchoolPortalDbContext _context;
    private readonly IAtRiskService _atRisk;

    public MatricHubService(SchoolPortalDbContext context, IAtRiskService atRisk)
    {
        _context = context;
        _atRisk = atRisk;
    }

    public async Task<List<string>> GetSubjectsAsync()
    {
        var fromPapers = await _context.MatricPastPapers
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => p.Subject)
            .Distinct()
            .ToListAsync();

        var fromQuizzes = await _context.MatricQuizQuestions
            .AsNoTracking()
            .Select(q => q.Subject)
            .Distinct()
            .ToListAsync();

        return fromPapers.Union(fromQuizzes).OrderBy(s => s).ToList();
    }

    public async Task<List<PastPaperDto>> GetPastPapersAsync(string? subject)
    {
        // IsActive filter: deactivated rows (e.g. the phantom 2019 P2s) must never surface.
        var query = _context.MatricPastPapers.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(p => p.Subject == subject);

        return await query
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.Subject)
            .ThenBy(p => p.PaperNumber)
            .Select(p => new PastPaperDto(
                p.MatricPastPaperId,
                p.Subject,
                p.Year,
                p.PaperNumber,
                p.PaperType.ToString(),
                p.Language,
                p.Url,
                p.HasMemo,
                p.MemoUrl,
                p.Notes))
            .ToListAsync();
    }

    public async Task<StudyPlanDto> GetStudyPlanAsync(Guid studentId, Guid schoolId)
    {
        var inGr12 = await _context.Enrollments.AnyAsync(e =>
            e.StudentId == studentId && e.IsActive && e.Class.GradeLevel == 12 && e.SchoolId == schoolId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // The NSC written exams start in late October ("the November exams"); 21 October is
        // the nominal countdown anchor. December/January callers count down to next year's.
        var examStart = new DateOnly(today.Year, 10, 21);
        if (today > new DateOnly(today.Year, 11, 30)) examStart = examStart.AddYears(1);
        var daysToExams = Math.Max(0, examStart.DayNumber - today.DayNumber);
        var weeksToExams = (daysToExams + 6) / 7;

        if (!inGr12)
            return new StudyPlanDto(false, examStart, daysToExams, weeksToExams, 0, new List<SubjectStudyGoalDto>());

        // Same per-subject averages the learner sees on their NSC status view (GetMine).
        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId && g.Submission.StudentId == studentId)
            .Select(g => new
            {
                SubjectName = g.Submission.Assignment.ClassSubject.Subject.Name,
                Percentage = g.Submission.Assignment.MaxMarks > 0
                    ? Math.Round((double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100, 1)
                    : 0.0
            })
            .ToListAsync();

        var subjects = grades
            .GroupBy(g => g.SubjectName)
            .Select(g =>
            {
                var avg = Math.Round(g.Average(x => x.Percentage), 1);
                var (sessions, hint) = SuggestWeeklyGoal(avg);
                var status = avg >= 40 ? "Pass" : avg >= 30 ? "AtRisk" : "Fail";
                return new SubjectStudyGoalDto(g.Key, avg, status, sessions, hint);
            })
            // Weakest subjects first — that is where the plan's hours go.
            .OrderBy(s => s.Average)
            .ToList();

        return new StudyPlanDto(true, examStart, daysToExams, weeksToExams,
            subjects.Sum(s => s.WeeklySessions), subjects);
    }

    public async Task<RiskDashboardDto> GetRiskDashboardAsync(
        Guid schoolId, IReadOnlySet<Guid>? accessibleClassIds, Guid? classId)
    {
        var (classes, learners) = await ComputeGrade12RiskAsync(schoolId, accessibleClassIds, classId);
        return new RiskDashboardDto(classes, Summarise(learners), Sorted(learners)
            .ToList());
    }

    public async Task<GradeOverviewDto> GetGradeOverviewAsync(
        Guid schoolId, IReadOnlySet<Guid>? accessibleClassIds)
    {
        var (_, learners) = await ComputeGrade12RiskAsync(schoolId, accessibleClassIds, classId: null);

        var rows = Sorted(learners).Select(l =>
        {
            var red = l.Subjects.Where(s => s.Risk == "red").Select(s => s.SubjectName).ToList();
            var amber = l.Subjects.Where(s => s.Risk == "amber").Select(s => s.SubjectName).ToList();
            var missing = l.Subjects.Sum(s => s.MissingAssessments);

            var flags = new List<string>();
            if (red.Count >= 2) flags.Add($"{red.Count} subjects at red risk");
            foreach (var s in l.Subjects.Where(s => s.Trend == "declining" && s.Average < 60))
                flags.Add($"Declining in {s.SubjectName}");
            if (missing >= 3) flags.Add($"{missing} missing assessments");

            return new GradeOverviewLearnerDto(
                l.StudentId, l.Name, l.StudentNumber, l.ClassName,
                l.OverallRisk, red, amber, missing, flags,
                l.InterventionBand, l.SubjectsBelowFifty, l.CapturedSubjectCount, l.TotalSubjectCount);
        }).ToList();

        return new GradeOverviewDto(rows.Count, Summarise(learners), rows);
    }

    private static RiskSummaryDto Summarise(List<LearnerRiskDto> learners) => new(
        learners.Count(l => l.OverallRisk == "red"),
        learners.Count(l => l.OverallRisk == "amber"),
        learners.Count(l => l.OverallRisk == "green"),
        learners.Count(l => l.OverallRisk == "no_data"));

    // red first, then amber, then green, then no_data; most red subjects at the top.
    private static IEnumerable<LearnerRiskDto> Sorted(List<LearnerRiskDto> learners) => learners
        .OrderBy(l => l.OverallRisk switch { "red" => 0, "amber" => 1, "green" => 2, _ => 3 })
        .ThenByDescending(l => l.RedCount)
        .ThenBy(l => l.Name);

    /// <summary>Resolves the caller's accessible Grade 12 classes, then delegates the at-risk
    /// judgment to the shared <see cref="IAtRiskService"/> (the ONE place that logic lives) and
    /// decorates each result with learner identity. accessibleClassIds null = unrestricted
    /// (oversight); classId narrows to one class (already scope-checked by the controller).</summary>
    private async Task<(List<ClassRefDto> Classes, List<LearnerRiskDto> Learners)> ComputeGrade12RiskAsync(
        Guid schoolId, IReadOnlySet<Guid>? accessibleClassIds, Guid? classId)
    {
        var classQuery = _context.Classes.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && c.GradeLevel == 12);
        if (classId.HasValue) classQuery = classQuery.Where(c => c.ClassId == classId.Value);
        if (accessibleClassIds is not null) classQuery = classQuery.Where(c => accessibleClassIds.Contains(c.ClassId));

        var classes = await classQuery.Select(c => new ClassRefDto(c.ClassId, c.Name)).ToListAsync();
        if (classes.Count == 0) return (classes, new List<LearnerRiskDto>());
        var classIds = classes.Select(c => c.ClassId).ToList();

        // Delegate the at-risk judgment to the shared primitive (the ONE place it lives), for the
        // school's current term. No current term → null → graceful (all captured marks count, no
        // trend). The roster (identity) is loaded here and decorated onto each result.
        var currentTermId = await _context.Terms.AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.IsCurrent)
            .Select(t => (Guid?)t.TermId)
            .FirstOrDefaultAsync();

        var risk = await _atRisk.EvaluateAsync(schoolId, classIds, currentTermId);

        var students = await _context.Enrollments.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.IsActive && e.SchoolId == schoolId)
            .Select(e => new
            {
                e.Student.StudentId,
                Name = $"{e.Student.User.FirstName} {e.Student.User.LastName}",
                e.Student.StudentNumber,
                ClassName = e.Class.Name,
            })
            .ToListAsync();

        var learners = students.Select(st =>
        {
            if (risk.TryGetValue(st.StudentId, out var r))
                return new LearnerRiskDto(
                    st.StudentId, st.Name, st.StudentNumber, st.ClassName,
                    r.OverallRisk, r.Subjects, r.RedCount, r.AmberCount, r.GreenCount,
                    r.InterventionBand, r.SubjectsBelowFifty, r.CapturedSubjectCount, r.TotalSubjectCount);
            // Enrolled but no subjects in play → no signal.
            return new LearnerRiskDto(
                st.StudentId, st.Name, st.StudentNumber, st.ClassName,
                "no_data", new List<SubjectRiskDto>(), 0, 0, 0, null, 0, 0, 0);
        }).ToList();

        return (classes, learners);
    }

    private static (int Sessions, string Hint) SuggestWeeklyGoal(double average) => average switch
    {
        < 40 => (4, "Rebuild the fundamentals with your teacher, then work through one past-paper section per session."),
        < 50 => (3, "Close the gaps: revise core topics first, then move to timed past-paper practice."),
        < 60 => (2, "Consolidate: alternate topic revision with timed past papers and mark against the memo."),
        < 80 => (2, "Sharpen exam technique: timed past papers, then review the memo against your answers."),
        _    => (1, "Maintain: one timed past paper a week; review the memo and log anything you dropped marks on."),
    };

    public async Task<List<MatricQuizQuestionDto>> GetQuizQuestionsAsync(string subject, int count)
    {
        // Fetch all for subject then randomise in memory (small dataset)
        var all = await _context.MatricQuizQuestions
            .AsNoTracking()
            .Where(q => q.Subject == subject)
            .Select(q => new MatricQuizQuestionDto(
                q.MatricQuizQuestionId,
                q.Subject,
                q.Difficulty,
                q.QuestionText,
                q.OptionA,
                q.OptionB,
                q.OptionC,
                q.OptionD))
            .ToListAsync();

        return all.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
    }
}
