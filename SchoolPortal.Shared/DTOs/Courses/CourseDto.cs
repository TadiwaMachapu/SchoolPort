namespace SchoolPortal.Shared.DTOs.Courses;

public class CourseDto
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsPublished { get; set; }
    public string CreatedByName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int ModuleCount { get; set; }
    public int LessonCount { get; set; }
    public List<CourseModuleDto> Modules { get; set; } = new();
}

public class CourseModuleDto
{
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Order { get; set; }
    public List<LessonDto> Lessons { get; set; } = new();
}

public class LessonDto
{
    public Guid LessonId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Content { get; set; }
    public string? VideoUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public int Order { get; set; }
    public int? DurationMinutes { get; set; }
    public bool IsPublished { get; set; }
}

public class CreateCourseRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid? ClassSubjectId { get; set; }
}

public class CreateModuleRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Order { get; set; }
}

public class CreateLessonRequest
{
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!; // RichText | Video | PDF | Link
    public string? Content { get; set; }
    public string? VideoUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public int Order { get; set; }
    public int? DurationMinutes { get; set; }
    public bool IsPublished { get; set; }
}
