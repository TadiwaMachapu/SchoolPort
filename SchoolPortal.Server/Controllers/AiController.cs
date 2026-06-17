using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize(Roles="Admin,Teacher")]. All endpoints call Anthropic → ai.use
// (teaching + SMT + ITAdministrator). Spend is capped via School.Settings.AiMonthlyCostCapZar.
[RequirePermission(PermissionKeys.AiUse)]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AiController(IAiService aiService, SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _aiService = aiService;
        _context = context;
        _currentUser = currentUser;
    }

    [HttpPost("grade-suggestion/{submissionId}")]
    public async Task<IActionResult> SuggestGrade(Guid submissionId)
    {
        var submission = await _context.Submissions
            .Include(s => s.Assignment)
            .Include(s => s.Student).ThenInclude(st => st.User)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && s.SchoolId == _currentUser.SchoolId);

        if (submission == null)
            return NotFound("Submission not found");

        if (string.IsNullOrWhiteSpace(submission.Comments))
            return BadRequest("This submission has no text content to grade");

        var suggestion = await _aiService.SuggestGradeAsync(
            submission.Assignment.Title,
            submission.Assignment.Description,
            submission.Assignment.MaxMarks,
            submission.Comments
        );

        return Ok(new
        {
            suggestion.SuggestedScore,
            suggestion.Feedback,
            suggestion.Reasoning,
            suggestion.Confidence,
            MaxMarks = submission.Assignment.MaxMarks,
            StudentName = $"{submission.Student.User.FirstName} {submission.Student.User.LastName}"
        });
    }

    [HttpPost("generate-questions/{lessonId}")]
    public async Task<IActionResult> GenerateQuestions(Guid lessonId, [FromQuery] int count = 5)
    {
        var lesson = await _context.Lessons
            .Include(l => l.Module).ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(l => l.LessonId == lessonId && l.Module.Course.SchoolId == _currentUser.SchoolId);

        if (lesson == null)
            return NotFound("Lesson not found");

        if (string.IsNullOrWhiteSpace(lesson.Content))
            return BadRequest("Lesson has no text content");

        var questions = await _aiService.GenerateQuizQuestionsAsync(lesson.Content, count);

        return Ok(new { questions, lessonTitle = lesson.Title, count = questions.Count });
    }

    [HttpPost("plagiarism-check")]
    public async Task<IActionResult> CheckPlagiarism([FromBody] PlagiarismCheckRequest request)
    {
        var submissions = await _context.Submissions
            .Where(s => request.SubmissionIds.Contains(s.SubmissionId) && s.SchoolId == _currentUser.SchoolId)
            .Include(s => s.Student).ThenInclude(st => st.User)
            .ToListAsync();

        if (submissions.Count < 2)
            return BadRequest("Need at least 2 submissions to compare");

        var results = new List<object>();
        for (int i = 0; i < submissions.Count - 1; i++)
        {
            for (int j = i + 1; j < submissions.Count; j++)
            {
                var s1 = submissions[i];
                var s2 = submissions[j];

                if (string.IsNullOrWhiteSpace(s1.Comments) || string.IsNullOrWhiteSpace(s2.Comments))
                    continue;

                var similarity = await _aiService.CheckPlagiarismAsync(s1.Comments, s2.Comments);
                results.Add(new
                {
                    Student1 = $"{s1.Student.User.FirstName} {s1.Student.User.LastName}",
                    Student2 = $"{s2.Student.User.FirstName} {s2.Student.User.LastName}",
                    Similarity = Math.Round(similarity * 100, 1),
                    Flag = similarity > 0.7 ? "High" : similarity > 0.4 ? "Medium" : "Low"
                });
            }
        }

        return Ok(results);
    }
}

public record PlagiarismCheckRequest(List<Guid> SubmissionIds);
