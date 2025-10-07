namespace SchoolPortal.Shared.DTOs.Attendance;

public class BulkAttendanceRequest
{
    public List<AttendanceItem> Attendances { get; set; } = new();
}

public class AttendanceItem
{
    public int ClassId { get; set; }
    public int StudentId { get; set; }
    public DateTime Date { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
}
