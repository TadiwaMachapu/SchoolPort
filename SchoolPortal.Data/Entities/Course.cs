namespace SchoolPortal.Data.Entities;

public class Course
{
    public Guid CourseId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? ClassSubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsPublished { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual ClassSubject? ClassSubject { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<CourseModule> Modules { get; set; } = new List<CourseModule>();
}

public class CourseModule
{
    public Guid ModuleId { get; set; }
    public Guid CourseId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Course Course { get; set; } = null!;
    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}

public class Lesson
{
    public Guid LessonId { get; set; }
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!; // RichText, Video, PDF, Link
    public string? Content { get; set; }       // HTML for RichText, URL for Video/PDF/Link
    public string? VideoUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public int Order { get; set; }
    public int? DurationMinutes { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual CourseModule Module { get; set; } = null!;
}
