using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize(Roles = "Admin")] — now per-action permissions. Reads/integration
// administration require whatsapp_admin; sending the absence broadcast requires whatsapp_trigger.
public class WhatsAppController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WhatsAppController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // GET /api/whatsapp/settings
    [HttpGet("settings")]
    [RequirePermission(PermissionKeys.CommunicationsWhatsAppAdmin)]
    public async Task<IActionResult> GetSettings()
    {
        var school = await _context.Schools
            .AsNoTracking()
            .Where(s => s.SchoolId == _currentUser.SchoolId)
            .Select(s => new { s.Settings })
            .FirstOrDefaultAsync();

        return Ok(school?.Settings.WhatsApp ?? new WhatsAppConfig());
    }

    // PUT /api/whatsapp/settings
    [HttpPut("settings")]
    [RequirePermission(PermissionKeys.CommunicationsWhatsAppAdmin)]
    public async Task<IActionResult> UpdateSettings([FromBody] WhatsAppConfig config)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId);

        if (school == null) return NotFound();

        school.Settings.WhatsApp = config;
        _context.Entry(school).Property(s => s.Settings).IsModified = true;
        await _context.SaveChangesAsync();
        return Ok(config);
    }

    // GET /api/whatsapp/log
    [HttpGet("log")]
    [RequirePermission(PermissionKeys.CommunicationsWhatsAppAdmin)]
    public async Task<IActionResult> GetLog([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = _context.WhatsAppLogs
            .AsNoTracking()
            .Where(l => l.SchoolId == _currentUser.SchoolId)
            .OrderByDescending(l => l.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, items });
    }

    // POST /api/whatsapp/compose — manually compose and queue a message
    [HttpPost("compose")]
    [RequirePermission(PermissionKeys.CommunicationsWhatsAppAdmin)]
    public async Task<IActionResult> Compose([FromBody] ComposeRequest request)
    {
        var school = await _context.Schools
            .AsNoTracking()
            .Where(s => s.SchoolId == _currentUser.SchoolId)
            .Select(s => new { s.Settings, s.Name })
            .FirstOrDefaultAsync();

        var isConfigured = school?.Settings.WhatsApp.Provider != "None"
            && !string.IsNullOrEmpty(school?.Settings.WhatsApp.ApiKey);

        var log = new WhatsAppLog
        {
            SchoolId = _currentUser.SchoolId,
            RecipientName = request.RecipientName,
            RecipientPhone = string.IsNullOrEmpty(request.RecipientPhone) ? "Not set" : request.RecipientPhone,
            TriggerType = "Manual",
            MessageBody = request.Message,
            Status = isConfigured ? "Queued" : "Simulated",
            CreatedAt = DateTime.UtcNow
        };

        _context.WhatsAppLogs.Add(log);
        await _context.SaveChangesAsync();
        return Ok(new { log.WhatsAppLogId, log.Status });
    }

    // POST /api/whatsapp/test — send a test message
    [HttpPost("test")]
    [RequirePermission(PermissionKeys.CommunicationsWhatsAppAdmin)]
    public async Task<IActionResult> SendTest([FromBody] TestMessageRequest request)
    {
        var school = await _context.Schools
            .AsNoTracking()
            .Where(s => s.SchoolId == _currentUser.SchoolId)
            .Select(s => new { s.Settings, s.Name })
            .FirstOrDefaultAsync();

        var isConfigured = school?.Settings.WhatsApp.Provider != "None"
            && !string.IsNullOrEmpty(school?.Settings.WhatsApp.ApiKey);

        var log = new WhatsAppLog
        {
            SchoolId = _currentUser.SchoolId,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            TriggerType = "Test",
            MessageBody = $"[TEST from {school?.Name}] This is a test message from SchoolPortal WhatsApp integration.",
            Status = isConfigured ? "Queued" : "Simulated",
            CreatedAt = DateTime.UtcNow
        };

        _context.WhatsAppLogs.Add(log);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            log.Status,
            Message = isConfigured
                ? "Message queued for delivery via the configured provider."
                : "Provider not configured — message logged as Simulated. Configure API credentials in settings to enable real delivery."
        });
    }

    // POST /api/whatsapp/parents/absence-reminders — queue absence alerts for all absent learners today
    [HttpPost("parents/absence-reminders")]
    [RequirePermission(PermissionKeys.CommunicationsWhatsAppTrigger)]
    public async Task<IActionResult> SendAbsenceReminders([FromQuery] DateTime? date)
    {
        var schoolId = _currentUser.SchoolId;
        var targetDate = (date ?? DateTime.UtcNow).Date;

        var school = await _context.Schools
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .Select(s => new { s.Settings, s.Name })
            .FirstOrDefaultAsync();

        if (school == null) return NotFound();
        var config = school.Settings.WhatsApp;
        var isConfigured = config.Provider != "None" && !string.IsNullOrEmpty(config.ApiKey);

        var absentStudents = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId && a.Date.Date == targetDate && a.Status == 0)
            .Include(a => a.Student).ThenInclude(s => s.User)
            .Include(a => a.Student).ThenInclude(s => s.ParentUser)
            .ToListAsync();

        var logs = new List<WhatsAppLog>();
        foreach (var att in absentStudents)
        {
            var parent = att.Student.ParentUser;
            if (parent == null) continue;

            var message = config.AbsenceTemplate
                .Replace("{ParentName}", $"{parent.FirstName}")
                .Replace("{LearnerName}", $"{att.Student.User.FirstName} {att.Student.User.LastName}")
                .Replace("{Date}", targetDate.ToString("dd MMMM yyyy"))
                .Replace("{SchoolName}", school.Name);

            logs.Add(new WhatsAppLog
            {
                SchoolId = schoolId,
                RecipientName = $"{parent.FirstName} {parent.LastName}",
                RecipientPhone = parent.PhoneNumber ?? "Not set",
                TriggerType = "Absence",
                MessageBody = message,
                Status = isConfigured && !string.IsNullOrEmpty(parent.PhoneNumber) ? "Queued" : "Simulated",
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.WhatsAppLogs.AddRange(logs);
        await _context.SaveChangesAsync();
        return Ok(new { queued = logs.Count, status = isConfigured ? "Queued" : "Simulated" });
    }
}

public record ComposeRequest(string RecipientName, string RecipientPhone, string Message);
public record TestMessageRequest(string RecipientName, string RecipientPhone);
