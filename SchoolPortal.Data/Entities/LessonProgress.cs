namespace SchoolPortal.Data.Entities;

public class LessonProgress
{
    public Guid ProgressId { get; set; }
    public Guid LessonId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? TimeSpentSeconds { get; set; }
    public DateTime LastAccessedAt { get; set; }

    public virtual Lesson Lesson { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
}

public class LearningPath
{
    public Guid PathId { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual ICollection<LearningPathCourse> Courses { get; set; } = new List<LearningPathCourse>();
}

public class LearningPathCourse
{
    public Guid PathCourseId { get; set; }
    public Guid PathId { get; set; }
    public Guid CourseId { get; set; }
    public int Order { get; set; }
    public Guid? PrerequisiteCourseId { get; set; }

    public virtual LearningPath Path { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
}
