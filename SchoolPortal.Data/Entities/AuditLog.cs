namespace SchoolPortal.Data.Entities;

/// <summary>
/// Append-only audit record. Sprint 1.5.0: the key type was corrected from <c>long</c> to
/// <c>Guid</c> to match the <c>gen_random_uuid()</c> default (the previous mismatch meant
/// inserts failed). New columns capture which position authorised an action and which
/// permission was exercised, per the Sprint 1.5.0 audit requirement.
/// </summary>
public class AuditLog
{
    public Guid AuditLogId { get; set; }
    public Guid? SchoolId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? AuthorizingPositionKey { get; set; }  // which position authorised this access
    public string? PermissionUsed { get; set; }          // which permission was exercised
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
}
