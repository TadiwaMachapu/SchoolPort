namespace SchoolPortal.Data.Entities;

/// <summary>NSC past-paper type (Sprint 1.5.2). Stored as string (TaskType convention).</summary>
public enum PastPaperType
{
    NSCNovember,
    NSCPrelim,
    TrialExam,
    Exemplar,
}

public class MatricPastPaper
{
    public Guid MatricPastPaperId { get; set; }
    public string Subject { get; set; } = null!;
    public int Year { get; set; }
    public int PaperNumber { get; set; }
    // Sprint 1.5.2: paper type, grade, and soft-delete flag. Global reference data — no SchoolId.
    public PastPaperType PaperType { get; set; } = PastPaperType.NSCNovember;
    public int Grade { get; set; } = 12;
    public bool IsActive { get; set; } = true;
    public string Language { get; set; } = "English";
    public string Url { get; set; } = null!;
    public bool HasMemo { get; set; }
    public string? MemoUrl { get; set; }
    public string? Notes { get; set; }
}
