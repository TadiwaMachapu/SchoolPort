namespace SchoolPortal.Data.Entities;

public class Career
{
    public Guid CareerId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Category { get; set; }

    public virtual ICollection<UniversityCourse> Courses { get; set; } = new List<UniversityCourse>();
}
