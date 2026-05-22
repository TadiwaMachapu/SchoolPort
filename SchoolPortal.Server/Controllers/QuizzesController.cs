using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Quizzes;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuizzesController : ControllerBase
{
    private readonly IQuizService _quizService;

    public QuizzesController(IQuizService quizService)
    {
        _quizService = quizService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<QuizDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuizzes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool teacherView = false)
    {
        var result = await _quizService.GetQuizzesAsync(page, pageSize, teacherView);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuiz(Guid id, [FromQuery] bool teacherView = false)
    {
        var quiz = await _quizService.GetQuizAsync(id, teacherView);
        return Ok(quiz);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizRequest request)
    {
        var quiz = await _quizService.CreateQuizAsync(request);
        return CreatedAtAction(nameof(GetQuiz), new { id = quiz.QuizId }, quiz);
    }

    [HttpPut("{id}/publish")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishQuiz(Guid id, [FromQuery] bool publish = true)
    {
        var quiz = await _quizService.PublishQuizAsync(id, publish);
        return Ok(quiz);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteQuiz(Guid id)
    {
        await _quizService.DeleteQuizAsync(id);
        return NoContent();
    }

    // Student: start attempt
    [HttpPost("{id}/attempts")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(QuizAttemptDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> StartAttempt(Guid id)
    {
        var attempt = await _quizService.StartAttemptAsync(id);
        return StatusCode(201, attempt);
    }

    // Student: submit attempt
    [HttpPost("attempts/{attemptId}/submit")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(QuizAttemptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitAttempt(Guid attemptId, [FromBody] SubmitQuizRequest request)
    {
        var result = await _quizService.SubmitAttemptAsync(attemptId, request);
        return Ok(result);
    }

    // Student: get my attempts
    [HttpGet("{id}/attempts/mine")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(List<QuizAttemptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAttempts(Guid id)
    {
        var attempts = await _quizService.GetMyAttemptsAsync(id);
        return Ok(attempts);
    }

    // Teacher/Admin: get all attempts
    [HttpGet("{id}/attempts")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(typeof(List<QuizAttemptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAttempts(Guid id)
    {
        var attempts = await _quizService.GetAllAttemptsAsync(id);
        return Ok(attempts);
    }
}
