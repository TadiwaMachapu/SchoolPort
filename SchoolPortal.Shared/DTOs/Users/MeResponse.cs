namespace SchoolPortal.Shared.DTOs.Users;

public class MeResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = null!;
    public string? SchoolLogoUrl { get; set; }
    public string? SchoolPrimaryColor { get; set; }
}
