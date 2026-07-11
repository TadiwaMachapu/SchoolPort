namespace SchoolPortal.Shared.DTOs.Grades;

// Sprint 1.5.2.5 — Marks Capture & Approval (Weeks 1-2).

/// <summary>One assessment task in a class-subject's task list.</summary>
public class TaskSummaryDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public int? TermNumber { get; set; }
    public decimal MaxMarks { get; set; }
    public bool HasRubric { get; set; }
    public decimal? SbaWeight { get; set; }
    public DateTime DueAt { get; set; }
    /// <summary>Learners with a captured mark (scored or absent) / class size.</summary>
    public int CapturedCount { get; set; }
    public int ClassSize { get; set; }
    /// <summary>Open approval status: Draft | Submitted | Approved | Rejected | null (never captured).</summary>
    public string? ApprovalStatus { get; set; }
}

public class CriteriaDto
{
    public Guid CriteriaId { get; set; }
    public string Name { get; set; } = null!;
    public decimal MaxMark { get; set; }
    public int DisplayOrder { get; set; }
}

public class LearnerCriteriaScoreDto
{
    public Guid CriteriaId { get; set; }
    /// <summary>null = not yet entered (pending); 0 = captured as zero.</summary>
    public decimal? Score { get; set; }
}

/// <summary>One learner row in the capture grid.</summary>
public class LearnerMarkDto
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = null!;
    public string Surname { get; set; } = null!;
    public string StudentNumber { get; set; } = null!;
    public decimal? Score { get; set; }
    public bool IsAbsent { get; set; }
    public List<LearnerCriteriaScoreDto> CriteriaScores { get; set; } = new();
}

/// <summary>Everything the capture grid needs for one task: definition + all learners.</summary>
public class TaskMarksDto
{
    public Guid AssignmentId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public decimal MaxMarks { get; set; }
    public bool HasRubric { get; set; }
    public int? TermNumber { get; set; }
    public decimal? SbaWeight { get; set; }
    public string? ApprovalStatus { get; set; }
    public List<CriteriaDto> Criteria { get; set; } = new();
    public List<LearnerMarkDto> Learners { get; set; } = new();
}

public class BulkCaptureEntry
{
    public Guid StudentId { get; set; }
    /// <summary>Simple tasks: the mark. Rubric tasks: ignored — the server sums criteria.
    /// Must be null when IsAbsent (absent ≠ zero).</summary>
    public decimal? Score { get; set; }
    public bool IsAbsent { get; set; }
    public List<LearnerCriteriaScoreDto>? CriteriaScores { get; set; }
}

public class BulkCaptureRequest
{
    public Guid TaskId { get; set; }
    public Guid ClassSubjectId { get; set; }
    /// <summary>Optional reason recorded on the audit rows for changed marks.</summary>
    public string? ChangeReason { get; set; }
    public List<BulkCaptureEntry> Entries { get; set; } = new();
}

public class BulkCaptureResultDto
{
    public int Saved { get; set; }
    public int Changed { get; set; }
    /// <summary>Non-blocking review flags, e.g. unusually high/low class average.</summary>
    public List<string> Warnings { get; set; } = new();
}

public class TaskCriteriaInput
{
    public string Name { get; set; } = null!;
    public decimal MaxMark { get; set; }
}

public class CreateTaskRequest
{
    public Guid ClassSubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = "Test";
    public int? TermNumber { get; set; }
    /// <summary>Total marks for simple tasks; for rubric tasks the server derives it from criteria.</summary>
    public decimal MaxMarks { get; set; }
    public bool HasRubric { get; set; }
    public decimal? SbaWeight { get; set; }
    public DateTime? DueAt { get; set; }
    public List<TaskCriteriaInput>? Criteria { get; set; }
}

public class UpdateTaskRequest
{
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = "Test";
    public int? TermNumber { get; set; }
    public decimal MaxMarks { get; set; }
    public decimal? SbaWeight { get; set; }
    public DateTime? DueAt { get; set; }
}
