using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Tests.Security.Infrastructure;

/// <summary>
/// Shared add-only seed helpers for the Step 10 cross-tenant guard tests (the caller saves). Every
/// helper stamps the given schoolId, so a "foreign" graph is built simply by passing a different
/// schoolId. Keeps the per-controller test files focused on the assertion, not the wiring.
/// </summary>
public static class Seed
{
    public static Guid School(SchoolPortalDbContext db)
    {
        var id = Guid.NewGuid();
        db.Schools.Add(new School { SchoolId = id, Name = "S" + id.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid User(SchoolPortalDbContext db, Guid schoolId, string role = "Teacher", string identity = "Staff")
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { UserId = id, SchoolId = schoolId, Email = $"u_{id:N}@test.local", PasswordHash = "x", FirstName = "U", LastName = "X", Role = role, Identity = identity, IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Student(SchoolPortalDbContext db, Guid schoolId) => StudentFor(db, schoolId, User(db, schoolId, "Student", "Learner"));

    public static Guid StudentFor(SchoolPortalDbContext db, Guid schoolId, Guid userId)
    {
        var id = Guid.NewGuid();
        db.Students.Add(new Student { StudentId = id, SchoolId = schoolId, UserId = userId, StudentNumber = "N" + userId.ToString("N")[..6], CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid TeacherFor(SchoolPortalDbContext db, Guid schoolId, Guid userId)
    {
        var id = Guid.NewGuid();
        db.Teachers.Add(new Teacher { TeacherId = id, UserId = userId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Subject(SchoolPortalDbContext db, Guid schoolId)
    {
        var id = Guid.NewGuid();
        db.Subjects.Add(new Subject { SubjectId = id, SchoolId = schoolId, Name = "Sub" + id.ToString("N")[..4], Code = "S" + id.ToString("N")[..3], CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Class(SchoolPortalDbContext db, Guid schoolId, Guid? teacherId = null)
    {
        var id = Guid.NewGuid();
        db.Classes.Add(new Class { ClassId = id, SchoolId = schoolId, Name = "C" + id.ToString("N")[..4], TeacherId = teacherId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid ClassSubject(SchoolPortalDbContext db, Guid schoolId, Guid? classId = null, Guid? teacherId = null)
    {
        var id = Guid.NewGuid();
        db.ClassSubjects.Add(new ClassSubject
        {
            ClassSubjectId = id, ClassId = classId ?? Class(db, schoolId), SubjectId = Subject(db, schoolId),
            TeacherId = teacherId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow,
        });
        return id;
    }

    public static Guid Course(SchoolPortalDbContext db, Guid schoolId, Guid? classSubjectId = null)
    {
        var id = Guid.NewGuid();
        db.Courses.Add(new Course { CourseId = id, SchoolId = schoolId, ClassSubjectId = classSubjectId, Title = "Course " + id.ToString("N")[..4], IsPublished = false, CreatedByUserId = User(db, schoolId), CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Module(SchoolPortalDbContext db, Guid courseId, int order = 0)
    {
        var id = Guid.NewGuid();
        db.CourseModules.Add(new CourseModule { ModuleId = id, CourseId = courseId, Title = "Module", Order = order, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Lesson(SchoolPortalDbContext db, Guid moduleId, int order = 0)
    {
        var id = Guid.NewGuid();
        db.Lessons.Add(new Lesson { LessonId = id, ModuleId = moduleId, Title = "Lesson", Type = "Text", Order = order, IsPublished = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Activity(SchoolPortalDbContext db, Guid schoolId, string name = "Activity", Guid? ownerUserId = null)
    {
        var id = Guid.NewGuid();
        db.Activities.Add(new Activity { ActivityId = id, SchoolId = schoolId, Name = name, ActivityType = "Sport", Date = DateTime.UtcNow, OwnerUserId = ownerUserId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid ActivityParticipant(SchoolPortalDbContext db, Guid schoolId, Guid activityId, Guid studentId)
    {
        var id = Guid.NewGuid();
        db.ActivityParticipants.Add(new ActivityParticipant { ActivityParticipantId = id, ActivityId = activityId, StudentId = studentId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
        return id;
    }

    public static Guid Notification(SchoolPortalDbContext db, Guid schoolId, Guid userId)
    {
        var id = Guid.NewGuid();
        db.Notifications.Add(new PersistedNotification { NotificationId = id, UserId = userId, SchoolId = schoolId, Type = "test", Title = "T", Message = "M", IsRead = false, CreatedAt = DateTime.UtcNow });
        return id;
    }
}
