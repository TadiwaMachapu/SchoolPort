namespace SchoolPortal.Shared.DTOs.Subjects;

/// <summary>A selectable teacher for class-subject assignment. TeacherId is the Teacher entity
/// PK (what ClassSubject.TeacherId / Class.TeacherId reference), NOT the User id.</summary>
public class TeacherOptionDto
{
    public Guid TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
}
