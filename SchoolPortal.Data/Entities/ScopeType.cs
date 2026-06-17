namespace SchoolPortal.Data.Entities;

/// <summary>
/// The kind of scope a position assignment is bounded to. <see cref="None"/> means
/// the position is unscoped (school-wide). Entity-backed scopes (Subject, Class,
/// Activity) store their id in <c>UserPositionScope.ScopeRefId</c>; value scopes
/// (Phase, Grade, Advisory) store a string in <c>UserPositionScope.ScopeValue</c>.
/// </summary>
public enum ScopeType
{
    None = 0,
    Subject = 1,
    Phase = 2,
    Grade = 3,
    Class = 4,
    Activity = 5,
    Advisory = 6
}
