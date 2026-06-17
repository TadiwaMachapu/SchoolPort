using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] (class) + [Authorize(Roles="Admin,Teacher")] (writes). Views are
// any-authenticated (platform.access); authoring assessments requires assessment.create.
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(PaginatedResult<AssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] Guid? classId,
        [FromQuery] DateTime? dueFrom,
        [FromQuery] DateTime? dueTo,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _assignmentService.GetAssignmentsAsync(classId, dueFrom, dueTo, status, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignment(Guid id)
    {
        var assignment = await _assignmentService.GetAssignmentByIdAsync(id);
        return Ok(assignment);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.AssessmentCreate)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentRequest request)
    {
        var assignment = await _assignmentService.CreateAssignmentAsync(request);
        return CreatedAtAction(nameof(GetAssignment), new { id = assignment.AssignmentId }, assignment);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.AssessmentCreate)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAssignment(Guid id, [FromBody] UpdateAssignmentRequest request)
    {
        var assignment = await _assignmentService.UpdateAssignmentAsync(id, request);
        return Ok(assignment);
    }
}
