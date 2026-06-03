using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Hubs;
using SchoolPortal.Shared.DTOs.Notifications;

namespace SchoolPortal.Server.Services;

public record Notification(
    string Type,
    string Title,
    string Message,
    string? Link = null)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public interface INotificationService
{
    Task NotifySchoolAsync(Guid schoolId, Notification notification);
    Task NotifyRoleAsync(Guid schoolId, string role, Notification notification);
    Task NotifyUserAsync(Guid userId, Notification notification);
    Task<NotificationsResponse> GetForCurrentUserAsync(int limit = 30);
    Task MarkReadAsync(Guid notificationId);
    Task MarkAllReadAsync();
}

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(
        IHubContext<NotificationHub> hub,
        SchoolPortalDbContext context,
        ICurrentUserService currentUser)
    {
        _hub = hub;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task NotifySchoolAsync(Guid schoolId, Notification notification)
    {
        var userIds = await _context.Users
            .Where(u => u.SchoolId == schoolId && u.IsActive)
            .Select(u => u.UserId)
            .ToListAsync();

        await PersistForUsersAsync(userIds, schoolId, notification);
        await _hub.Clients.Group($"school:{schoolId}").SendAsync("notification", notification);
    }

    public async Task NotifyRoleAsync(Guid schoolId, string role, Notification notification)
    {
        var userIds = await _context.Users
            .Where(u => u.SchoolId == schoolId && u.Role == role && u.IsActive)
            .Select(u => u.UserId)
            .ToListAsync();

        await PersistForUsersAsync(userIds, schoolId, notification);
        await _hub.Clients.Group($"school:{schoolId}:role:{role}").SendAsync("notification", notification);
    }

    public async Task NotifyUserAsync(Guid userId, Notification notification)
    {
        var entity = new PersistedNotification
        {
            NotificationId = Guid.NewGuid(),
            UserId = userId,
            SchoolId = _currentUser.SchoolId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Link = notification.Link,
            IsRead = false,
            CreatedAt = notification.Timestamp
        };

        _context.Notifications.Add(entity);
        await _context.SaveChangesAsync();
        await _hub.Clients.Group($"user:{userId}").SendAsync("notification", notification);
    }

    public async Task<NotificationsResponse> GetForCurrentUserAsync(int limit = 30)
    {
        var userId = _currentUser.UserId;

        var items = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                Link = n.Link,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        return new NotificationsResponse { Items = items, UnreadCount = unreadCount };
    }

    public async Task MarkReadAsync(Guid notificationId)
    {
        var userId = _currentUser.UserId;
        var n = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        if (n != null)
        {
            n.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllReadAsync()
    {
        var userId = _currentUser.UserId;
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    private async Task PersistForUsersAsync(List<Guid> userIds, Guid schoolId, Notification notification)
    {
        if (userIds.Count == 0) return;

        var entities = userIds.Select(uid => new PersistedNotification
        {
            NotificationId = Guid.NewGuid(),
            UserId = uid,
            SchoolId = schoolId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Link = notification.Link,
            IsRead = false,
            CreatedAt = notification.Timestamp
        }).ToList();

        _context.Notifications.AddRange(entities);
        await _context.SaveChangesAsync();
    }
}
