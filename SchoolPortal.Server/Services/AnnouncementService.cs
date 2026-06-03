using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Announcements;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Services;

public interface IAnnouncementService
{
    Task<PaginatedResult<AnnouncementDto>> GetAnnouncementsAsync(DateTime? since, int page, int pageSize);
    Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request);
    Task<AnnouncementDto> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request);
    Task DeleteAnnouncementAsync(Guid id);
}

public class AnnouncementService : IAnnouncementService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AnnouncementService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<AnnouncementDto>> GetAnnouncementsAsync(DateTime? since, int page, int pageSize)
    {
        var query = _context.Announcements
            .AsNoTracking()
            .Where(a => a.SchoolId == _currentUser.SchoolId && a.IsActive);

        if (since.HasValue)
            query = query.Where(a => a.CreatedAt >= since.Value);

        var total = await query.CountAsync();
        var items = await query
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AnnouncementDto
            {
                AnnouncementId = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Audience = a.Audience,
                AudienceValue = a.AudienceValue,
                CreatedByName = $"{a.CreatedByUser.FirstName} {a.CreatedByUser.LastName}",
                CreatedAt = a.CreatedAt,
                ExpiresAt = a.ExpiresAt,
                IsActive = a.IsActive
            })
            .ToListAsync();

        return new PaginatedResult<AnnouncementDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request)
    {
        var announcement = new Announcement
        {
            SchoolId = _currentUser.SchoolId,
            Title = request.Title,
            Content = request.Content,
            Audience = request.Audience,
            AudienceValue = request.AudienceValue,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        await _context.Entry(announcement).Reference(a => a.CreatedByUser).LoadAsync();

        return new AnnouncementDto
        {
            AnnouncementId = announcement.AnnouncementId,
            Title = announcement.Title,
            Content = announcement.Content,
            Audience = announcement.Audience,
            AudienceValue = announcement.AudienceValue,
            CreatedByName = announcement.CreatedByUser != null
                ? $"{announcement.CreatedByUser.FirstName} {announcement.CreatedByUser.LastName}"
                : "Unknown",
            CreatedAt = announcement.CreatedAt,
            ExpiresAt = announcement.ExpiresAt,
            IsActive = announcement.IsActive
        };
    }

    public async Task<AnnouncementDto> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request)
    {
        var announcement = await _context.Announcements
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.AnnouncementId == id && a.SchoolId == _currentUser.SchoolId);

        if (announcement == null)
            throw new KeyNotFoundException("Announcement not found");

        announcement.Title = request.Title;
        announcement.Content = request.Content;
        announcement.Audience = request.Audience;
        announcement.AudienceValue = request.AudienceValue;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.IsActive = request.IsActive;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AnnouncementDto
        {
            AnnouncementId = announcement.AnnouncementId,
            Title = announcement.Title,
            Content = announcement.Content,
            Audience = announcement.Audience,
            AudienceValue = announcement.AudienceValue,
            CreatedByName = announcement.CreatedByUser != null
                ? $"{announcement.CreatedByUser.FirstName} {announcement.CreatedByUser.LastName}"
                : "Unknown",
            CreatedAt = announcement.CreatedAt,
            ExpiresAt = announcement.ExpiresAt,
            IsActive = announcement.IsActive
        };
    }

    public async Task DeleteAnnouncementAsync(Guid id)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == id && a.SchoolId == _currentUser.SchoolId);

        if (announcement == null)
            throw new KeyNotFoundException("Announcement not found");

        // Soft delete
        announcement.IsActive = false;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
