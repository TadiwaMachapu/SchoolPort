namespace SchoolPortal.Data.Entities;

public class GradebookSimpleView
{
    public int SchoolId { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public int AssignmentId { get; set; }
    public string AssignmentTitle { get; set; } = null!;
    public decimal MaxMarks { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentNumber { get; set; } = null!;
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime GradedAt { get; set; }
    public decimal Percentage { get; set; }
}
