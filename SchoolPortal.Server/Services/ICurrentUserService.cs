using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;

namespace SchoolPortal.Server.Services;

public interface ICurrentUserService
{
    Guid SchoolId { get; }
    Guid UserId { get; }
    bool IsAuthenticated { get; }

    /// <summary>Layer-1 identity: Staff | Learner | Parent | External | System.
    /// Falls back to a mapping from the legacy role claim for pre-1.5.0 tokens.</summary>
    string Identity { get; }

    /// <summary>True if the per-request effective permission set (JWT-resolved, expiry
    /// enforced) contains the key. Routine path only — sensitive operations re-resolve
    /// from the database via PermissionResolver (Step 4 handler wires this).</summary>
    bool HasPermission(string permissionKey);

    /// <summary>True if the user holds an active, in-window position with this key.</summary>
    bool HasPosition(string positionKey);

    /// <summary>True if any active position carries a scope entry matching (type, scopeId).
    /// Claim-level check; deep relationship checks (e.g. IsMyChild) live in Step 4/7.</summary>
    bool IsInScope(ScopeType type, Guid scopeId);

    IReadOnlySet<string> GetEffectivePermissions();

    /// <summary>The user's active, in-window positions (with scopes + effective dates) from the
    /// per-request resolved set — same source as GetEffectivePermissions(), so no re-resolution.</summary>
    IReadOnlyList<PositionClaim> GetActivePositions();
}
