namespace SchoolPortal.Shared.DTOs.Users;

public class MeResponse
{
    public UserProfile User { get; set; } = null!;
    public SchoolInfo School { get; set; } = null!;

    // Sprint 1.5.0 Step 8 — Layer-1 identity, the user's active positions (with scopes + effective
    // dates), and the resolved effective permission set for the current school context. The
    // permission set is authoritative (resolved server-side), for UX gating only.
    public string Identity { get; set; } = string.Empty;
    public List<MePosition> Positions { get; set; } = new();
    public List<string> Permissions { get; set; } = new();

    // Sprint 1.5.0 Step 8 (sidebar Matric Hub gate) — grade context the frontend needs but
    // can't derive from identity/positions alone. GradeLevel is the learner's own grade
    // (null for non-learners); HasGrade12Child is true when a parent has any linked child in
    // Grade 12. Both feed deriveNav's Matric Hub visibility rule.
    public int? GradeLevel { get; set; }
    public bool HasGrade12Child { get; set; }
}

public class MePosition
{
    public string Key { get; set; } = null!;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public List<MeScope> Scopes { get; set; } = new();
}

public class MeScope
{
    public int ScopeType { get; set; }     // matches the ScopeType enum (None=0, Subject=1, …)
    public Guid? ScopeRefId { get; set; }
    public string? ScopeValue { get; set; }
}

public class UserProfile
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class SchoolInfo
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
}
