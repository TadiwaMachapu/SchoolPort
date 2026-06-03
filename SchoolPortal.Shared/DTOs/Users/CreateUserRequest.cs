namespace SchoolPortal.Shared.DTOs.Users;

public class CreateUserRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!; // Admin, Teacher, Student, Parent
    public string? PhoneNumber { get; set; }
}
