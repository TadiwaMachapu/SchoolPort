using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Users;

namespace SchoolPortal.Server.Services;

public class UserService : IUserService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(SchoolPortalDbContext context, ICurrentUserService currentUser, ILogger<UserService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PaginatedResult<UserDto>> GetUsersAsync(string? role, string? q, int page, int pageSize)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.SchoolId == _currentUser.SchoolId);

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(u =>
                u.Email.Contains(q) ||
                u.FirstName.Contains(q) ||
                u.LastName.Contains(q));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                SchoolId = u.SchoolId,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();

        return new PaginatedResult<UserDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        var exists = await _context.Users
            .AnyAsync(u => u.SchoolId == _currentUser.SchoolId && u.Email == request.Email);

        if (exists)
        {
            throw new InvalidOperationException("A user with this email already exists");
        }

        var user = new User
        {
            SchoolId = _currentUser.SchoolId,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new UserDto
        {
            UserId = user.UserId,
            SchoolId = user.SchoolId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.SchoolId == _currentUser.SchoolId);

        if (user == null)
            throw new KeyNotFoundException("User not found");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new UserDto
        {
            UserId = user.UserId,
            SchoolId = user.SchoolId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.SchoolId == _currentUser.SchoolId);

        if (user == null)
            throw new KeyNotFoundException("User not found");

        // Soft delete
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<MeResponse> GetMeAsync(Guid userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.School)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        return new MeResponse
        {
            User = new UserProfile
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role
            },
            School = new SchoolInfo
            {
                SchoolId = user.SchoolId,
                Name = user.School.Name,
                LogoUrl = user.School.BrandingLogoUrl,
                PrimaryColor = user.School.BrandingPrimaryColor
            }
        };
    }
}
