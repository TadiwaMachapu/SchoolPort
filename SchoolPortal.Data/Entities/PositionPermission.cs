namespace SchoolPortal.Data.Entities;

/// <summary>
/// Seeded join between a <see cref="Position"/> and a <see cref="Permission"/>. This is
/// the in-code position-to-permission map. Global reference data — no SchoolId.
/// </summary>
public class PositionPermission
{
    public Guid PositionPermissionId { get; set; }
    public Guid PositionId { get; set; }
    public Guid PermissionId { get; set; }

    public virtual Position Position { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
