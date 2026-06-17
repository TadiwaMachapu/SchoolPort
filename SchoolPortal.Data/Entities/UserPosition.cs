namespace SchoolPortal.Data.Entities;

/// <summary>
/// A position appointment for a user within a school (tenant-scoped). A user holds zero
/// or more. Grants nothing outside [EffectiveFrom, EffectiveTo] or when IsActive is
/// false. External/System positions MUST carry a non-null EffectiveTo; System positions
/// MUST reference a <see cref="ConsentRecord"/>. Scopes (which subject/grade/class/etc.)
/// live in <see cref="UserPositionScope"/>.
/// </summary>
public class UserPosition
{
    public Guid UserPositionId { get; set; }
    public Guid SchoolId { get; set; }                 // tenant boundary — a position in school A grants nothing in B
    public Guid UserId { get; set; }
    public Guid PositionId { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }          // null = open-ended (only allowed for non-external/non-system)

    public Guid? GrantedByUserId { get; set; }          // who assigned it (audit)
    public Guid? ConsentRecordId { get; set; }          // required for System positions

    public bool IsActive { get; set; } = true;          // soft-revoke without losing the audit trail
    public DateTime CreatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    public virtual School School { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Position Position { get; set; } = null!;
    public virtual User? GrantedByUser { get; set; }
    public virtual ConsentRecord? ConsentRecord { get; set; }
    public virtual ICollection<UserPositionScope> Scopes { get; set; } = new List<UserPositionScope>();
}
