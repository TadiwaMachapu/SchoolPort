namespace SchoolPortal.Shared.DTOs.Subjects;

public class BulkClassSubjectRequest
{
    public List<ClassSubjectItem> ClassSubjects { get; set; } = new();
}

public class ClassSubjectItem
{
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? TeacherId { get; set; }
}
