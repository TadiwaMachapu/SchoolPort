namespace SchoolPortal.Shared.DTOs.Schools;

public class SchoolDto
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Domain { get; set; }
    public string? BrandingLogoUrl { get; set; }
    public string? BrandingPrimaryColor { get; set; }
    public bool IsActive { get; set; }
}
