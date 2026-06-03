namespace SchoolPortal.Shared.DTOs.Users;

public class UpdateUserRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
}
