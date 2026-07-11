namespace SchoolPortal.Data.Entities;

/// <summary>
/// CAPS assessment task type for an <see cref="Assignment"/>. Stored as a string column
/// (see DbContext). Existing rows default to <see cref="Assignment"/>. Quizzes are a separate
/// entity and surface as <see cref="Quiz"/> in the learner's unified task list.
/// PAT (Sprint 1.5.2.5) = Practical Assessment Task — the year-long cumulative CAPS project
/// (Design, Art, Drama) with its own weighting rules.
/// </summary>
public enum TaskType
{
    Assignment,
    Quiz,
    Test,
    Project,
    Practical,
    Exam,
    PAT,
}

public class Assignment
{
    public Guid AssignmentId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; } = TaskType.Assignment;
    public DateTime DueAt { get; set; }
    public decimal MaxMarks { get; set; }
    // Sprint 1.5.2.5 — marks capture
    public bool HasRubric { get; set; }
    public decimal? SbaWeight { get; set; }
    public int? TermNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public long RowVersion { get; set; } = 1;

    // Navigation properties
    public virtual ClassSubject ClassSubject { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public virtual ICollection<Grade> Grades { get; set; } = new List<Grade>();
    public virtual ICollection<AssessmentCriteria> Criteria { get; set; } = new List<AssessmentCriteria>();
    public virtual ICollection<ApprovalRecord> ApprovalRecords { get; set; } = new List<ApprovalRecord>();
}
