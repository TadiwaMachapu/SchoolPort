using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Courses;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + [Authorize(Roles="Admin,Teacher")] writes. Course views →
// platform.access; all content management (courses/modules/lessons) → courses.manage.
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(PaginatedResult<CourseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool publishedOnly = false)
    {
        var result = await _courseService.GetCoursesAsync(page, pageSize, publishedOnly);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourse(Guid id)
    {
        var course = await _courseService.GetCourseAsync(id);
        return Ok(course);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request)
    {
        var course = await _courseService.CreateCourseAsync(request);
        return CreatedAtAction(nameof(GetCourse), new { id = course.CourseId }, course);
    }

    [HttpPut("{id}/publish")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishCourse(Guid id, [FromQuery] bool publish = true)
    {
        var course = await _courseService.PublishCourseAsync(id, publish);
        return Ok(course);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCourse(Guid id)
    {
        await _courseService.DeleteCourseAsync(id);
        return NoContent();
    }

    // Modules
    [HttpPost("{courseId}/modules")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(typeof(CourseModuleDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddModule(Guid courseId, [FromBody] CreateModuleRequest request)
    {
        var module = await _courseService.AddModuleAsync(courseId, request);
        return CreatedAtAction(nameof(GetCourse), new { id = courseId }, module);
    }

    [HttpDelete("modules/{moduleId}")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteModule(Guid moduleId)
    {
        await _courseService.DeleteModuleAsync(moduleId);
        return NoContent();
    }

    [HttpPut("{courseId}/modules/reorder")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReorderModules(Guid courseId, [FromBody] List<Guid> orderedIds)
    {
        await _courseService.ReorderModulesAsync(courseId, orderedIds);
        return NoContent();
    }

    // Lessons
    [HttpPost("modules/{moduleId}/lessons")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(typeof(LessonDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddLesson(Guid moduleId, [FromBody] CreateLessonRequest request)
    {
        var lesson = await _courseService.AddLessonAsync(moduleId, request);
        return StatusCode(201, lesson);
    }

    [HttpPut("lessons/{lessonId}")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(typeof(LessonDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLesson(Guid lessonId, [FromBody] CreateLessonRequest request)
    {
        var lesson = await _courseService.UpdateLessonAsync(lessonId, request);
        return Ok(lesson);
    }

    [HttpDelete("lessons/{lessonId}")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLesson(Guid lessonId)
    {
        await _courseService.DeleteLessonAsync(lessonId);
        return NoContent();
    }

    [HttpPut("modules/{moduleId}/lessons/reorder")]
    [RequirePermission(PermissionKeys.CoursesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReorderLessons(Guid moduleId, [FromBody] List<Guid> orderedIds)
    {
        await _courseService.ReorderLessonsAsync(moduleId, orderedIds);
        return NoContent();
    }
}
