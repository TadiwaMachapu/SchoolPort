namespace SchoolPortal.Data.Entities;

/// <summary>
/// CAPS moderation workflow state for an assessment task. Stored as a string column
/// (same convention as <see cref="TaskType"/>).
/// </summary>
public enum ApprovalStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
}

/// <summary>
/// Sprint 1.5.2.5 — one submit→review cycle of a task's marks (per-task grain). A rejection
/// followed by resubmission creates a NEW record, preserving moderation history for CAPS.
/// A partial unique index allows at most one open (Draft/Submitted) record per assignment.
/// Week 3 (HOD moderation UI) consumes these; Weeks 1-2 only create Draft records.
/// </summary>
public class ApprovalRecord
{
    public Guid ApprovalRecordId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual Assignment Assignment { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User SubmittedByUser { get; set; } = null!;
    public virtual User? ReviewedByUser { get; set; }
}
