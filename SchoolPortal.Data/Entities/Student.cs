namespace SchoolPortal.Data.Entities;

public class Student
{
    public int StudentId { get; set; }
    public int UserId { get; set; }
    public int SchoolId { get; set; }
    public string StudentNumber { get; set; } = null!;
    public int? GradeLevel { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int? ParentUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User? ParentUser { get; set; }
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
