using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Hubs;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize] + [Authorize(Roles="Admin,Teacher")] on class-discussion creation.
// Own threads/DMs/sending → platform.access (participant-checked in code); creating a class
// discussion → communications.message_class (class-teacher function, CS-2).
public class MessagesController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHubContext<NotificationHub> _hub;

    public MessagesController(SchoolPortalDbContext context, ICurrentUserService currentUser, IHubContext<NotificationHub> hub)
    {
        _context = context;
        _currentUser = currentUser;
        _hub = hub;
    }

    [HttpGet("threads")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetThreads()
    {
        var userId = _currentUser.UserId;
        var threads = await _context.MessageThreads
            .AsNoTracking()
            .Where(t => t.SchoolId == _currentUser.SchoolId &&
                        t.Participants.Any(p => p.UserId == userId))
            .Include(t => t.Participants).ThenInclude(p => p.User)
            .Include(t => t.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .ThenInclude(m => m.Sender)
            .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
            .Select(t => new
            {
                t.ThreadId,
                t.Subject,
                t.Type,
                t.ClassId,
                LastMessage = t.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault() == null ? null : new
                {
                    t.Messages.OrderByDescending(m => m.SentAt).First().Content,
                    t.Messages.OrderByDescending(m => m.SentAt).First().SentAt,
                    Sender = t.Messages.OrderByDescending(m => m.SentAt).First().Sender.FirstName
                },
                Participants = t.Participants.Select(p => new
                {
                    p.UserId,
                    Name = $"{p.User.FirstName} {p.User.LastName}",
                    p.User.Role
                }),
                UnreadCount = t.Messages.Count(m => !m.IsDeleted &&
                    m.SentAt > (t.Participants.FirstOrDefault(p => p.UserId == userId)!.LastReadAt ?? DateTime.MinValue))
            })
            .ToListAsync();

        return Ok(threads);
    }

    [HttpGet("threads/{threadId}")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> GetMessages(Guid threadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = _currentUser.UserId;
        var isParticipant = await _context.ThreadParticipants
            .AnyAsync(p => p.ThreadId == threadId && p.UserId == userId);

        if (!isParticipant) return Forbid();

        var messages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId && !m.IsDeleted)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.MessageId,
                m.Content,
                m.SentAt,
                m.SenderUserId,
                SenderName = $"{m.Sender.FirstName} {m.Sender.LastName}",
                m.Sender.Role,
                IsOwn = m.SenderUserId == userId
            })
            .ToListAsync();

        // Mark as read
        var participant = await _context.ThreadParticipants
            .FirstOrDefaultAsync(p => p.ThreadId == threadId && p.UserId == userId);
        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(messages.OrderBy(m => m.SentAt));
    }

    [HttpPost("threads/{threadId}/messages")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> SendMessage(Guid threadId, [FromBody] SendMessageRequest request)
    {
        var userId = _currentUser.UserId;
        var isParticipant = await _context.ThreadParticipants
            .AnyAsync(p => p.ThreadId == threadId && p.UserId == userId);

        if (!isParticipant) return Forbid();

        var message = new ChatMessage
        {
            ThreadId = threadId,
            SenderUserId = userId,
            Content = request.Content,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);

        var thread = await _context.MessageThreads.FindAsync(threadId);
        if (thread != null) thread.LastMessageAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId);
        var senderName = $"{sender?.FirstName} {sender?.LastName}";

        // Broadcast to all thread participants via SignalR
        await _hub.Clients.Group($"thread:{threadId}").SendAsync("newMessage", new
        {
            message.MessageId,
            message.Content,
            message.SentAt,
            message.SenderUserId,
            SenderName = senderName,
            IsOwn = false
        });

        return Ok(new
        {
            message.MessageId,
            message.Content,
            message.SentAt,
            SenderName = senderName,
            IsOwn = true
        });
    }

    [HttpPost("threads/direct")]
    [RequirePermission(PermissionKeys.PlatformAccess)]
    public async Task<IActionResult> CreateDirectThread([FromBody] CreateDirectThreadRequest request)
    {
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.SchoolId;

        // Check if thread already exists between these two users
        var existing = await _context.MessageThreads
            .Where(t => t.SchoolId == schoolId && t.Type == "Direct" &&
                        t.Participants.Any(p => p.UserId == userId) &&
                        t.Participants.Any(p => p.UserId == request.RecipientUserId))
            .FirstOrDefaultAsync();

        if (existing != null) return Ok(new { existing.ThreadId });

        var thread = new MessageThread
        {
            SchoolId = schoolId,
            Type = "Direct",
            Subject = request.Subject,
            CreatedAt = DateTime.UtcNow,
            Participants = new List<ThreadParticipant>
            {
                new() { UserId = userId, JoinedAt = DateTime.UtcNow },
                new() { UserId = request.RecipientUserId, JoinedAt = DateTime.UtcNow }
            }
        };

        _context.MessageThreads.Add(thread);
        await _context.SaveChangesAsync();

        return Ok(new { thread.ThreadId });
    }

    [HttpPost("threads/class/{classId}")]
    [RequirePermission(PermissionKeys.CommunicationsMessageClass)]
    public async Task<IActionResult> CreateClassDiscussion(Guid classId, [FromBody] CreateDirectThreadRequest request)
    {
        var schoolId = _currentUser.SchoolId;

        var enrolledUserIds = await _context.Enrollments
            .Where(e => e.ClassId == classId && e.IsActive)
            .Include(e => e.Student)
            .Select(e => e.Student.UserId)
            .ToListAsync();

        var teacherUserIds = await _context.ClassSubjects
            .Where(cs => cs.ClassId == classId)
            .Where(cs => cs.TeacherId != null)
            .Include(cs => cs.Teacher)
            .Select(cs => cs.Teacher!.UserId)
            .Distinct()
            .ToListAsync();

        var allUserIds = enrolledUserIds.Union(teacherUserIds).Distinct().ToList();

        var thread = new MessageThread
        {
            SchoolId = schoolId,
            ClassId = classId,
            Type = "ClassDiscussion",
            Subject = request.Subject ?? "Class Discussion",
            CreatedAt = DateTime.UtcNow,
            Participants = allUserIds.Select(uid => new ThreadParticipant
            {
                UserId = uid,
                JoinedAt = DateTime.UtcNow
            }).ToList()
        };

        _context.MessageThreads.Add(thread);
        await _context.SaveChangesAsync();

        return Ok(new { thread.ThreadId });
    }
}

public record SendMessageRequest(string Content);
public record CreateDirectThreadRequest(Guid RecipientUserId, string? Subject);
