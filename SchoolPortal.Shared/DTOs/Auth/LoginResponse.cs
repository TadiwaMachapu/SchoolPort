namespace SchoolPortal.Shared.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;
}

public class UserInfo
{
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;

    /// <summary>Layer-1 identity (Staff | Learner | Parent | External | System). Sprint 1.5.0:
    /// returned so the client can set the sp_identity cookie. Empty for pre-1.5.0 users whose
    /// Identity has not been backfilled.</summary>
    public string Identity { get; set; } = string.Empty;
}
