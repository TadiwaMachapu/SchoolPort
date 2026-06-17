using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Quizzes;

namespace SchoolPortal.Server.Services;

public interface IQuizService
{
    Task<PaginatedResult<QuizDto>> GetQuizzesAsync(int page, int pageSize, bool teacherView = false);
    Task<QuizDto> GetQuizAsync(Guid quizId, bool teacherView = false);
    Task<QuizDto> CreateQuizAsync(CreateQuizRequest request);
    Task<QuizDto> PublishQuizAsync(Guid quizId, bool publish);
    Task DeleteQuizAsync(Guid quizId);
    Task<QuizAttemptDto> StartAttemptAsync(Guid quizId);
    Task<QuizAttemptDto> SubmitAttemptAsync(Guid attemptId, SubmitQuizRequest request);
    Task<List<QuizAttemptDto>> GetMyAttemptsAsync(Guid quizId);
    Task<List<QuizAttemptDto>> GetAllAttemptsAsync(Guid quizId);
}

public class QuizService : IQuizService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public QuizService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<QuizDto>> GetQuizzesAsync(int page, int pageSize, bool teacherView = false)
    {
        var query = _context.Quizzes
            .AsNoTracking()
            .Where(q => q.SchoolId == _currentUser.SchoolId);

        if (!teacherView) query = query.Where(q => q.IsPublished);

        var total = await query.CountAsync();
        var items = await query
            .Include(q => q.CreatedByUser)
            .Include(q => q.Questions)
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuizDto
            {
                QuizId = q.QuizId,
                Title = q.Title,
                Description = q.Description,
                TimeLimitMinutes = q.TimeLimitMinutes,
                MaxAttempts = q.MaxAttempts,
                ShuffleQuestions = q.ShuffleQuestions,
                ShowResultsImmediately = q.ShowResultsImmediately,
                IsPublished = q.IsPublished,
                CreatedByName = $"{q.CreatedByUser.FirstName} {q.CreatedByUser.LastName}",
                CreatedAt = q.CreatedAt,
                QuestionCount = q.Questions.Count
            })
            .ToListAsync();

        return new PaginatedResult<QuizDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<QuizDto> GetQuizAsync(Guid quizId, bool teacherView = false)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.QuizId == quizId && q.SchoolId == _currentUser.SchoolId)
            .Include(q => q.CreatedByUser)
            .Include(q => q.Questions.OrderBy(x => x.Order))
                .ThenInclude(q => q.Options.OrderBy(o => o.Order))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Quiz not found");

        var dto = new QuizDto
        {
            QuizId = quiz.QuizId,
            Title = quiz.Title,
            Description = quiz.Description,
            TimeLimitMinutes = quiz.TimeLimitMinutes,
            MaxAttempts = quiz.MaxAttempts,
            ShuffleQuestions = quiz.ShuffleQuestions,
            ShowResultsImmediately = quiz.ShowResultsImmediately,
            IsPublished = quiz.IsPublished,
            CreatedByName = $"{quiz.CreatedByUser.FirstName} {quiz.CreatedByUser.LastName}",
            CreatedAt = quiz.CreatedAt,
            QuestionCount = quiz.Questions.Count,
            Questions = quiz.Questions.Select(q => new QuizQuestionDto
            {
                QuestionId = q.QuestionId,
                Text = q.Text,
                Type = q.Type,
                Order = q.Order,
                Marks = q.Marks,
                Explanation = teacherView ? q.Explanation : null,
                Options = q.Options.Select(o => new QuizOptionDto
                {
                    OptionId = o.OptionId,
                    Text = o.Text,
                    IsCorrect = teacherView && o.IsCorrect,
                    Order = o.Order
                }).ToList()
            }).ToList()
        };

        if (quiz.ShuffleQuestions && !teacherView)
            dto.Questions = dto.Questions.OrderBy(_ => Guid.NewGuid()).ToList();

        return dto;
    }

    public async Task<QuizDto> CreateQuizAsync(CreateQuizRequest request)
    {
        // Step 10 (Teaching cluster, H1-class): a ClassSubjectId supplied in the body must belong to
        // the caller's school, else a quiz would link to another school's class-subject (the FK
        // resolves across tenants). Nullable — only validated when a value is supplied.
        if (request.ClassSubjectId.HasValue &&
            !await _context.ClassSubjects.AnyAsync(cs => cs.ClassSubjectId == request.ClassSubjectId.Value && cs.SchoolId == _currentUser.SchoolId))
            throw new KeyNotFoundException("ClassSubject not found");

        var quiz = new Quiz
        {
            SchoolId = _currentUser.SchoolId,
            ClassSubjectId = request.ClassSubjectId,
            Title = request.Title,
            Description = request.Description,
            TimeLimitMinutes = request.TimeLimitMinutes,
            MaxAttempts = request.MaxAttempts,
            ShuffleQuestions = request.ShuffleQuestions,
            ShowResultsImmediately = request.ShowResultsImmediately,
            IsPublished = false,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            Questions = request.Questions.Select((q, i) => new QuizQuestion
            {
                Text = q.Text,
                Type = q.Type,
                Order = q.Order > 0 ? q.Order : i,
                Marks = q.Marks,
                Explanation = q.Explanation,
                Options = q.Options.Select((o, j) => new QuizOption
                {
                    Text = o.Text,
                    IsCorrect = o.IsCorrect,
                    Order = o.Order > 0 ? o.Order : j
                }).ToList()
            }).ToList()
        };

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync();
        return await GetQuizAsync(quiz.QuizId, teacherView: true);
    }

    public async Task<QuizDto> PublishQuizAsync(Guid quizId, bool publish)
    {
        var quiz = await _context.Quizzes
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Quiz not found");

        if (publish && !await _context.QuizQuestions.AnyAsync(q => q.QuizId == quizId))
            throw new InvalidOperationException("Cannot publish a quiz with no questions");

        quiz.IsPublished = publish;
        quiz.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await GetQuizAsync(quizId, teacherView: true);
    }

    public async Task DeleteQuizAsync(Guid quizId)
    {
        var quiz = await _context.Quizzes
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Quiz not found");

        _context.Quizzes.Remove(quiz);
        await _context.SaveChangesAsync();
    }

    public async Task<QuizAttemptDto> StartAttemptAsync(Guid quizId)
    {
        var quiz = await _context.Quizzes
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.SchoolId == _currentUser.SchoolId && q.IsPublished)
            ?? throw new KeyNotFoundException("Quiz not found or not published");

        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == Guid.Empty) throw new InvalidOperationException("Student not found");

        var attemptCount = await _context.QuizAttempts
            .CountAsync(a => a.QuizId == quizId && a.StudentId == studentId);

        if (attemptCount >= quiz.MaxAttempts)
            throw new InvalidOperationException($"Maximum attempts ({quiz.MaxAttempts}) reached");

        var attempt = new QuizAttempt
        {
            QuizId = quizId,
            StudentId = studentId,
            SchoolId = _currentUser.SchoolId,
            StartedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync();

        return MapAttempt(attempt, quiz.Title);
    }

    public async Task<QuizAttemptDto> SubmitAttemptAsync(Guid attemptId, SubmitQuizRequest request)
    {
        // Step 10 (Teaching cluster, IDOR): the attempt must belong to the CALLER (own student) and
        // their school — previously it loaded by attemptId alone, so a learner could submit/score
        // another student's (or another school's) in-progress attempt by guessing its id.
        var myStudentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId && s.SchoolId == _currentUser.SchoolId)
            .Select(s => s.StudentId).FirstOrDefaultAsync();

        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.AttemptId == attemptId
                && a.SchoolId == _currentUser.SchoolId
                && a.StudentId == myStudentId
                && !a.IsCompleted)
            ?? throw new KeyNotFoundException("Attempt not found or already submitted");

        decimal totalScore = 0;
        decimal maxScore = attempt.Quiz.Questions.Sum(q => q.Marks);

        var answers = new List<QuizAnswer>();
        foreach (var ans in request.Answers)
        {
            var question = attempt.Quiz.Questions.FirstOrDefault(q => q.QuestionId == ans.QuestionId);
            if (question == null) continue;

            bool? isCorrect = null;
            decimal marksAwarded = 0;

            if (question.Type is "MultipleChoice" or "TrueFalse" && ans.SelectedOptionId.HasValue)
            {
                var option = question.Options.FirstOrDefault(o => o.OptionId == ans.SelectedOptionId);
                isCorrect = option?.IsCorrect ?? false;
                marksAwarded = isCorrect == true ? question.Marks : 0;
            }

            totalScore += marksAwarded;
            answers.Add(new QuizAnswer
            {
                AttemptId = attemptId,
                QuestionId = ans.QuestionId,
                SelectedOptionId = ans.SelectedOptionId,
                TextAnswer = ans.TextAnswer,
                IsCorrect = isCorrect,
                MarksAwarded = marksAwarded
            });
        }

        _context.QuizAnswers.AddRange(answers);

        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.Score = totalScore;
        attempt.MaxScore = maxScore;
        attempt.IsCompleted = true;

        await _context.SaveChangesAsync();

        var dto = MapAttempt(attempt, attempt.Quiz.Title);
        dto.Answers = answers.Select(a => new QuizAnswerDto
        {
            QuestionId = a.QuestionId,
            SelectedOptionId = a.SelectedOptionId,
            TextAnswer = a.TextAnswer,
            IsCorrect = a.IsCorrect,
            MarksAwarded = a.MarksAwarded
        }).ToList();

        return dto;
    }

    public async Task<List<QuizAttemptDto>> GetMyAttemptsAsync(Guid quizId)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        return await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizId && a.StudentId == studentId)
            .Include(a => a.Quiz)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new QuizAttemptDto
            {
                AttemptId = a.AttemptId,
                QuizId = a.QuizId,
                QuizTitle = a.Quiz.Title,
                StartedAt = a.StartedAt,
                SubmittedAt = a.SubmittedAt,
                Score = a.Score,
                MaxScore = a.MaxScore,
                IsCompleted = a.IsCompleted,
                Percentage = a.MaxScore > 0 ? Math.Round((decimal)(a.Score ?? 0) / a.MaxScore.Value * 100, 1) : null
            })
            .ToListAsync();
    }

    public async Task<List<QuizAttemptDto>> GetAllAttemptsAsync(Guid quizId)
    {
        return await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizId && a.Quiz.SchoolId == _currentUser.SchoolId)
            .Include(a => a.Quiz)
            .Include(a => a.Student).ThenInclude(s => s.User)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new QuizAttemptDto
            {
                AttemptId = a.AttemptId,
                QuizId = a.QuizId,
                QuizTitle = a.Quiz.Title,
                StartedAt = a.StartedAt,
                SubmittedAt = a.SubmittedAt,
                Score = a.Score,
                MaxScore = a.MaxScore,
                IsCompleted = a.IsCompleted,
                Percentage = a.MaxScore > 0 ? Math.Round((decimal)(a.Score ?? 0) / a.MaxScore.Value * 100, 1) : null
            })
            .ToListAsync();
    }

    private static QuizAttemptDto MapAttempt(QuizAttempt a, string quizTitle) => new()
    {
        AttemptId = a.AttemptId,
        QuizId = a.QuizId,
        QuizTitle = quizTitle,
        StartedAt = a.StartedAt,
        SubmittedAt = a.SubmittedAt,
        Score = a.Score,
        MaxScore = a.MaxScore,
        IsCompleted = a.IsCompleted,
        Percentage = a.MaxScore > 0 ? Math.Round((decimal)(a.Score ?? 0) / a.MaxScore.Value * 100, 1) : null
    };
}
