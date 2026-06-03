namespace SchoolPortal.Shared.DTOs.Attendance;

public class MyAttendanceSummaryDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public int TotalDays { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public double AttendanceRate { get; set; }
    public List<AttendanceDayDto> Records { get; set; } = new();
}

public class AttendanceDayDto
{
    public DateTime Date { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceDto
{
    public Guid AttendanceId { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentNumber { get; set; } = null!;
    public DateTime Date { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
}
