using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Submissions;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubmission(
        [FromForm] Guid assignmentId,
        [FromForm] string? comments,
        [FromForm] IFormFile? file)
    {
        // For MVP, file upload handling is simplified
        // In production, upload to blob storage
        var submissionId = await _submissionService.CreateSubmissionAsync(assignmentId, comments);
        return CreatedAtAction(nameof(CreateSubmission), new { }, new { submissionId });
    }

    [HttpGet("by-assignment/{assignmentId}")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(typeof(List<SubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissionsByAssignment(Guid assignmentId)
    {
        var submissions = await _submissionService.GetSubmissionsByAssignmentAsync(assignmentId);
        return Ok(submissions);
    }
}
