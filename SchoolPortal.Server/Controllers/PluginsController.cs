using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PluginsController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _httpFactory;

    public PluginsController(SchoolPortalDbContext context, ICurrentUserService currentUser, IHttpClientFactory httpFactory)
    {
        _context = context;
        _currentUser = currentUser;
        _httpFactory = httpFactory;
    }

    // Public marketplace
    [HttpGet("marketplace")]
    [Authorize]
    public async Task<IActionResult> GetMarketplace()
    {
        var installed = await _context.PluginInstallations
            .AsNoTracking()
            .Where(i => i.SchoolId == _currentUser.SchoolId)
            .Select(i => i.PluginId)
            .ToListAsync();

        var plugins = await _context.Plugins
            .AsNoTracking()
            .Where(p => p.IsPublic && p.IsApproved)
            .Select(p => new
            {
                p.PluginId,
                p.Name,
                p.Description,
                p.IconUrl,
                p.DeveloperName,
                p.Permissions,
                IsInstalled = installed.Contains(p.PluginId)
            })
            .ToListAsync();

        return Ok(plugins);
    }

    // Installed plugins for this school
    [HttpGet("installed")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetInstalled()
    {
        var installations = await _context.PluginInstallations
            .AsNoTracking()
            .Where(i => i.SchoolId == _currentUser.SchoolId)
            .Include(i => i.Plugin)
            .Select(i => new
            {
                i.InstallationId,
                i.Plugin.PluginId,
                i.Plugin.Name,
                i.Plugin.Description,
                i.Plugin.IconUrl,
                i.Plugin.IframeUrl,
                i.IsActive,
                i.InstalledAt
            })
            .ToListAsync();

        return Ok(installations);
    }

    // Install a plugin
    [HttpPost("{pluginId}/install")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Install(Guid pluginId)
    {
        var plugin = await _context.Plugins
            .FirstOrDefaultAsync(p => p.PluginId == pluginId && p.IsPublic && p.IsApproved)
            ?? throw new KeyNotFoundException("Plugin not found");

        var existing = await _context.PluginInstallations
            .AnyAsync(i => i.PluginId == pluginId && i.SchoolId == _currentUser.SchoolId);

        if (existing) return Conflict("Plugin already installed");

        var installation = new PluginInstallation
        {
            PluginId = pluginId,
            SchoolId = _currentUser.SchoolId,
            IsActive = true,
            InstalledAt = DateTime.UtcNow
        };

        _context.PluginInstallations.Add(installation);
        await _context.SaveChangesAsync();

        // Notify plugin via webhook
        _ = NotifyPluginWebhookAsync(plugin, "installed", _currentUser.SchoolId);

        return Ok(new { installation.InstallationId });
    }

    // Uninstall a plugin
    [HttpDelete("{pluginId}/install")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Uninstall(Guid pluginId)
    {
        var installation = await _context.PluginInstallations
            .FirstOrDefaultAsync(i => i.PluginId == pluginId && i.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Plugin not installed");

        _context.PluginInstallations.Remove(installation);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Register a new plugin (for developers)
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterPluginRequest request)
    {
        var plugin = new Plugin
        {
            Name = request.Name,
            Description = request.Description,
            IconUrl = request.IconUrl,
            WebhookUrl = request.WebhookUrl,
            IframeUrl = request.IframeUrl,
            DeveloperName = request.DeveloperName,
            DeveloperEmail = request.DeveloperEmail,
            Permissions = JsonSerializer.Serialize(request.Permissions),
            IsApproved = false, // requires admin approval
            IsPublic = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Plugins.Add(plugin);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            plugin.PluginId,
            Message = "Plugin registered successfully. It will appear in the marketplace once approved by our team."
        });
    }

    // Dispatch a webhook event to all installed plugins (internal use)
    [HttpPost("dispatch")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Dispatch([FromBody] DispatchEventRequest request)
    {
        var installations = await _context.PluginInstallations
            .Where(i => i.SchoolId == _currentUser.SchoolId && i.IsActive)
            .Include(i => i.Plugin)
            .Where(i => i.Plugin.WebhookUrl != null)
            .ToListAsync();

        foreach (var installation in installations)
        {
            _ = NotifyPluginWebhookAsync(installation.Plugin, request.EventType, _currentUser.SchoolId, request.Payload);
        }

        return Ok(new { Dispatched = installations.Count });
    }

    private async Task NotifyPluginWebhookAsync(Plugin plugin, string eventType, Guid schoolId, object? payload = null)
    {
        if (string.IsNullOrEmpty(plugin.WebhookUrl)) return;

        try
        {
            var client = _httpFactory.CreateClient();
            var body = JsonSerializer.Serialize(new
            {
                eventType,
                schoolId,
                pluginId = plugin.PluginId,
                timestamp = DateTime.UtcNow,
                payload
            });

            await client.PostAsync(plugin.WebhookUrl,
                new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch
        {
            // Webhook delivery failure is non-critical
        }
    }
}

public record RegisterPluginRequest(
    string Name, string DeveloperName, string DeveloperEmail,
    string? Description = null, string? IconUrl = null,
    string? WebhookUrl = null, string? IframeUrl = null,
    List<string>? Permissions = null);

public record DispatchEventRequest(string EventType, object? Payload = null);
