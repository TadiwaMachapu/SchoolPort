namespace SchoolPortal.Data.Entities;

public class MatricQuizQuestion
{
    public Guid MatricQuizQuestionId { get; set; }
    public string Subject { get; set; } = null!;
    public string Difficulty { get; set; } = "Medium";
    public string QuestionText { get; set; } = null!;
    public string OptionA { get; set; } = null!;
    public string OptionB { get; set; } = null!;
    public string OptionC { get; set; } = null!;
    public string OptionD { get; set; } = null!;
    public string CorrectOption { get; set; } = null!;
    public string? Explanation { get; set; }
}
