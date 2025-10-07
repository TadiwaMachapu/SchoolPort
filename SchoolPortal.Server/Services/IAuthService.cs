using SchoolPortal.Shared.DTOs.Auth;

namespace SchoolPortal.Server.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken);
}
