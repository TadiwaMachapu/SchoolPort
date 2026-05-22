namespace SchoolPortal.Data.Entities;

public class Attendance
{
    public Guid AttendanceId { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public DateTime Date { get; set; }
    public int Status { get; set; } // 0=Absent, 1=Present, 2=Late
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual Class Class { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
