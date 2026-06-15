using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Academics;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Class gradebook/category views → marks.view_class;
// learner's own grades → marks.view_own; setting category weights → assessment.create.
public class GradebookController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IScopeService _scope;

    public GradebookController(SchoolPortalDbContext context, ICurrentUserService currentUser, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _scope = scope;
    }

    // Full gradebook matrix for a class — teacher/admin view
    [HttpGet("{classId}")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetGradebook(Guid classId, [FromQuery] Guid? termId)
    {
        if (!await _scope.CanAccessClassAsync(classId)) return NotFound(); // Step 7 IDOR
        var schoolId = _currentUser.SchoolId;

        DateTime? termStart = null;
        DateTime? termEnd = null;
        if (termId.HasValue)
        {
            var term = await _context.Terms
                .AsNoTracking()
                .Where(t => t.TermId == termId.Value && t.SchoolId == schoolId)
                .Select(t => new { t.StartDate, t.EndDate })
                .FirstOrDefaultAsync();
            if (term != null) { termStart = term.StartDate; termEnd = term.EndDate; }
        }

        var students = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.IsActive && e.SchoolId == schoolId)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .OrderBy(e => e.Student.User.LastName)
            .Select(e => new { e.Student.StudentId, Name = $"{e.Student.User.FirstName} {e.Student.User.LastName}", e.Student.StudentNumber })
            .ToListAsync();

        var assignmentsQuery = _context.Assignments
            .AsNoTracking()
            .Where(a => a.ClassSubject.ClassId == classId && a.SchoolId == schoolId);

        if (termStart.HasValue && termEnd.HasValue)
            assignmentsQuery = assignmentsQuery.Where(a => a.DueAt >= termStart.Value && a.DueAt <= termEnd.Value);

        var assignments = await assignmentsQuery
            .Include(a => a.ClassSubject).ThenInclude(cs => cs.Subject)
            .OrderBy(a => a.DueAt)
            .Select(a => new { a.AssignmentId, a.Title, a.MaxMarks, Subject = a.ClassSubject.Subject.Name, CapsPhase = a.ClassSubject.Subject.CapsPhase })
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();
        var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();

        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.Submission.Assignment.ClassSubject.ClassId == classId && g.SchoolId == schoolId)
            .Select(g => new
            {
                g.Submission.StudentId,
                g.Submission.AssignmentId,
                g.Score,
                g.Feedback
            })
            .ToListAsync();

        // Grade categories for weighted average
        var categories = await _context.GradeCategories
            .AsNoTracking()
            .Where(c => c.ClassSubject.ClassId == classId)
            .Select(c => new { c.Name, c.Weight })
            .ToListAsync();

        var gradeLookup = grades.ToDictionary(g => (g.StudentId, g.AssignmentId));

        var rows = students.Select(s =>
        {
            var studentGrades = assignments.Select(a =>
            {
                var grade = gradeLookup.GetValueOrDefault((s.StudentId, a.AssignmentId));
                return new
                {
                    a.AssignmentId,
                    Score = grade?.Score,
                    MaxMarks = a.MaxMarks,
                    Percentage = grade != null ? Math.Round((double)grade.Score / (double)a.MaxMarks * 100, 1) : (double?)null
                };
            }).ToList();

            var completedGrades = studentGrades.Where(g => g.Score.HasValue).ToList();
            var average = completedGrades.Count > 0 ? completedGrades.Average(g => g.Percentage) : (double?)null;

            return new { s.StudentId, s.Name, s.StudentNumber, Grades = studentGrades, Average = average };
        }).ToList();

        return Ok(new { Students = rows, Assignments = assignments, Categories = categories });
    }

    // Student's own grade summary
    [HttpGet("my-grades")]
    [RequirePermission(PermissionKeys.MarksViewOwn)]
    public async Task<IActionResult> GetMyGrades()
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        var grades = await _context.Grades
            .AsNoTracking()
            .Where(g => g.Submission.StudentId == studentId && g.SchoolId == _currentUser.SchoolId)
            .Include(g => g.Submission).ThenInclude(s => s.Assignment).ThenInclude(a => a.ClassSubject).ThenInclude(cs => cs.Subject)
            .Include(g => g.Submission).ThenInclude(s => s.Assignment).ThenInclude(a => a.ClassSubject).ThenInclude(cs => cs.Class)
            .OrderByDescending(g => g.GradedAt)
            .Select(g => new
            {
                g.GradeId,
                g.Score,
                MaxMarks = g.Submission.Assignment.MaxMarks,
                Percentage = Math.Round((double)g.Score / (double)g.Submission.Assignment.MaxMarks * 100, 1),
                AssignmentTitle = g.Submission.Assignment.Title,
                Subject = g.Submission.Assignment.ClassSubject.Subject.Name,
                Class = g.Submission.Assignment.ClassSubject.Class.Name,
                g.Feedback,
                g.GradedAt
            })
            .ToListAsync();

        return Ok(grades);
    }

    // Learner's own aggregated academics — backs the My Academics page (Subjects / Marks /
    // Assignments tabs) in ONE call. Own data only (filtered to the caller's StudentId), so no
    // ScopeService needed; MarksViewOwn is the Learner-implicit gate. Percentages only — CAPS
    // codes (1-7) are derived client-side. Assignments + quizzes are unified into Tasks.
    [HttpGet("my-academics")]
    [RequirePermission(PermissionKeys.MarksViewOwn)]
    public async Task<IActionResult> GetMyAcademics()
    {
        var schoolId = _currentUser.SchoolId;

        var studentId = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();
        if (studentId == Guid.Empty) return Ok(new MyAcademicsResponse());

        // Terms — restricted to the current academic year so the Term 1-4 selector and the
        // date→term mapping don't collide across years.
        var allTerms = await _context.Terms
            .AsNoTracking()
            .Where(t => t.SchoolId == schoolId)
            .OrderBy(t => t.StartDate)
            .Select(t => new { t.TermId, t.AcademicYearId, t.TermNumber, t.IsCurrent, t.StartDate, t.EndDate })
            .ToListAsync();
        var current = allTerms.FirstOrDefault(t => t.IsCurrent);
        var terms = current != null
            ? allTerms.Where(t => t.AcademicYearId == current.AcademicYearId).ToList()
            : allTerms;

        int? TermOf(DateTime? d) => d == null
            ? null
            : terms.FirstOrDefault(t => d >= t.StartDate && d <= t.EndDate)?.TermNumber;

        // Learner's subjects = class-subjects of their active-enrolment classes.
        var classIds = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.IsActive && e.SchoolId == schoolId)
            .Select(e => e.ClassId)
            .ToListAsync();

        var classSubjects = await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => classIds.Contains(cs.ClassId) && cs.SchoolId == schoolId)
            .Select(cs => new
            {
                cs.ClassSubjectId,
                SubjectName = cs.Subject.Name,
                cs.Subject.CapsPhase,
                TeacherName = cs.Teacher != null ? cs.Teacher.User.FirstName + " " + cs.Teacher.User.LastName : null,
            })
            .ToListAsync();
        var csIds = classSubjects.Select(c => c.ClassSubjectId).ToList();

        var assignments = await _context.Assignments
            .AsNoTracking()
            .Where(a => csIds.Contains(a.ClassSubjectId) && a.SchoolId == schoolId)
            .Select(a => new
            {
                a.AssignmentId,
                a.ClassSubjectId,
                a.Title,
                a.TaskType,
                a.DueAt,
                a.MaxMarks,
                SubjectName = a.ClassSubject.Subject.Name,
            })
            .ToListAsync();
        var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();

        var submissions = await _context.Submissions
            .AsNoTracking()
            .Where(s => s.StudentId == studentId && assignmentIds.Contains(s.AssignmentId))
            .Select(s => new
            {
                s.AssignmentId,
                s.SubmittedAt,
                GradeScore = s.Grade != null ? (decimal?)s.Grade.Score : null,
                GradedAt = s.Grade != null ? (DateTime?)s.Grade.GradedAt : null,
            })
            .ToListAsync();
        var subByAssignment = submissions.ToDictionary(s => s.AssignmentId);

        var quizzes = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.ClassSubjectId != null && csIds.Contains(q.ClassSubjectId.Value)
                        && q.IsPublished && q.SchoolId == schoolId)
            .Select(q => new
            {
                q.QuizId,
                ClassSubjectId = q.ClassSubjectId!.Value,
                q.Title,
                q.CreatedAt,
                SubjectName = q.ClassSubject!.Subject.Name,
            })
            .ToListAsync();
        var quizIds = quizzes.Select(q => q.QuizId).ToList();

        var attemptsRaw = await _context.QuizAttempts
            .AsNoTracking()
            .Where(at => at.StudentId == studentId && quizIds.Contains(at.QuizId) && at.IsCompleted)
            .Select(at => new { at.QuizId, at.Score, at.MaxScore, at.SubmittedAt })
            .ToListAsync();
        var attemptByQuiz = attemptsRaw
            .GroupBy(a => a.QuizId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SubmittedAt).First());

        var tasks = new List<MyAcademicsTask>();

        foreach (var a in assignments)
        {
            subByAssignment.TryGetValue(a.AssignmentId, out var sub);
            var graded = sub?.GradeScore != null;
            var submitted = sub != null;
            double? percent = graded && a.MaxMarks > 0
                ? Math.Round((double)sub!.GradeScore!.Value / (double)a.MaxMarks * 100, 1)
                : null;
            tasks.Add(new MyAcademicsTask
            {
                TaskId = a.AssignmentId,
                Source = "assignment",
                ClassSubjectId = a.ClassSubjectId,
                SubjectName = a.SubjectName,
                Title = a.Title,
                Type = a.TaskType.ToString(),
                TermNumber = TermOf(a.DueAt),
                Date = sub?.GradedAt ?? sub?.SubmittedAt ?? a.DueAt,
                DueAt = a.DueAt,
                Score = graded ? sub!.GradeScore : null,
                OutOf = a.MaxMarks,
                Percent = percent,
                Status = graded ? "graded" : submitted ? "submitted" : "not_submitted",
            });
        }

        foreach (var q in quizzes)
        {
            attemptByQuiz.TryGetValue(q.QuizId, out var at);
            var completed = at != null;
            double? percent = completed && at!.Score != null && at.MaxScore != null && at.MaxScore.Value > 0
                ? Math.Round((double)at.Score!.Value / (double)at.MaxScore!.Value * 100, 1)
                : null;
            tasks.Add(new MyAcademicsTask
            {
                TaskId = q.QuizId,
                Source = "quiz",
                ClassSubjectId = q.ClassSubjectId,
                SubjectName = q.SubjectName,
                Title = q.Title,
                Type = "Quiz",
                TermNumber = TermOf(at?.SubmittedAt ?? q.CreatedAt),
                Date = at?.SubmittedAt,
                DueAt = null,
                Score = at?.Score,
                OutOf = at?.MaxScore,
                Percent = percent,
                Status = completed ? "graded" : "not_submitted",
            });
        }

        // Subject summary — current-term formal (assignment) tasks only; quizzes are formative and
        // surface in the task list but don't drive the term average. Trend = current vs previous term.
        int? curTermNo = current?.TermNumber;
        int? prevTermNo = curTermNo.HasValue ? curTermNo - 1 : null;

        var subjects = classSubjects.Select(cs =>
        {
            var curTasks = tasks.Where(t => t.ClassSubjectId == cs.ClassSubjectId && t.Source == "assignment"
                && (curTermNo == null || t.TermNumber == curTermNo)).ToList();
            var gradedPercents = curTasks.Where(t => t.Percent != null).Select(t => t.Percent!.Value).ToList();
            double? curAvg = gradedPercents.Count > 0 ? Math.Round(gradedPercents.Average(), 1) : null;

            var trend = "none";
            if (curAvg != null && prevTermNo != null)
            {
                var prev = tasks.Where(t => t.ClassSubjectId == cs.ClassSubjectId && t.Source == "assignment"
                    && t.TermNumber == prevTermNo && t.Percent != null).Select(t => t.Percent!.Value).ToList();
                if (prev.Count > 0)
                {
                    var prevAvg = prev.Average();
                    trend = curAvg > prevAvg + 1 ? "up" : curAvg < prevAvg - 1 ? "down" : "flat";
                }
            }

            return new MyAcademicsSubject
            {
                ClassSubjectId = cs.ClassSubjectId,
                SubjectName = cs.SubjectName,
                TeacherName = cs.TeacherName,
                CapsPhase = cs.CapsPhase,
                TermAveragePercent = curAvg,
                TasksAssessed = gradedPercents.Count,
                TasksTotal = curTasks.Count,
                Trend = trend,
            };
        }).OrderBy(s => s.SubjectName).ToList();

        return Ok(new MyAcademicsResponse
        {
            CurrentTerm = current == null ? null
                : new MyAcademicsTerm { TermId = current.TermId, TermNumber = current.TermNumber, IsCurrent = true },
            Terms = terms.Select(t => new MyAcademicsTerm
            {
                TermId = t.TermId, TermNumber = t.TermNumber, IsCurrent = t.IsCurrent,
            }).ToList(),
            Subjects = subjects,
            Tasks = tasks.OrderBy(t => t.DueAt ?? DateTime.MaxValue).ToList(),
        });
    }

    // Grade categories (weighted marking)
    [HttpGet("categories/{classSubjectId}")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetCategories(Guid classSubjectId)
    {
        if (!await CanAccessClassSubjectAsync(classSubjectId)) return NotFound(); // Step 7 IDOR
        var categories = await _context.GradeCategories
            .AsNoTracking()
            .Where(c => c.ClassSubjectId == classSubjectId)
            .ToListAsync();

        return Ok(categories);
    }

    [HttpPost("categories/{classSubjectId}")]
    [RequirePermission(PermissionKeys.AssessmentCreate)]
    public async Task<IActionResult> SetCategories(Guid classSubjectId, [FromBody] List<SetCategoryRequest> request)
    {
        if (!await CanAccessClassSubjectAsync(classSubjectId)) return Forbid(); // Step 7 IDOR (write)
        var totalWeight = request.Sum(r => r.Weight);
        if (Math.Abs(totalWeight - 1.0m) > 0.01m)
            return BadRequest("Category weights must sum to 1.0 (100%)");

        // Replace all categories for this class subject
        var existing = _context.GradeCategories.Where(c => c.ClassSubjectId == classSubjectId);
        _context.GradeCategories.RemoveRange(existing);

        _context.GradeCategories.AddRange(request.Select(r => new GradeCategory
        {
            ClassSubjectId = classSubjectId,
            Name = r.Name,
            Weight = r.Weight,
            CreatedAt = DateTime.UtcNow
        }));

        await _context.SaveChangesAsync();
        return Ok();
    }

    // Step 7: a class-subject is in scope iff its parent class is in the caller's scope.
    private async Task<bool> CanAccessClassSubjectAsync(Guid classSubjectId)
    {
        var classId = await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => cs.ClassSubjectId == classSubjectId && cs.SchoolId == _currentUser.SchoolId)
            .Select(cs => (Guid?)cs.ClassId)
            .FirstOrDefaultAsync();
        return classId is not null && await _scope.CanAccessClassAsync(classId.Value);
    }
}

public record SetCategoryRequest(string Name, decimal Weight);
