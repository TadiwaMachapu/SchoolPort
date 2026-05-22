using Microsoft.AspNetCore.SignalR;
using SchoolPortal.Server.Hubs;

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
}

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task NotifySchoolAsync(Guid schoolId, Notification notification) =>
        _hub.Clients.Group($"school:{schoolId}").SendAsync("notification", notification);

    public Task NotifyRoleAsync(Guid schoolId, string role, Notification notification) =>
        _hub.Clients.Group($"school:{schoolId}:role:{role}").SendAsync("notification", notification);

    public Task NotifyUserAsync(Guid userId, Notification notification) =>
        _hub.Clients.Group($"user:{userId}").SendAsync("notification", notification);
}
