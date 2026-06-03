namespace SchoolPortal.Data.Entities;

public class AiGapAnalysisCache
{
    public Guid AiGapAnalysisCacheId { get; set; }
    public Guid StudentId { get; set; }
    public Guid UniversityCourseId { get; set; }
    public string InputFingerprint { get; set; } = null!;
    public string ResultJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    public virtual Student Student { get; set; } = null!;
    public virtual UniversityCourse UniversityCourse { get; set; } = null!;
}
