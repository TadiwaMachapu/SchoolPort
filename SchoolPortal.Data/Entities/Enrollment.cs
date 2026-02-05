namespace SchoolPortal.Data.Entities;

public class Enrollment
{
    public int EnrollmentId { get; set; }
    public int ClassId { get; set; }
    public int StudentId { get; set; }
    public int SchoolId { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? DroppedAt { get; set; }
    public bool IsActive { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual Class Class { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
