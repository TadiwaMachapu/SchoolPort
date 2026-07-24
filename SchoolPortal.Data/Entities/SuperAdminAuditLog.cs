namespace SchoolPortal.Data.Entities;

/// <summary>
/// Append-only audit trail of platform-level SuperAdmin mutations (school create, feature-flag
/// changes, activate/deactivate). SuperAdmin sits OUTSIDE the school identity model, so the actor
/// is a SuperAdmin (FK), not a User. Mirrors <see cref="MarkCaptureAuditLog"/>: typed columns, real
/// FKs with Restrict (audit must survive), written explicitly in the service inside the mutation's
/// SaveChanges so the log row and its effect are one transaction. PreviousValue/NewValue hold a
/// compact JSON diff of ONLY what changed — never a full blob the reader has to diff by hand.
/// </summary>
public class SuperAdminAuditLog
{
    public Guid AuditId { get; set; }
    public Guid SuperAdminId { get; set; }              // actor — FK to super_admins
    public string ActionType { get; set; } = null!;     // SuperAdminAuditActions.* (string col, not pg enum)
    public Guid? TargetSchoolId { get; set; }           // nullable: null only for no-single-target actions
    public string? PreviousValue { get; set; }          // JSON diff; null for creates (nothing before)
    public string? NewValue { get; set; }               // JSON diff
    public string? Reason { get; set; }                 // optional free-text (e.g. why deactivated)
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual SuperAdmin SuperAdmin { get; set; } = null!;
    public virtual School? TargetSchool { get; set; }
}

/// <summary>
/// The canonical set of SuperAdmin audit action types. String column (not a pg enum) so new
/// actions need no ALTER TYPE — same convention as <c>Assignment.TaskType</c>.
/// </summary>
public static class SuperAdminAuditActions
{
    public const string SchoolCreated         = "SchoolCreated";
    public const string SchoolFeaturesUpdated = "SchoolFeaturesUpdated";
    public const string SchoolStatusChanged   = "SchoolStatusChanged";
}
