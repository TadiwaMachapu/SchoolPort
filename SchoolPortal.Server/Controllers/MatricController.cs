using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Staff Gr12 dashboard (named learners) →
// marks.view_class; learner's own NSC status → marks.view_own; study tools (subjects, past
// papers, quiz, NSC requirements) → platform.access. AI tutor (Sprint 1.5.2 v2) → ai.tutor
// (Learner identity-implicit + marks.view_class staff cluster), day-capped per learner and
// cost-capped via School.Settings.AiMonthlyCostCapZar.
public class MatricController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IMatricHubService _hub;
    private readonly IMatricTutorService _tutor;
    private readonly IScopeService _scope;

    public MatricController(
        SchoolPortalDbContext context,
        ICurrentUserService currentUser,
        IMatricHubService hub,
        IMatricTutorService tutor,
        IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _hub = hub;
        _tutor = tutor;
        _scope = scope;
    }

    // GET /api/matric/dashboard?classId= [Admin, Teacher]
    [HttpGet("dashboard")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid? classId)
    {
        var schoolId = _currentUser.SchoolId;

        // Get Gr 12 classes
        var classQuery = _context.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == schoolId && c.GradeLevel == 12);

        if (classId.HasValue)
            classQuery = classQuery.Where(c => c.ClassId == classId.Value);

        // Step 7: IDOR on an explicit class + scope the dashboard to the caller's classes
        // (oversight = null = all Gr12; a teacher sees only their own Gr12 classes).
        if (classId.HasValue && !await _scope.CanAccessClassAsync(classId.Value)) return NotFound();
        var accessible = await _scope.GetAccessibleClassIdsAsync();
        if (accessible is not null)
            classQuery = classQuery.Where(c => accessible.Contains(c.ClassId));

        var classes = await classQuery.Select(c => new { c.ClassId, c.Name }).ToListAsync();

        if (!classes.Any())
            return Ok(new { classes = Array.Empty<object>(), learners = Array.Empty<object>() });

        var classIds = classes.Select(c => c.ClassId).ToList();

        // Get enrolled students in those classes
        var students = await _context.Enrollments
            .AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.IsActive && e.SchoolId == schoolId)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Select(e => new
            {
                e.Student.StudentId,
                Name = $"{e.Student.User.FirstName} {e.Student.User.LastName}",
                e.Student.StudentNumber,
                ClassId = e.ClassId,
                ClassName = e.Class.Name
            })
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();

        // Get all grades for these students
        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId && studentIds.Contains(g.Submission.StudentId))
            .Select(g => new
            {
                StudentId = g.Submission.StudentId,
                SubjectName = g.Submission.Assignment.ClassSubject.Subject.Name,
                CapsPhase = g.Submission.Assignment.ClassSubject.Subject.CapsPhase,
                Percentage = g.Submission.Assignment.MaxMarks > 0
                    ? Math.Round((double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100, 1)
                    : 0.0
            })
            .ToListAsync();

        // Build learner result rows
        var learners = students.Select(s =>
        {
            var sGrades = grades.Where(g => g.StudentId == s.StudentId).ToList();
            var bySubject = sGrades
                .GroupBy(g => new { g.SubjectName, g.CapsPhase })
                .Select(g =>
                {
                    var avg = Math.Round(g.Average(x => x.Percentage), 1);
                    return new
                    {
                        SubjectName = g.Key.SubjectName,
                        CapsPhase = g.Key.CapsPhase,
                        Average = avg,
                        Status = NscStatus(avg)
                    };
                })
                .OrderBy(x => x.SubjectName)
                .ToList();

            var passCount = bySubject.Count(x => x.Status == "Pass");
            var atRiskCount = bySubject.Count(x => x.Status == "AtRisk");
            var failCount = bySubject.Count(x => x.Status == "Fail");

            var overall = failCount > 0 ? "Fail"
                        : atRiskCount > 0 ? "AtRisk"
                        : bySubject.Count > 0 ? "Pass"
                        : "NoData";

            return new
            {
                s.StudentId,
                s.Name,
                s.StudentNumber,
                s.ClassName,
                Subjects = bySubject,
                PassCount = passCount,
                AtRiskCount = atRiskCount,
                FailCount = failCount,
                OverallStatus = overall
            };
        }).ToList();

        return Ok(new { Classes = classes, Learners = learners });
    }

    // GET /api/matric/risk-dashboard?classId= [Staff — marks.view_class] (Week 2 Feature 5).
    // Scoped via IScopeService: a teacher sees their own Gr12 classes, oversight sees all.
    // classId only narrows WITHIN the caller's scope (out-of-scope id → 404, like GetDashboard).
    [HttpGet("risk-dashboard")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetRiskDashboard([FromQuery] Guid? classId)
    {
        if (classId.HasValue && !await _scope.CanAccessClassAsync(classId.Value)) return NotFound();
        var accessible = await _scope.GetAccessibleClassIdsAsync();
        return Ok(await _hub.GetRiskDashboardAsync(_currentUser.SchoolId, accessible, classId));
    }

    // GET /api/matric/grade-overview [Staff — marks.view_class] (Week 2 Feature 6).
    // Takes NO ids; everything derives from the caller's scope — a GradeHead's grade scope
    // covers their whole grade's classes, a teacher sees only their own Gr12 classes.
    [HttpGet("grade-overview")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetGradeOverview()
    {
        var accessible = await _scope.GetAccessibleClassIdsAsync();
        return Ok(await _hub.GetGradeOverviewAsync(_currentUser.SchoolId, accessible));
    }

    // GET /api/matric/mine [Student]
    [HttpGet("mine")]
    [RequirePermission(PermissionKeys.MarksViewOwn)]
    public async Task<IActionResult> GetMine()
    {
        var schoolId = _currentUser.SchoolId;

        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => new { s.StudentId, s.StudentNumber })
            .FirstOrDefaultAsync();

        if (student == null) return NotFound();

        // Check enrolled in a Gr 12 class
        var inGr12 = await _context.Enrollments
            .AnyAsync(e => e.StudentId == student.StudentId && e.IsActive && e.Class.GradeLevel == 12);

        if (!inGr12)
            return Ok(new { isGrade12 = false, subjects = Array.Empty<object>() });

        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId && g.Submission.StudentId == student.StudentId)
            .Select(g => new
            {
                SubjectName = g.Submission.Assignment.ClassSubject.Subject.Name,
                CapsPhase = g.Submission.Assignment.ClassSubject.Subject.CapsPhase,
                Percentage = g.Submission.Assignment.MaxMarks > 0
                    ? Math.Round((double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100, 1)
                    : 0.0
            })
            .ToListAsync();

        var bySubject = grades
            .GroupBy(g => new { g.SubjectName, g.CapsPhase })
            .Select(g =>
            {
                var avg = Math.Round(g.Average(x => x.Percentage), 1);
                return new
                {
                    SubjectName = g.Key.SubjectName,
                    CapsPhase = g.Key.CapsPhase,
                    Average = avg,
                    Status = NscStatus(avg)
                };
            })
            .OrderBy(x => x.SubjectName)
            .ToList();

        return Ok(new
        {
            IsGrade12 = true,
            Subjects = bySubject,
            PassCount = bySubject.Count(x => x.Status == "Pass"),
            AtRiskCount = bySubject.Count(x => x.Status == "AtRisk"),
            FailCount = bySubject.Count(x => x.Status == "Fail"),
            OverallStatus = bySubject.Any(x => x.Status == "Fail") ? "Fail"
                          : bySubject.Any(x => x.Status == "AtRisk") ? "AtRisk"
                          : bySubject.Any() ? "Pass" : "NoData"
        });
    }

    // GET /api/matric/subjects [Student]
    [HttpGet("subjects")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects = await _hub.GetSubjectsAsync();
        return Ok(subjects);
    }

    // GET /api/matric/past-papers?subject= [Student]
    [HttpGet("past-papers")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetPastPapers([FromQuery] string? subject)
    {
        var papers = await _hub.GetPastPapersAsync(subject);
        return Ok(papers);
    }

    // GET /api/matric/study-plan [Student] — countdown + per-subject weekly goals from own
    // marks (Step 4). Reads the caller's own averages → marks.view_own, like GetMine.
    [HttpGet("study-plan")]
    [RequirePermission(PermissionKeys.MarksViewOwn)]
    public async Task<IActionResult> GetStudyPlan()
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => (Guid?)s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == null) return NotFound();

        return Ok(await _hub.GetStudyPlanAsync(studentId.Value, schoolId));
    }

    // GET /api/matric/nsc-requirements [Any] — static national policy catalogue (Step 2).
    [HttpGet("nsc-requirements")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public IActionResult GetNscRequirements() => Ok(new
    {
        SubjectRules = NscRequirementsCatalogue.SubjectRules,
        PassLevels = NscRequirementsCatalogue.PassLevels,
        AchievementLevels = NscRequirementsCatalogue.AchievementLevels,
    });

    // GET /api/matric/quiz?subject=&count=10 [Student]
    [HttpGet("quiz")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetQuiz([FromQuery] string subject, [FromQuery] int count = 10)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return BadRequest("subject is required");

        count = Math.Clamp(count, 1, 20);
        var questions = await _hub.GetQuizQuestionsAsync(subject, count);
        return Ok(questions);
    }

    // POST /api/matric/tutor [Learner identity-implicit; staff via marks.view_class cluster]
    // Tutor v2 (Step 3): ai.tutor permission; learners are day-capped (default 20 successful
    // answers, School.Settings.MatricTutorDailyLimit), staff testers have no Student row and
    // are bounded by the monthly AI cost cap instead.
    [HttpPost("tutor")]
    [RequirePermission(PermissionKeys.AiTutor)]
    public async Task<IActionResult> AskTutor([FromBody] TutorRequest request, [FromQuery] bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("subject and question are required");

        var schoolId = _currentUser.SchoolId;
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => (Guid?)s.StudentId)
            .FirstOrDefaultAsync();

        var result = await _tutor.GetExplanationAsync(studentId, schoolId, request.Subject, request.Question, forceRefresh);
        return Ok(new
        {
            available = result.Available,
            reason = result.Reason,
            answer = result.AnswerMarkdown,
            fromCache = result.FromCache,
            remainingToday = result.RemainingToday,
        });
    }

    private static string NscStatus(double average) => average switch
    {
        >= 40 => "Pass",
        >= 30 => "AtRisk",
        _ => "Fail"
    };
}

public record TutorRequest(string Subject, string Question);
