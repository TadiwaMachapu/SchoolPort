namespace SchoolPortal.Shared.DTOs.Users;

public class MeResponse
{
    public UserProfile User { get; set; } = null!;
    public SchoolInfo School { get; set; } = null!;
}

public class UserProfile
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class SchoolInfo
{
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
}
