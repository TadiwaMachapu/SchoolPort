using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Services;

public interface IMatricHubService
{
    Task<List<string>> GetSubjectsAsync();
    Task<List<PastPaperDto>> GetPastPapersAsync(string? subject);
    Task<List<MatricQuizQuestionDto>> GetQuizQuestionsAsync(string subject, int count);
}

public record PastPaperDto(
    Guid MatricPastPaperId,
    string Subject,
    int Year,
    int PaperNumber,
    string Language,
    string Url,
    bool HasMemo,
    string? MemoUrl,
    string? Notes
);

public record MatricQuizQuestionDto(
    Guid MatricQuizQuestionId,
    string Subject,
    string Difficulty,
    string QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD
);

public class MatricHubService : IMatricHubService
{
    private readonly SchoolPortalDbContext _context;

    public MatricHubService(SchoolPortalDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetSubjectsAsync()
    {
        var fromPapers = await _context.MatricPastPapers
            .AsNoTracking()
            .Select(p => p.Subject)
            .Distinct()
            .ToListAsync();

        var fromQuizzes = await _context.MatricQuizQuestions
            .AsNoTracking()
            .Select(q => q.Subject)
            .Distinct()
            .ToListAsync();

        return fromPapers.Union(fromQuizzes).OrderBy(s => s).ToList();
    }

    public async Task<List<PastPaperDto>> GetPastPapersAsync(string? subject)
    {
        var query = _context.MatricPastPapers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(p => p.Subject == subject);

        return await query
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.Subject)
            .ThenBy(p => p.PaperNumber)
            .Select(p => new PastPaperDto(
                p.MatricPastPaperId,
                p.Subject,
                p.Year,
                p.PaperNumber,
                p.Language,
                p.Url,
                p.HasMemo,
                p.MemoUrl,
                p.Notes))
            .ToListAsync();
    }

    public async Task<List<MatricQuizQuestionDto>> GetQuizQuestionsAsync(string subject, int count)
    {
        // Fetch all for subject then randomise in memory (small dataset)
        var all = await _context.MatricQuizQuestions
            .AsNoTracking()
            .Where(q => q.Subject == subject)
            .Select(q => new MatricQuizQuestionDto(
                q.MatricQuizQuestionId,
                q.Subject,
                q.Difficulty,
                q.QuestionText,
                q.OptionA,
                q.OptionB,
                q.OptionC,
                q.OptionD))
            .ToListAsync();

        return all.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
    }
}
