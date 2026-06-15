namespace SchoolPortal.Shared.DTOs.Academics;

// Sprint 1.5.0 Step 8 — single aggregated payload backing the learner My Academics page
// (Subjects / My Marks / Assignments tabs). Percentages only; CAPS codes (1-7) are derived
// client-side via getCapsCode (per design decision). Assignments and quizzes are unified into
// one Tasks list (Source distinguishes them) for the Assignments tab.
public class MyAcademicsResponse
{
    public MyAcademicsTerm? CurrentTerm { get; set; }
    public List<MyAcademicsTerm> Terms { get; set; } = new();
    public List<MyAcademicsSubject> Subjects { get; set; } = new();
    public List<MyAcademicsTask> Tasks { get; set; } = new();
}

public class MyAcademicsTerm
{
    public Guid TermId { get; set; }
    public int TermNumber { get; set; }
    public bool IsCurrent { get; set; }
}

public class MyAcademicsSubject
{
    public Guid ClassSubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public string? TeacherName { get; set; }
    public string? CapsPhase { get; set; }

    // Current-term summary (the Subjects-tab card hero figures).
    public double? TermAveragePercent { get; set; }
    public int TasksAssessed { get; set; }
    public int TasksTotal { get; set; }

    // "up" | "down" | "flat" | "none" — current term average vs the previous term's.
    public string Trend { get; set; } = "none";
}

public class MyAcademicsTask
{
    public Guid TaskId { get; set; }
    public string Source { get; set; } = null!;        // "assignment" | "quiz"
    public Guid ClassSubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;          // TaskType (assignment) or "Quiz"
    public int? TermNumber { get; set; }               // term whose date-range contains the task

    public DateTime? Date { get; set; }                // graded/submitted date, else due date
    public DateTime? DueAt { get; set; }
    public decimal? Score { get; set; }
    public decimal? OutOf { get; set; }
    public double? Percent { get; set; }
    public string Status { get; set; } = null!;        // "not_submitted" | "submitted" | "graded"
}
