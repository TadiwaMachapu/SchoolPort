namespace SchoolPortal.Shared.DTOs.Positions;

// Sprint 1.5.0 Step 9 — position management (assign / edit / revoke / who-holds-what).

public class PositionCatalogueItemDto
{
    public string Key { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int ScopeType { get; set; }              // matches ScopeType enum
    public string ScopeTypeName { get; set; } = null!;
    public bool IsExternal { get; set; }
    public bool IsSystem { get; set; }
    public bool RequiresTimeLimit { get; set; }
    public bool RequiresConsent { get; set; }
    public int? DefaultDurationHours { get; set; }
    public bool InPreset { get; set; }              // in the school's size-preset default set (advisory)
}

public class ScopeDto
{
    public int ScopeType { get; set; }
    public Guid? ScopeRefId { get; set; }
    public string? ScopeValue { get; set; }
    public string Label { get; set; } = null!;      // resolved for display (subject name / "Grade 10" / "FET")
}

public class PositionAssignmentDto
{
    public Guid UserPositionId { get; set; }
    public string PositionKey { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int ScopeType { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public List<ScopeDto> Scopes { get; set; } = new();
}

public class UserPositionsDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Identity { get; set; } = null!;
    public List<PositionAssignmentDto> Assignments { get; set; } = new();
}

public class PositionHolderDto
{
    public Guid UserPositionId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public List<ScopeDto> Scopes { get; set; } = new();
}

public class PositionOverviewItemDto
{
    public string PositionKey { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public List<PositionHolderDto> Holders { get; set; } = new();
}

public class ScopeInput
{
    public int? ScopeType { get; set; }     // optional — defaults to the position's own ScopeType
    public Guid? ScopeRefId { get; set; }   // Subject id (Subject scope)
    public string? ScopeValue { get; set; } // Grade ("10") / Phase ("FET")
}

public class AssignPositionRequest
{
    public Guid UserId { get; set; }
    public string PositionKey { get; set; } = null!;
    public DateTime? EffectiveFrom { get; set; }   // defaults to now
    public DateTime? EffectiveTo { get; set; }     // required for External/System / time-limited
    public Guid? ConsentRecordId { get; set; }     // required for System positions
    public List<ScopeInput> Scopes { get; set; } = new();
}

public class UpdateAssignmentRequest
{
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool? IsActive { get; set; }
    public List<ScopeInput>? Scopes { get; set; }  // when present, replaces the assignment's scopes
}
