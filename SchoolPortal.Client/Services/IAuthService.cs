using SchoolPortal.Shared.DTOs.Auth;

namespace SchoolPortal.Client.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<UserInfo?> GetCurrentUserAsync();
}
