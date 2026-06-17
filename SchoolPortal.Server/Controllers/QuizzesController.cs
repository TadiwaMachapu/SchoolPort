using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Quizzes;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Views → platform.access; authoring (create/publish/
// delete) → assessment.create; student attempts → assignments.submit/view_assigned (implicit);
// teacher attempt lists → marks.view_class.
public class QuizzesController : ControllerBase
{
    private readonly IQuizService _quizService;

    public QuizzesController(IQuizService quizService)
    {
        _quizService = quizService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.PlatformAccess)]
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
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuiz(Guid id, [FromQuery] bool teacherView = false)
    {
        var quiz = await _quizService.GetQuizAsync(id, teacherView);
        return Ok(quiz);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.AssessmentCreate)]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizRequest request)
    {
        var quiz = await _quizService.CreateQuizAsync(request);
        return CreatedAtAction(nameof(GetQuiz), new { id = quiz.QuizId }, quiz);
    }

    [HttpPut("{id}/publish")]
    [RequirePermission(PermissionKeys.AssessmentCreate)]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishQuiz(Guid id, [FromQuery] bool publish = true)
    {
        var quiz = await _quizService.PublishQuizAsync(id, publish);
        return Ok(quiz);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.AssessmentCreate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteQuiz(Guid id)
    {
        await _quizService.DeleteQuizAsync(id);
        return NoContent();
    }

    // Student: start attempt
    [HttpPost("{id}/attempts")]
    [RequirePermission(PermissionKeys.AssignmentsSubmit)]
    [ProducesResponseType(typeof(QuizAttemptDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> StartAttempt(Guid id)
    {
        var attempt = await _quizService.StartAttemptAsync(id);
        return StatusCode(201, attempt);
    }

    // Student: submit attempt
    [HttpPost("attempts/{attemptId}/submit")]
    [RequirePermission(PermissionKeys.AssignmentsSubmit)]
    [ProducesResponseType(typeof(QuizAttemptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitAttempt(Guid attemptId, [FromBody] SubmitQuizRequest request)
    {
        var result = await _quizService.SubmitAttemptAsync(attemptId, request);
        return Ok(result);
    }

    // Student: get my attempts
    [HttpGet("{id}/attempts/mine")]
    [RequirePermission(PermissionKeys.AssignmentsViewAssigned)]
    [ProducesResponseType(typeof(List<QuizAttemptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAttempts(Guid id)
    {
        var attempts = await _quizService.GetMyAttemptsAsync(id);
        return Ok(attempts);
    }

    // Teacher/Admin: get all attempts
    [HttpGet("{id}/attempts")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    [ProducesResponseType(typeof(List<QuizAttemptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAttempts(Guid id)
    {
        var attempts = await _quizService.GetAllAttemptsAsync(id);
        return Ok(attempts);
    }
}
