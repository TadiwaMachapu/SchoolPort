namespace SchoolPortal.Data.Entities;

public class MatricTutorCache
{
    public Guid MatricTutorCacheId { get; set; }
    public string Subject { get; set; } = null!;
    public string InputFingerprint { get; set; } = null!;
    public string Question { get; set; } = null!;
    public string AnswerMarkdown { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
