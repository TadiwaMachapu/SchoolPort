using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Submissions;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + role overrides. Learner submit/view-own → assignments.submit /
// assignments.view_assigned (identity-implicit); teacher submission lists → marks.view_class.
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
    [RequirePermission(PermissionKeys.AssignmentsSubmit)]
    [RequestSizeLimit(52_428_800)] // 50 MB
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSubmission(
        [FromForm] Guid assignmentId,
        [FromForm] string? comments,
        IFormFile? file)
    {
        // Step 11 QA M-1: validate the assignment belongs to the caller's school BEFORE uploading,
        // so a foreign/invalid id (which 404s) can't leave an orphan file in the storage bucket.
        await _submissionService.EnsureAssignmentInSchoolAsync(assignmentId);

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
    [RequirePermission(PermissionKeys.MarksViewClass)]
    [ProducesResponseType(typeof(List<SubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissionsByAssignment(Guid assignmentId)
    {
        var submissions = await _submissionService.GetSubmissionsByAssignmentAsync(assignmentId);
        return Ok(submissions);
    }

    /// <summary>Returns ungraded submissions for the current teacher (or all for Admin).</summary>
    [HttpGet("pending")]
    [RequirePermission(PermissionKeys.MarksViewClass)]
    [ProducesResponseType(typeof(List<PendingSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending([FromQuery] int limit = 20)
    {
        var result = await _submissionService.GetPendingSubmissionsAsync(limit);
        return Ok(result);
    }

    [HttpGet("by-assignment/{assignmentId}/mine")]
    [RequirePermission(PermissionKeys.AssignmentsViewAssigned)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubmission(Guid assignmentId)
    {
        var submission = await _submissionService.GetMySubmissionAsync(assignmentId);
        if (submission == null) return NotFound();
        return Ok(submission);
    }
}
