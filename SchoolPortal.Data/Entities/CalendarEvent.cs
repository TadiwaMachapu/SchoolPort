namespace SchoolPortal.Data.Entities;

public class CalendarEvent
{
    public Guid EventId { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Type { get; set; } = null!; // Assignment, Exam, Holiday, Meeting, Other
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool AllDay { get; set; }
    public Guid? ClassId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Class? Class { get; set; }
}

public class TimetableSlot
{
    public Guid SlotId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public int DayOfWeek { get; set; } // 1=Mon, 5=Fri
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Room { get; set; }

    public virtual ClassSubject ClassSubject { get; set; } = null!;
}
