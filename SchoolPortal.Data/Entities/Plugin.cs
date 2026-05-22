namespace SchoolPortal.Data.Entities;

public class Plugin
{
    public Guid PluginId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? WebhookUrl { get; set; }
    public string? IframeUrl { get; set; }
    public string DeveloperName { get; set; } = null!;
    public string DeveloperEmail { get; set; } = null!;
    public bool IsApproved { get; set; }
    public bool IsPublic { get; set; }
    public string Permissions { get; set; } = "[]"; // JSON array of permission strings
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<PluginInstallation> Installations { get; set; } = new List<PluginInstallation>();
}

public class PluginInstallation
{
    public Guid InstallationId { get; set; }
    public Guid PluginId { get; set; }
    public Guid SchoolId { get; set; }
    public string? Configuration { get; set; } // JSON config
    public bool IsActive { get; set; }
    public DateTime InstalledAt { get; set; }

    public virtual Plugin Plugin { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
