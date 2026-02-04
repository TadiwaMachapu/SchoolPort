namespace SchoolPortal.Shared.DTOs.Attendance;

public class BulkAttendanceRequest
{
    public List<AttendanceItem> Attendances { get; set; } = new();
}

public class AttendanceItem
{
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
}
