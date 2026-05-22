namespace SchoolPortal.Data.Entities;

public class AttendanceSummaryView
{
    public int SchoolId { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentNumber { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int TotalDays { get; set; }
}
