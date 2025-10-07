using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Users;

namespace SchoolPortal.Server.Services;

public interface IUserService
{
    Task<PaginatedResult<UserDto>> GetUsersAsync(string? role, string? q, int page, int pageSize);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<MeResponse> GetMeAsync(int userId);
}
