namespace SchoolPortal.Data.Entities;

/// <summary>
/// A single scope entry bounding a <see cref="UserPosition"/>. An appointment may carry
/// zero scopes (unscoped position) or several (e.g. a SubjectTeacher across multiple
/// classes). Entity-backed scopes (Subject, Class, Activity) populate
/// <see cref="ScopeRefId"/>; value scopes (Phase, Grade, Advisory) populate
/// <see cref="ScopeValue"/>. <see cref="ScopeRefId"/> is intentionally polymorphic
/// (it points to different tables by <see cref="ScopeType"/>), so it is indexed but not
/// a database foreign key.
/// </summary>
public class UserPositionScope
{
    public Guid UserPositionScopeId { get; set; }
    public Guid UserPositionId { get; set; }
    public ScopeType ScopeType { get; set; }
    public Guid? ScopeRefId { get; set; }    // Subject/Class/Activity id (polymorphic — no FK)
    public string? ScopeValue { get; set; }   // Phase ("FET"), Grade ("10"), Advisory group code

    public virtual UserPosition UserPosition { get; set; } = null!;
}
