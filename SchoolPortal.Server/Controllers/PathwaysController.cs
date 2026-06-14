using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Learner pathways feature (own subjects/APS/goals/
// gap-analysis/Gr9) → pathways.view_own; reference data + a learner's subject lookup →
// platform.access; class subject-enrolment matrix → marks.view_class; subject enrol/withdraw →
// academics.manage (academic structure; tightened off rank-and-file teachers, cf. AS-3).
public class PathwaysController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPathwaysService _pathways;
    private readonly IAiGapAnalysisService _gapAnalysis;
    private readonly IGr9AdvisorService _gr9Advisor;

    public PathwaysController(
        SchoolPortalDbContext context,
        ICurrentUserService currentUser,
        IPathwaysService pathways,
        IAiGapAnalysisService gapAnalysis,
        IGr9AdvisorService gr9Advisor)
    {
        _context = context;
        _currentUser = currentUser;
        _pathways = pathways;
        _gapAnalysis = gapAnalysis;
        _gr9Advisor = gr9Advisor;
    }

    // ── Existing subject enrolment endpoints (unchanged) ─────────────────────

    // GET /api/pathways/learner/{studentId}
    [HttpGet("learner/{studentId}")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetLearnerSubjects(Guid studentId)
    {
        var schoolId = _currentUser.SchoolId;

        if (_currentUser.Role == "Student")
        {
            var myStudentId = await _context.Students
                .Where(s => s.UserId == _currentUser.UserId)
                .Select(s => (Guid?)s.StudentId)
                .FirstOrDefaultAsync();
            if (myStudentId != studentId) return Forbid();
        }

        var subjects = await _context.LearnerSubjects
            .AsNoTracking()
            .Where(ls => ls.StudentId == studentId && ls.SchoolId == schoolId)
            .Include(ls => ls.Subject)
            .Include(ls => ls.AcademicYear)
            .OrderBy(ls => ls.AcademicYear.Year)
            .ThenBy(ls => ls.Subject.Name)
            .Select(ls => new
            {
                ls.LearnerSubjectId,
                ls.SubjectId,
                SubjectName = ls.Subject.Name,
                SubjectCode = ls.Subject.Code,
                CapsPhase = ls.Subject.CapsPhase,
                Year = ls.AcademicYear.Year,
                ls.EnrolledAt
            })
            .ToListAsync();

        return Ok(subjects);
    }

    // GET /api/pathways/mine
    [HttpGet("mine")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetMySubjects()
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => (Guid?)s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == null) return NotFound("Student record not found.");

        var subjects = await _context.LearnerSubjects
            .AsNoTracking()
            .Where(ls => ls.StudentId == studentId && ls.SchoolId == schoolId)
            .Include(ls => ls.Subject)
            .Include(ls => ls.AcademicYear)
            .OrderBy(ls => ls.AcademicYear.Year)
            .ThenBy(ls => ls.Subject.Name)
            .Select(ls => new
            {
                ls.LearnerSubjectId,
                ls.SubjectId,
                SubjectName = ls.Subject.Name,
                SubjectCode = ls.Subject.Code,
                CapsPhase = ls.Subject.CapsPhase,
                Year = ls.AcademicYear.Year,
                ls.EnrolledAt
            })
            .ToListAsync();

        return Ok(subjects);
    }

    // GET /api/pathways/class/{classId}
    [HttpGet("class/{classId}")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    public async Task<IActionResult> GetClassMatrix(Guid classId)
    {
        var schoolId = _currentUser.SchoolId;

        var currentYear = await _context.AcademicYears
            .AsNoTracking()
            .Where(y => y.SchoolId == schoolId)
            .OrderByDescending(y => y.Year)
            .Select(y => new { y.AcademicYearId, y.Year })
            .FirstOrDefaultAsync();

        if (currentYear == null) return Ok(new { AcademicYearId = (Guid?)null, Year = 0, Students = Array.Empty<object>(), Subjects = Array.Empty<object>() });

        var students = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.IsActive && e.SchoolId == schoolId)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .OrderBy(e => e.Student.User.LastName)
            .Select(e => new { e.Student.StudentId, Name = $"{e.Student.User.FirstName} {e.Student.User.LastName}", e.Student.StudentNumber })
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();

        var enrolments = await _context.LearnerSubjects
            .AsNoTracking()
            .Where(ls => ls.SchoolId == schoolId && ls.AcademicYearId == currentYear.AcademicYearId && studentIds.Contains(ls.StudentId))
            .Include(ls => ls.Subject)
            .Select(ls => new { ls.LearnerSubjectId, ls.StudentId, ls.SubjectId, SubjectName = ls.Subject.Name, CapsPhase = ls.Subject.CapsPhase })
            .ToListAsync();

        var allSubjects = enrolments.Select(e => new { e.SubjectId, e.SubjectName, e.CapsPhase }).Distinct().OrderBy(s => s.SubjectName).ToList();

        var rows = students.Select(s =>
        {
            var studentSubjects = enrolments.Where(e => e.StudentId == s.StudentId).Select(e => e.SubjectId).ToHashSet();
            return new { s.StudentId, s.Name, s.StudentNumber, EnrolledSubjectIds = studentSubjects.ToList() };
        });

        return Ok(new { AcademicYearId = currentYear.AcademicYearId, Year = currentYear.Year, Students = rows, Subjects = allSubjects });
    }

    // POST /api/pathways/enrol
    [HttpPost("enrol")]
    [RequirePermission(PermissionKeys.AcademicsManage)]
    public async Task<IActionResult> Enrol([FromBody] EnrolRequest request)
    {
        var schoolId = _currentUser.SchoolId;
        var existing = await _context.LearnerSubjects
            .FirstOrDefaultAsync(ls => ls.StudentId == request.StudentId && ls.SubjectId == request.SubjectId && ls.AcademicYearId == request.AcademicYearId);

        if (existing != null) return Conflict("Learner is already enrolled in this subject for the selected year.");

        var ls = new LearnerSubject
        {
            StudentId = request.StudentId,
            SubjectId = request.SubjectId,
            AcademicYearId = request.AcademicYearId,
            SchoolId = schoolId,
            EnrolledAt = DateTime.UtcNow
        };
        _context.LearnerSubjects.Add(ls);
        await _context.SaveChangesAsync();
        return Ok(new { ls.LearnerSubjectId });
    }

    // DELETE /api/pathways/{learnerSubjectId}
    [HttpDelete("{learnerSubjectId}")]
    [RequirePermission(PermissionKeys.AcademicsManage)]
    public async Task<IActionResult> Withdraw(Guid learnerSubjectId)
    {
        var ls = await _context.LearnerSubjects
            .FirstOrDefaultAsync(l => l.LearnerSubjectId == learnerSubjectId && l.SchoolId == _currentUser.SchoolId);

        if (ls == null) return NotFound();
        _context.LearnerSubjects.Remove(ls);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── New v1 endpoints ─────────────────────────────────────────────────────

    // GET /api/pathways/universities
    [HttpGet("universities")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetUniversities()
    {
        var universities = await _context.Universities
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new
            {
                u.UniversityId,
                u.Name,
                u.Abbreviation,
                u.Province,
                u.Website,
                CourseCount = u.Courses.Count
            })
            .ToListAsync();

        return Ok(universities);
    }

    // GET /api/pathways/universities/{id}/courses
    [HttpGet("universities/{id}/courses")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetUniversityCourses(Guid id)
    {
        var courses = await _context.UniversityCourses
            .AsNoTracking()
            .Where(c => c.UniversityId == id)
            .Include(c => c.Career)
            .Include(c => c.SubjectRequirements)
            .OrderBy(c => c.Faculty)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.UniversityCourseId,
                c.Name,
                c.Faculty,
                c.MinimumAps,
                c.ApsNotes,
                CareerName = c.Career != null ? c.Career.Name : null,
                CareerCategory = c.Career != null ? c.Career.Category : null,
                SubjectRequirements = c.SubjectRequirements
                    .Where(r => r.IsRequired)
                    .Select(r => new { r.SubjectName, r.MinimumPercent, r.Notes })
                    .ToList()
            })
            .ToListAsync();

        return Ok(courses);
    }

    // GET /api/pathways/careers
    [HttpGet("careers")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetCareers()
    {
        var careers = await _context.Careers
            .AsNoTracking()
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.CareerId,
                c.Name,
                c.Description,
                c.Category,
                CourseCount = c.Courses.Count
            })
            .ToListAsync();

        return Ok(careers);
    }

    // GET /api/pathways/aps — current learner's APS from live gradebook
    [HttpGet("aps")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetMyAps()
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == schoolId)
            .Select(s => (Guid?)s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == null) return NotFound("Student record not found.");

        var result = await _pathways.GetLearnerApsAsync(studentId.Value, schoolId);
        return Ok(result);
    }

    // GET /api/pathways/goals
    [HttpGet("goals")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetMyGoals()
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound("Student record not found.");

        var goals = await _pathways.GetLearnerGoalsAsync(studentId.Value, schoolId);
        return Ok(goals);
    }

    // POST /api/pathways/goals
    [HttpPost("goals")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> AddGoal([FromBody] AddGoalRequest request)
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound("Student record not found.");

        try
        {
            var goal = await _pathways.AddGoalAsync(studentId.Value, schoolId, request.UniversityCourseId);
            return Ok(goal);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // DELETE /api/pathways/goals/{goalId}
    [HttpDelete("goals/{goalId}")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> DeleteGoal(Guid goalId)
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound("Student record not found.");

        try
        {
            await _pathways.DeleteGoalAsync(goalId, studentId.Value, schoolId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // GET /api/pathways/goals/{goalId}/tracking
    [HttpGet("goals/{goalId}/tracking")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetGoalTracking(Guid goalId)
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound("Student record not found.");

        try
        {
            var tracking = await _pathways.GetGoalTrackingAsync(goalId, studentId.Value, schoolId);
            return Ok(tracking);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // POST /api/pathways/goals/{goalId}/gap-analysis
    [HttpPost("goals/{goalId}/gap-analysis")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetGapAnalysis(Guid goalId, [FromQuery] bool forceRefresh = false)
    {
        var schoolId = _currentUser.SchoolId;
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound("Student record not found.");

        var goal = await _context.LearnerCareerGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(g =>
                g.LearnerCareerGoalId == goalId &&
                g.StudentId == studentId &&
                g.SchoolId == schoolId);

        if (goal == null) return NotFound();

        var result = await _gapAnalysis.GetGapAnalysisAsync(
            studentId.Value, schoolId, goal.UniversityCourseId, forceRefresh);

        if (result == null)
            return Ok(new { available = false });

        return Ok(new { available = true, analysis = result });
    }

    // ── Grade 9 Subject Advisor ──────────────────────────────────────────────────

    // GET /api/pathways/gr9-profile [Student]
    [HttpGet("gr9-profile")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetGr9Profile()
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound();

        var profile = await _gr9Advisor.GetGr9ProfileAsync(studentId.Value, _currentUser.SchoolId);
        return Ok(profile);
    }

    // POST /api/pathways/gr9-advice?forceRefresh=bool [Student]
    [HttpPost("gr9-advice")]
    [RequirePermission(PermissionKeys.PathwaysViewOwn)]
    public async Task<IActionResult> GetGr9Advice([FromQuery] bool forceRefresh = false)
    {
        var studentId = await GetStudentIdAsync();
        if (studentId == null) return NotFound();

        var result = await _gr9Advisor.GetAiAdviceAsync(studentId.Value, _currentUser.SchoolId, forceRefresh);
        if (result == null)
            return Ok(new { available = false });

        return Ok(new { available = true, advice = result });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<Guid?> GetStudentIdAsync() =>
        await _context.Students
            .AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == _currentUser.SchoolId)
            .Select(s => (Guid?)s.StudentId)
            .FirstOrDefaultAsync();
}

public record EnrolRequest(Guid StudentId, Guid SubjectId, Guid AcademicYearId);
public record AddGoalRequest(Guid UniversityCourseId);
