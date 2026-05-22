namespace SchoolPortal.Shared.DTOs.Quizzes;

public class QuizDto
{
    public Guid QuizId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int MaxAttempts { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShowResultsImmediately { get; set; }
    public bool IsPublished { get; set; }
    public string CreatedByName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int QuestionCount { get; set; }
    public List<QuizQuestionDto> Questions { get; set; } = new();
}

public class QuizQuestionDto
{
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Order { get; set; }
    public decimal Marks { get; set; }
    public string? Explanation { get; set; }
    public List<QuizOptionDto> Options { get; set; } = new();
}

public class QuizOptionDto
{
    public Guid OptionId { get; set; }
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; } // only sent to teachers
    public int Order { get; set; }
}

public class QuizAttemptDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public decimal? Score { get; set; }
    public decimal? MaxScore { get; set; }
    public bool IsCompleted { get; set; }
    public decimal? Percentage { get; set; }
    public List<QuizAnswerDto> Answers { get; set; } = new();
}

public class QuizAnswerDto
{
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? MarksAwarded { get; set; }
}

public class CreateQuizRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public bool ShuffleQuestions { get; set; }
    public bool ShowResultsImmediately { get; set; } = true;
    public Guid? ClassSubjectId { get; set; }
    public List<CreateQuizQuestionRequest> Questions { get; set; } = new();
}

public class CreateQuizQuestionRequest
{
    public string Text { get; set; } = null!;
    public string Type { get; set; } = "MultipleChoice";
    public int Order { get; set; }
    public decimal Marks { get; set; } = 1;
    public string? Explanation { get; set; }
    public List<CreateQuizOptionRequest> Options { get; set; } = new();
}

public class CreateQuizOptionRequest
{
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public int Order { get; set; }
}

public class SubmitQuizRequest
{
    public List<SubmitAnswerRequest> Answers { get; set; } = new();
}

public class SubmitAnswerRequest
{
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string? TextAnswer { get; set; }
}
