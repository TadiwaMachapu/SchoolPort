namespace SchoolPortal.Data.Entities;

public class Gr9SubjectAdviceCache
{
    public Guid Gr9SubjectAdviceCacheId { get; set; }
    public Guid StudentId { get; set; }
    public string InputFingerprint { get; set; } = null!;
    public string AdviceJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
