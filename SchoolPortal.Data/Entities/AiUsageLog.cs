namespace SchoolPortal.Data.Entities;

public class AiUsageLog
{
    public Guid AiUsageLogId { get; set; }
    public Guid SchoolId { get; set; }
    public string Feature { get; set; } = null!;
    public Guid? StudentId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostZar { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public virtual School School { get; set; } = null!;
}
