using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Users;

namespace SchoolPortal.Server.Services;

public interface IUserService
{
    Task<PaginatedResult<UserDto>> GetUsersAsync(string? role, string? q, int page, int pageSize);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request);
    Task DeleteUserAsync(Guid id);
    Task<MeResponse> GetMeAsync(Guid userId);
}
