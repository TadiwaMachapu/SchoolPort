namespace SchoolPortal.Data.Entities;

public class Term
{
    public Guid TermId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid SchoolId { get; set; }
    public int TermNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual AcademicYear AcademicYear { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
