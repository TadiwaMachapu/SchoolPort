namespace SchoolPortal.Data.Entities;

public class PrincipalSummaryCache
{
    public Guid PrincipalSummaryCacheId { get; set; }
    public Guid ClassId { get; set; }
    public Guid TermId { get; set; }
    public string InputFingerprint { get; set; } = null!;
    public string SummaryMarkdown { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
