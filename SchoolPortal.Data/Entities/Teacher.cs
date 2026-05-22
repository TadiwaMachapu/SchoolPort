namespace SchoolPortal.Data.Entities;

public class Teacher
{
    public Guid TeacherId { get; set; }
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? Specialization { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
}
