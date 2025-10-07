namespace SchoolPortal.Shared.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public string Role { get; set; } = null!;
    public int UserId { get; set; }
    public int SchoolId { get; set; }
}
