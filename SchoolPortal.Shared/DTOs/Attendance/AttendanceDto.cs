namespace SchoolPortal.Shared.DTOs.Attendance;

public class AttendanceDto
{
    public int AttendanceId { get; set; }
    public int ClassId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentNumber { get; set; } = null!;
    public DateTime Date { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
}
