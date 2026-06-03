namespace SchoolPortal.Data.Entities;

public class DataSubjectRequest
{
    public Guid RequestId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public string RequestType { get; set; } = null!; // Access, Deletion, Correction, Portability
    public string? Description { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Rejected
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
