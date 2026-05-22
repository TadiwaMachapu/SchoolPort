using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProgressController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ProgressController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Mark a lesson as complete
    [HttpPost("lessons/{lessonId}/complete")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CompleteLesson(Guid lessonId, [FromQuery] int? timeSpentSeconds)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == Guid.Empty) return BadRequest("Student not found");

        var progress = await _context.LessonProgress
            .FirstOrDefaultAsync(p => p.LessonId == lessonId && p.StudentId == studentId);

        if (progress == null)
        {
            progress = new LessonProgress
            {
                LessonId = lessonId,
                StudentId = studentId,
                SchoolId = _currentUser.SchoolId,
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow,
                TimeSpentSeconds = timeSpentSeconds,
                LastAccessedAt = DateTime.UtcNow
            };
            _context.LessonProgress.Add(progress);
        }
        else
        {
            progress.IsCompleted = true;
            progress.CompletedAt ??= DateTime.UtcNow;
            progress.LastAccessedAt = DateTime.UtcNow;
            if (timeSpentSeconds.HasValue)
                progress.TimeSpentSeconds = (progress.TimeSpentSeconds ?? 0) + timeSpentSeconds.Value;
        }

        await _context.SaveChangesAsync();
        return Ok(new { progress.ProgressId, progress.IsCompleted, progress.CompletedAt });
    }

    // Get course completion % for current student
    [HttpGet("courses/{courseId}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetCourseProgress(Guid courseId)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        var totalLessons = await _context.Lessons
            .CountAsync(l => l.Module.CourseId == courseId && l.IsPublished);

        var completedLessons = await _context.LessonProgress
            .CountAsync(p => p.Lesson.Module.CourseId == courseId &&
                             p.StudentId == studentId && p.IsCompleted);

        var percentage = totalLessons > 0 ? Math.Round((double)completedLessons / totalLessons * 100, 1) : 0d;

        return Ok(new
        {
            CourseId = courseId,
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            Percentage = percentage,
            IsComplete = percentage >= 100
        });
    }

    // Teacher view: all students' progress on a course
    [HttpGet("courses/{courseId}/all-students")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAllStudentsProgress(Guid courseId)
    {
        var schoolId = _currentUser.SchoolId;

        var totalLessons = await _context.Lessons
            .CountAsync(l => l.Module.CourseId == courseId && l.IsPublished);

        var progresses = await _context.LessonProgress
            .AsNoTracking()
            .Where(p => p.Lesson.Module.CourseId == courseId && p.SchoolId == schoolId && p.IsCompleted)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Completed = g.Count() })
            .ToListAsync();

        var studentIds = progresses.Select(p => p.StudentId).ToList();
        var students = await _context.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.StudentId) && s.SchoolId == schoolId)
            .Include(s => s.User)
            .Select(s => new { s.StudentId, Name = $"{s.User.FirstName} {s.User.LastName}", s.StudentNumber })
            .ToListAsync();

        var result = students.Select(s => new
        {
            s.StudentId,
            s.Name,
            s.StudentNumber,
            CompletedLessons = progresses.FirstOrDefault(p => p.StudentId == s.StudentId)?.Completed ?? 0,
            TotalLessons = totalLessons,
            Percentage = totalLessons > 0
                ? Math.Round((double)(progresses.FirstOrDefault(p => p.StudentId == s.StudentId)?.Completed ?? 0) / totalLessons * 100, 1)
                : 0d
        }).ToList();

        return Ok(result);
    }

    // Learning Paths
    [HttpGet("learning-paths")]
    public async Task<IActionResult> GetLearningPaths()
    {
        var paths = await _context.LearningPaths
            .AsNoTracking()
            .Where(p => p.SchoolId == _currentUser.SchoolId && p.IsPublished)
            .Include(p => p.Courses).ThenInclude(c => c.Course)
            .Select(p => new
            {
                p.PathId,
                p.Title,
                p.Description,
                CourseCount = p.Courses.Count,
                Courses = p.Courses.OrderBy(c => c.Order).Select(c => new
                {
                    c.CourseId,
                    c.Course.Title,
                    c.Order,
                    c.PrerequisiteCourseId
                })
            })
            .ToListAsync();

        return Ok(paths);
    }
}
