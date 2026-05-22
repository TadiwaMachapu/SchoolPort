using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Courses;

namespace SchoolPortal.Server.Services;

public interface ICourseService
{
    Task<PaginatedResult<CourseDto>> GetCoursesAsync(int page, int pageSize, bool publishedOnly = false);
    Task<CourseDto> GetCourseAsync(Guid courseId);
    Task<CourseDto> CreateCourseAsync(CreateCourseRequest request);
    Task<CourseDto> PublishCourseAsync(Guid courseId, bool publish);
    Task DeleteCourseAsync(Guid courseId);
    Task<CourseModuleDto> AddModuleAsync(Guid courseId, CreateModuleRequest request);
    Task DeleteModuleAsync(Guid moduleId);
    Task<LessonDto> AddLessonAsync(Guid moduleId, CreateLessonRequest request);
    Task<LessonDto> UpdateLessonAsync(Guid lessonId, CreateLessonRequest request);
    Task DeleteLessonAsync(Guid lessonId);
    Task ReorderModulesAsync(Guid courseId, List<Guid> orderedModuleIds);
    Task ReorderLessonsAsync(Guid moduleId, List<Guid> orderedLessonIds);
}

public class CourseService : ICourseService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CourseService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<CourseDto>> GetCoursesAsync(int page, int pageSize, bool publishedOnly = false)
    {
        var query = _context.Courses
            .AsNoTracking()
            .Where(c => c.SchoolId == _currentUser.SchoolId);

        if (publishedOnly) query = query.Where(c => c.IsPublished);

        var total = await query.CountAsync();
        var items = await query
            .Include(c => c.CreatedByUser)
            .Include(c => c.Modules).ThenInclude(m => m.Lessons)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseDto
            {
                CourseId = c.CourseId,
                Title = c.Title,
                Description = c.Description,
                ThumbnailUrl = c.ThumbnailUrl,
                IsPublished = c.IsPublished,
                CreatedByName = $"{c.CreatedByUser.FirstName} {c.CreatedByUser.LastName}",
                CreatedAt = c.CreatedAt,
                ModuleCount = c.Modules.Count,
                LessonCount = c.Modules.Sum(m => m.Lessons.Count)
            })
            .ToListAsync();

        return new PaginatedResult<CourseDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<CourseDto> GetCourseAsync(Guid courseId)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId && c.SchoolId == _currentUser.SchoolId)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Modules.OrderBy(m => m.Order))
                .ThenInclude(m => m.Lessons.OrderBy(l => l.Order))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Course not found");

        return MapCourse(course);
    }

    public async Task<CourseDto> CreateCourseAsync(CreateCourseRequest request)
    {
        var course = new Course
        {
            SchoolId = _currentUser.SchoolId,
            ClassSubjectId = request.ClassSubjectId,
            Title = request.Title,
            Description = request.Description,
            ThumbnailUrl = request.ThumbnailUrl,
            IsPublished = false,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Courses.Add(course);
        await _context.SaveChangesAsync();
        await _context.Entry(course).Reference(c => c.CreatedByUser).LoadAsync();

        return MapCourse(course);
    }

    public async Task<CourseDto> PublishCourseAsync(Guid courseId, bool publish)
    {
        var course = await _context.Courses
            .Include(c => c.CreatedByUser)
            .Include(c => c.Modules).ThenInclude(m => m.Lessons)
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Course not found");

        course.IsPublished = publish;
        course.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapCourse(course);
    }

    public async Task DeleteCourseAsync(Guid courseId)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Course not found");

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
    }

    public async Task<CourseModuleDto> AddModuleAsync(Guid courseId, CreateModuleRequest request)
    {
        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Course not found");

        var module = new CourseModule
        {
            CourseId = courseId,
            Title = request.Title,
            Description = request.Description,
            Order = request.Order,
            CreatedAt = DateTime.UtcNow
        };

        _context.CourseModules.Add(module);
        await _context.SaveChangesAsync();

        return new CourseModuleDto
        {
            ModuleId = module.ModuleId,
            Title = module.Title,
            Description = module.Description,
            Order = module.Order,
            Lessons = new()
        };
    }

    public async Task DeleteModuleAsync(Guid moduleId)
    {
        var module = await _context.CourseModules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.Course.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Module not found");

        _context.CourseModules.Remove(module);
        await _context.SaveChangesAsync();
    }

    public async Task<LessonDto> AddLessonAsync(Guid moduleId, CreateLessonRequest request)
    {
        var module = await _context.CourseModules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.Course.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Module not found");

        var lesson = new Lesson
        {
            ModuleId = moduleId,
            Title = request.Title,
            Type = request.Type,
            Content = request.Content,
            VideoUrl = request.VideoUrl,
            FileUrl = request.FileUrl,
            ExternalUrl = request.ExternalUrl,
            Order = request.Order,
            DurationMinutes = request.DurationMinutes,
            IsPublished = request.IsPublished,
            CreatedAt = DateTime.UtcNow
        };

        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync();
        return MapLesson(lesson);
    }

    public async Task<LessonDto> UpdateLessonAsync(Guid lessonId, CreateLessonRequest request)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module).ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.Module.Course.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Lesson not found");

        lesson.Title = request.Title;
        lesson.Type = request.Type;
        lesson.Content = request.Content;
        lesson.VideoUrl = request.VideoUrl;
        lesson.FileUrl = request.FileUrl;
        lesson.ExternalUrl = request.ExternalUrl;
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.IsPublished = request.IsPublished;
        lesson.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapLesson(lesson);
    }

    public async Task DeleteLessonAsync(Guid lessonId)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module).ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.Module.Course.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Lesson not found");

        _context.Lessons.Remove(lesson);
        await _context.SaveChangesAsync();
    }

    public async Task ReorderModulesAsync(Guid courseId, List<Guid> orderedModuleIds)
    {
        var modules = await _context.CourseModules
            .Where(m => m.CourseId == courseId)
            .ToListAsync();

        for (int i = 0; i < orderedModuleIds.Count; i++)
        {
            var module = modules.FirstOrDefault(m => m.ModuleId == orderedModuleIds[i]);
            if (module != null) module.Order = i;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ReorderLessonsAsync(Guid moduleId, List<Guid> orderedLessonIds)
    {
        var lessons = await _context.Lessons
            .Where(l => l.ModuleId == moduleId)
            .ToListAsync();

        for (int i = 0; i < orderedLessonIds.Count; i++)
        {
            var lesson = lessons.FirstOrDefault(l => l.LessonId == orderedLessonIds[i]);
            if (lesson != null) lesson.Order = i;
        }

        await _context.SaveChangesAsync();
    }

    private static CourseDto MapCourse(Course c) => new()
    {
        CourseId = c.CourseId,
        Title = c.Title,
        Description = c.Description,
        ThumbnailUrl = c.ThumbnailUrl,
        IsPublished = c.IsPublished,
        CreatedByName = $"{c.CreatedByUser.FirstName} {c.CreatedByUser.LastName}",
        CreatedAt = c.CreatedAt,
        ModuleCount = c.Modules.Count,
        LessonCount = c.Modules.Sum(m => m.Lessons.Count),
        Modules = c.Modules.OrderBy(m => m.Order).Select(m => new CourseModuleDto
        {
            ModuleId = m.ModuleId,
            Title = m.Title,
            Description = m.Description,
            Order = m.Order,
            Lessons = m.Lessons.OrderBy(l => l.Order).Select(MapLesson).ToList()
        }).ToList()
    };

    private static LessonDto MapLesson(Lesson l) => new()
    {
        LessonId = l.LessonId,
        Title = l.Title,
        Type = l.Type,
        Content = l.Content,
        VideoUrl = l.VideoUrl,
        FileUrl = l.FileUrl,
        ExternalUrl = l.ExternalUrl,
        Order = l.Order,
        DurationMinutes = l.DurationMinutes,
        IsPublished = l.IsPublished
    };
}
