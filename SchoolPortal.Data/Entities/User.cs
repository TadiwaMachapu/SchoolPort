namespace SchoolPortal.Data.Entities;

public class User
{
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!; // Admin, Teacher, Student, Parent — legacy; kept readable during Sprint 1.5.0 transition
    public string? Identity { get; set; }      // Staff, Learner, Parent, External, System — Layer 1 boundary (nullable until backfilled)
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual Student? Student { get; set; }
    public virtual Teacher? Teacher { get; set; }
}
