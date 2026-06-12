namespace SchoolPortal.Data.Entities;

/// <summary>
/// An atomic permission verb (e.g. "marks.capture", "finance.refund"). Global reference
/// data — no SchoolId. Seeded in code. Permissions attach to positions via
/// <see cref="PositionPermission"/>, never directly to users.
/// </summary>
public class Permission
{
    public Guid PermissionId { get; set; }
    public string Key { get; set; } = null!;       // e.g. "marks.capture"
    public string Category { get; set; } = null!;   // Academic, Pathways, Discipline, Finance, Communication, System
    public string? Description { get; set; }

    public virtual ICollection<PositionPermission> PositionPermissions { get; set; } = new List<PositionPermission>();
}
