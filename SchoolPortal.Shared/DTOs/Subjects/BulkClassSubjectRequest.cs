namespace SchoolPortal.Shared.DTOs.Subjects;

public class BulkClassSubjectRequest
{
    public List<ClassSubjectItem> ClassSubjects { get; set; } = new();
}

public class ClassSubjectItem
{
    public int ClassId { get; set; }
    public int SubjectId { get; set; }
    public int? TeacherId { get; set; }
}
