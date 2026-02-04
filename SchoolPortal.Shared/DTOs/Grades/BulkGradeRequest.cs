namespace SchoolPortal.Shared.DTOs.Grades;

public class BulkGradeRequest
{
    public List<GradeItem> Grades { get; set; } = new();
}

public class GradeItem
{
    public Guid SubmissionId { get; set; }
    public decimal Score { get; set; }
    public string? Feedback { get; set; }
}
