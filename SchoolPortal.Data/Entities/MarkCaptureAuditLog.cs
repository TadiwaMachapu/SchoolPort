namespace SchoolPortal.Data.Entities;

/// <summary>
/// Sprint 1.5.2.5 — append-only audit trail of mark CHANGES (first-time entries are not
/// logged). The Grade FK is Restrict, not Cascade: audit history must survive, so a
/// hard-delete of audited marks fails loudly by design.
/// </summary>
public class MarkCaptureAuditLog
{
    public Guid AuditId { get; set; }
    public Guid GradeId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public decimal? PreviousScore { get; set; }
    public decimal? NewScore { get; set; }
    public bool PreviousIsAbsent { get; set; }
    public bool NewIsAbsent { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime ChangedAt { get; set; }

    // Navigation properties
    public virtual Grade Grade { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User ChangedByUser { get; set; } = null!;
}
