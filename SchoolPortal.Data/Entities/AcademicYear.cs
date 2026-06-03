namespace SchoolPortal.Data.Entities;

public class AcademicYear
{
    public Guid AcademicYearId { get; set; }
    public Guid SchoolId { get; set; }
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual ICollection<Term> Terms { get; set; } = new List<Term>();
}
