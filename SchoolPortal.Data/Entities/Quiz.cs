namespace SchoolPortal.Data.Entities;

public class Quiz
{
    public Guid QuizId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? ClassSubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public bool ShuffleQuestions { get; set; }
    public bool ShowResultsImmediately { get; set; } = true;
    public bool IsPublished { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    public virtual School School { get; set; } = null!;
    public virtual ClassSubject? ClassSubject { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public virtual ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}

public class QuizQuestion
{
    public Guid QuestionId { get; set; }
    public Guid QuizId { get; set; }
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!; // MultipleChoice, TrueFalse, ShortAnswer
    public int Order { get; set; }
    public decimal Marks { get; set; } = 1;
    public string? Explanation { get; set; }

    public virtual Quiz Quiz { get; set; } = null!;
    public virtual ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
    public virtual ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}

public class QuizOption
{
    public Guid OptionId { get; set; }
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public int Order { get; set; }

    public virtual QuizQuestion Question { get; set; } = null!;
}

public class QuizAttempt
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public decimal? Score { get; set; }
    public decimal? MaxScore { get; set; }
    public bool IsCompleted { get; set; }

    public virtual Quiz Quiz { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}

public class QuizAnswer
{
    public Guid AnswerId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? MarksAwarded { get; set; }

    public virtual QuizAttempt Attempt { get; set; } = null!;
    public virtual QuizQuestion Question { get; set; } = null!;
    public virtual QuizOption? SelectedOption { get; set; }
}
