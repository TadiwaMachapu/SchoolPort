namespace SchoolPortal.Shared.DTOs.Classes;

public class BulkEnrollmentRequest
{
    public List<EnrollmentItem> Enrollments { get; set; } = new();
}

public class EnrollmentItem
{
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
}
