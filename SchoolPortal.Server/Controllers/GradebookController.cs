using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

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
