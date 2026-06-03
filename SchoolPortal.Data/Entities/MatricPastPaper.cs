namespace SchoolPortal.Data.Entities;

public class MatricPastPaper
{
    public Guid MatricPastPaperId { get; set; }
    public string Subject { get; set; } = null!;
    public int Year { get; set; }
    public int PaperNumber { get; set; }
    public string Language { get; set; } = "English";
    public string Url { get; set; } = null!;
    public bool HasMemo { get; set; }
    public string? MemoUrl { get; set; }
    public string? Notes { get; set; }
}
