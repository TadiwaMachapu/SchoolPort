namespace SchoolPortal.Data.Entities;

/// <summary>
/// A position in the seeded catalogue (e.g. HOD, FinanceManager, Auditor). Global
/// reference data — no SchoolId. The position-to-permission map is seeded in code and
/// is NOT user-editable; schools assign catalogue positions to users via
/// <see cref="UserPosition"/>.
/// </summary>
public class Position
{
    public Guid PositionId { get; set; }
    public string Key { get; set; } = null!;          // stable identifier, e.g. "HOD"
    public string DisplayName { get; set; } = null!;
    public string Category { get; set; } = null!;      // SMT, Teaching, Finance, Operational, External, System
    public ScopeType ScopeType { get; set; }           // None = unscoped (school-wide)

    public bool IsExternal { get; set; }               // Auditor, DistrictOfficial — read-only, time-limited
    public bool IsSystem { get; set; }                 // SystemSupport — consent-gated, time-limited, logged
    public bool RequiresTimeLimit { get; set; }        // EffectiveTo must be set on assignment
    public bool RequiresConsent { get; set; }          // ConsentRecord must be present on assignment
    public int? DefaultDurationHours { get; set; }     // default window for time-limited positions (e.g. 24)

    public virtual ICollection<PositionPermission> PositionPermissions { get; set; } = new List<PositionPermission>();
}
