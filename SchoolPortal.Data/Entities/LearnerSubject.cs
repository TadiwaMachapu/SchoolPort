namespace SchoolPortal.Data.Entities;

public class LearnerSubject
{
    public Guid LearnerSubjectId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid SchoolId { get; set; }
    public DateTime EnrolledAt { get; set; }

    public virtual Student Student { get; set; } = null!;
    public virtual Subject Subject { get; set; } = null!;
    public virtual AcademicYear AcademicYear { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
