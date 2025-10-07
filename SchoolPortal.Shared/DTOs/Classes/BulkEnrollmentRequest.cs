namespace SchoolPortal.Shared.DTOs.Classes;

public class BulkEnrollmentRequest
{
    public List<EnrollmentItem> Enrollments { get; set; } = new();
}

public class EnrollmentItem
{
    public int ClassId { get; set; }
    public int StudentId { get; set; }
}
