using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Submissions;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;
    private readonly IStorageService _storageService;
    private readonly ICurrentUserService _currentUser;

    public SubmissionsController(
        ISubmissionService submissionService,
        IStorageService storageService,
        ICurrentUserService currentUser)
    {
        _submissionService = submissionService;
        _storageService = storageService;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubmission(
        [FromForm] Guid assignmentId,
        [FromForm] string? comments,
        IFormFile? file)
    {
        string? fileUrl = null;
        string? fileName = null;

        if (file != null && file.Length > 0)
        {
            (fileUrl, fileName) = await _storageService.UploadSubmissionFileAsync(
                _currentUser.SchoolId, assignmentId, _currentUser.UserId, file);
        }

        var submissionId = await _submissionService.CreateSubmissionAsync(assignmentId, comments, fileUrl, fileName);
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

    /// <summary>Returns ungraded submissions for the current teacher (or all for Admin).</summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Admin,Teacher")]
    [ProducesResponseType(typeof(List<PendingSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending([FromQuery] int limit = 20)
    {
        var result = await _submissionService.GetPendingSubmissionsAsync(limit);
        return Ok(result);
    }

    [HttpGet("by-assignment/{assignmentId}/mine")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubmission(Guid assignmentId)
    {
        var submission = await _submissionService.GetMySubmissionAsync(assignmentId);
        if (submission == null) return NotFound();
        return Ok(submission);
    }
}
