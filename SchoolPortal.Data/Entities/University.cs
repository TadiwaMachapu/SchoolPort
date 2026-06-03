namespace SchoolPortal.Data.Entities;

public class University
{
    public Guid UniversityId { get; set; }
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string Province { get; set; } = null!;
    public string? Website { get; set; }

    public virtual ICollection<UniversityCourse> Courses { get; set; } = new List<UniversityCourse>();
}
