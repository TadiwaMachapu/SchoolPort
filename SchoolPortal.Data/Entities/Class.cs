namespace SchoolPortal.Data.Entities;

public class Class
{
    public Guid ClassId { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public int? GradeLevel { get; set; }
    public int? AcademicYear { get; set; }
    public Guid? TeacherId { get; set; }
    public int? MaxCapacity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual School School { get; set; } = null!;
    public virtual Teacher? Teacher { get; set; }
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
