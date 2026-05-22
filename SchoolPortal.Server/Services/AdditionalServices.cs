using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Announcements;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Grades;
using SchoolPortal.Shared.DTOs.Schools;
using SchoolPortal.Shared.DTOs.Subjects;
using SchoolPortal.Shared.DTOs.Submissions;

namespace SchoolPortal.Server.Services;

// School Service
public interface ISchoolService
{
    Task<SchoolDto> GetCurrentSchoolAsync();
    Task<SchoolDto> UpdateThemeAsync(UpdateSchoolThemeRequest request);
    Task<SchoolDto> UpdateFeaturesAsync(UpdateSchoolFeaturesRequest request);
}

public class SchoolService : ISchoolService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SchoolService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<SchoolDto> GetCurrentSchoolAsync()
    {
        var school = await _context.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        return MapToDto(school);
    }

    public async Task<SchoolDto> UpdateThemeAsync(UpdateSchoolThemeRequest request)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        school.Theme ??= new();
        school.Theme.PrimaryColor = request.PrimaryColor;
        school.Theme.LogoUrl = request.LogoUrl;
        school.Theme.FaviconUrl = request.FaviconUrl;
        school.Theme.FontFamily = request.FontFamily;
        school.Theme.WelcomeMessage = request.WelcomeMessage;
        school.Theme.SupportEmail = request.SupportEmail;
        school.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(school);
    }

    public async Task<SchoolDto> UpdateFeaturesAsync(UpdateSchoolFeaturesRequest request)
    {
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("School not found");

        school.Features.Quizzes = request.Quizzes;
        school.Features.Attendance = request.Attendance;
        school.Features.ParentPortal = request.ParentPortal;
        school.Features.Messaging = request.Messaging;
        school.Features.Courses = request.Courses;
        school.Features.Analytics = request.Analytics;
        school.Features.AiGrading = request.AiGrading;
        school.Features.PlagiarismDetection = request.PlagiarismDetection;
        school.Features.Sso = request.Sso;
        school.Features.CustomReports = request.CustomReports;
        school.Features.WhiteLabel = request.WhiteLabel;
        school.Features.PluginApi = request.PluginApi;
        school.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(school);
    }

    private static SchoolDto MapToDto(Data.Entities.School school) => new()
    {
        SchoolId = school.SchoolId,
        Name = school.Name,
        Domain = school.Domain,
        BrandingLogoUrl = school.BrandingLogoUrl,
        BrandingPrimaryColor = school.BrandingPrimaryColor,
        IsActive = school.IsActive,
        Features = school.Features,
        Theme = school.Theme
    };
}

// Class Service
public interface IClassService
{
    Task<PaginatedResult<ClassDto>> GetClassesAsync(int? year, string? q, int page, int pageSize);
    Task<ClassDto> GetClassByIdAsync(Guid id);
    Task<ClassDto> CreateClassAsync(CreateClassRequest request);
    Task<ClassDto> UpdateClassAsync(Guid id, UpdateClassRequest request);
    Task DeleteClassAsync(Guid id);
    Task BulkEnrollAsync(BulkEnrollmentRequest request);
    Task<List<object>> GetStudentsAsync(Guid classId);
    Task<List<object>> GetSubjectsForClassAsync(Guid classId);
}

public class ClassService : IClassService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ClassService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ClassDto>> GetClassesAsync(int? year, string? q, int page, int pageSize)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == _currentUser.SchoolId);

        if (year.HasValue)
        {
            query = query.Where(c => c.AcademicYear == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(c => c.Name.Contains(q));
        }

        var total = await query.CountAsync();
        var items = await query
            .Include(c => c.Teacher).ThenInclude(t => t!.User)
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClassDto
            {
                ClassId = c.ClassId,
                Name = c.Name,
                GradeLevel = c.GradeLevel,
                AcademicYear = c.AcademicYear,
                TeacherId = c.TeacherId,
                TeacherName = c.Teacher != null ? $"{c.Teacher.User.FirstName} {c.Teacher.User.LastName}" : null,
                MaxCapacity = c.MaxCapacity,
                StudentCount = c.Enrollments.Count(e => e.IsActive),
                EnrollmentCount = c.Enrollments.Count(e => e.IsActive)
            })
            .ToListAsync();

        return new PaginatedResult<ClassDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ClassDto> GetClassByIdAsync(Guid id)
    {
        var classEntity = await _context.Classes
            .AsNoTracking()
            .Where(c => c.ClassId == id && c.SchoolId == _currentUser.SchoolId)
            .Include(c => c.Teacher).ThenInclude(t => t!.User)
            .Include(c => c.ClassSubjects).ThenInclude(cs => cs.Subject)
            .Include(c => c.ClassSubjects).ThenInclude(cs => cs.Teacher).ThenInclude(t => t!.User)
            .FirstOrDefaultAsync();

        if (classEntity == null)
        {
            throw new KeyNotFoundException("Class not found");
        }

        return new ClassDto
        {
            ClassId = classEntity.ClassId,
            Name = classEntity.Name,
            GradeLevel = classEntity.GradeLevel,
            AcademicYear = classEntity.AcademicYear,
            TeacherId = classEntity.TeacherId,
            TeacherName = classEntity.Teacher != null ? $"{classEntity.Teacher.User.FirstName} {classEntity.Teacher.User.LastName}" : null,
            MaxCapacity = classEntity.MaxCapacity,
            StudentCount = classEntity.Enrollments.Count(e => e.IsActive),
            EnrollmentCount = classEntity.Enrollments.Count(e => e.IsActive),
            Subjects = classEntity.ClassSubjects.Select(cs => new SubjectInfo
            {
                SubjectId = cs.SubjectId,
                Name = cs.Subject.Name,
                TeacherName = cs.Teacher != null ? $"{cs.Teacher.User.FirstName} {cs.Teacher.User.LastName}" : null
            }).ToList()
        };
    }

    public async Task<ClassDto> CreateClassAsync(CreateClassRequest request)
    {
        var classEntity = new Class
        {
            SchoolId = _currentUser.SchoolId,
            Name = request.Name,
            GradeLevel = request.GradeLevel,
            AcademicYear = request.AcademicYear,
            TeacherId = request.TeacherId,
            MaxCapacity = request.MaxCapacity,
            CreatedAt = DateTime.UtcNow
        };

        _context.Classes.Add(classEntity);
        await _context.SaveChangesAsync();

        return new ClassDto
        {
            ClassId = classEntity.ClassId,
            Name = classEntity.Name,
            GradeLevel = classEntity.GradeLevel,
            AcademicYear = classEntity.AcademicYear,
            TeacherId = classEntity.TeacherId,
            MaxCapacity = classEntity.MaxCapacity,
            StudentCount = 0,
            EnrollmentCount = 0
        };
    }

    public async Task<ClassDto> UpdateClassAsync(Guid id, UpdateClassRequest request)
    {
        var classEntity = await _context.Classes
            .FirstOrDefaultAsync(c => c.ClassId == id && c.SchoolId == _currentUser.SchoolId);

        if (classEntity == null)
            throw new KeyNotFoundException("Class not found");

        classEntity.Name = request.Name;
        classEntity.GradeLevel = request.GradeLevel;
        classEntity.AcademicYear = request.AcademicYear;
        classEntity.TeacherId = request.TeacherId;
        classEntity.MaxCapacity = request.MaxCapacity;
        classEntity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new ClassDto
        {
            ClassId = classEntity.ClassId,
            Name = classEntity.Name,
            GradeLevel = classEntity.GradeLevel,
            AcademicYear = classEntity.AcademicYear,
            TeacherId = classEntity.TeacherId,
            MaxCapacity = classEntity.MaxCapacity,
            StudentCount = 0,
            EnrollmentCount = 0
        };
    }

    public async Task DeleteClassAsync(Guid id)
    {
        var classEntity = await _context.Classes
            .FirstOrDefaultAsync(c => c.ClassId == id && c.SchoolId == _currentUser.SchoolId);

        if (classEntity == null)
            throw new KeyNotFoundException("Class not found");

        _context.Classes.Remove(classEntity);
        await _context.SaveChangesAsync();
    }

    public async Task BulkEnrollAsync(BulkEnrollmentRequest request)
    {
        var classIds = request.Enrollments.Select(e => e.ClassId).ToList();
        var studentIds = request.Enrollments.Select(e => e.StudentId).ToList();

        var existing = await _context.Enrollments
            .Where(e => classIds.Contains(e.ClassId) && studentIds.Contains(e.StudentId))
            .ToListAsync();

        var existingLookup = existing.ToDictionary(e => (e.ClassId, e.StudentId));

        foreach (var item in request.Enrollments)
        {
            if (existingLookup.TryGetValue((item.ClassId, item.StudentId), out var existingEnrollment))
            {
                if (!existingEnrollment.IsActive)
                {
                    existingEnrollment.IsActive = true;
                    existingEnrollment.EnrolledAt = DateTime.UtcNow;
                    existingEnrollment.DroppedAt = null;
                }
            }
            else
            {
                _context.Enrollments.Add(new Enrollment
                {
                    ClassId = item.ClassId,
                    StudentId = item.StudentId,
                    SchoolId = _currentUser.SchoolId,
                    EnrolledAt = DateTime.UtcNow,
                    IsActive = true
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<object>> GetStudentsAsync(Guid classId)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.IsActive && e.Class.SchoolId == _currentUser.SchoolId)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .OrderBy(e => e.Student.User.LastName)
            .Select(e => (object)new
            {
                UserId = e.Student.UserId,
                SchoolId = e.Student.SchoolId,
                Email = e.Student.User.Email,
                FirstName = e.Student.User.FirstName,
                LastName = e.Student.User.LastName,
                Role = "Student",
                IsActive = e.Student.User.IsActive,
                CreatedAt = e.Student.User.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<object>> GetSubjectsForClassAsync(Guid classId)
    {
        return await _context.ClassSubjects
            .AsNoTracking()
            .Where(cs => cs.ClassId == classId && cs.Class.SchoolId == _currentUser.SchoolId)
            .Include(cs => cs.Subject)
            .Include(cs => cs.Teacher).ThenInclude(t => t!.User)
            .OrderBy(cs => cs.Subject.Name)
            .Select(cs => (object)new
            {
                ClassSubjectId = cs.ClassSubjectId,
                SubjectId = cs.SubjectId,
                SubjectName = cs.Subject.Name,
                TeacherId = cs.TeacherId,
                TeacherName = cs.Teacher != null ? $"{cs.Teacher.User.FirstName} {cs.Teacher.User.LastName}" : null
            })
            .ToListAsync();
    }
}

// Subject Service
public interface ISubjectService
{
    Task<List<SubjectDto>> GetSubjectsAsync();
    Task<SubjectDto> GetSubjectByIdAsync(Guid id);
    Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request);
    Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectRequest request);
    Task DeleteSubjectAsync(Guid id);
    Task BulkAssignClassSubjectsAsync(BulkClassSubjectRequest request);
}

public class SubjectService : ISubjectService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubjectService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<SubjectDto>> GetSubjectsAsync()
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(s => s.SchoolId == _currentUser.SchoolId)
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto
            {
                SubjectId = s.SubjectId,
                Name = s.Name,
                Code = s.Code,
                Description = s.Description
            })
            .ToListAsync();
    }

    public async Task<SubjectDto> GetSubjectByIdAsync(Guid id)
    {
        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.SchoolId == _currentUser.SchoolId);

        if (subject == null)
            throw new KeyNotFoundException("Subject not found");

        return new SubjectDto
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description
        };
    }

    public async Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request)
    {
        var subject = new Subject
        {
            SchoolId = _currentUser.SchoolId,
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        return new SubjectDto
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description
        };
    }

    public async Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectRequest request)
    {
        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.SchoolId == _currentUser.SchoolId);

        if (subject == null)
            throw new KeyNotFoundException("Subject not found");

        subject.Name = request.Name;
        subject.Code = request.Code;
        subject.Description = request.Description;
        subject.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new SubjectDto
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description
        };
    }

    public async Task DeleteSubjectAsync(Guid id)
    {
        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.SchoolId == _currentUser.SchoolId);

        if (subject == null)
            throw new KeyNotFoundException("Subject not found");

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync();
    }

    public async Task BulkAssignClassSubjectsAsync(BulkClassSubjectRequest request)
    {
        var classIds = request.ClassSubjects.Select(cs => cs.ClassId).ToList();
        var subjectIds = request.ClassSubjects.Select(cs => cs.SubjectId).ToList();

        var existing = await _context.ClassSubjects
            .Where(cs => classIds.Contains(cs.ClassId) && subjectIds.Contains(cs.SubjectId))
            .ToListAsync();

        var existingLookup = existing.ToDictionary(cs => (cs.ClassId, cs.SubjectId));

        foreach (var item in request.ClassSubjects)
        {
            if (existingLookup.TryGetValue((item.ClassId, item.SubjectId), out var existingCs))
            {
                if (item.TeacherId.HasValue)
                    existingCs.TeacherId = item.TeacherId;
            }
            else
            {
                _context.ClassSubjects.Add(new ClassSubject
                {
                    ClassId = item.ClassId,
                    SubjectId = item.SubjectId,
                    TeacherId = item.TeacherId,
                    SchoolId = _currentUser.SchoolId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}

// Submission Service
public interface ISubmissionService
{
    Task<Guid> CreateSubmissionAsync(Guid assignmentId, string? comments, string? fileUrl = null, string? fileName = null);
    Task<List<SubmissionDto>> GetSubmissionsByAssignmentAsync(Guid assignmentId);
    Task<SubmissionDto?> GetMySubmissionAsync(Guid assignmentId);
}

public class SubmissionService : ISubmissionService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubmissionService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> CreateSubmissionAsync(Guid assignmentId, string? comments, string? fileUrl = null, string? fileName = null)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == Guid.Empty)
        {
            throw new InvalidOperationException("Student not found");
        }

        var submission = new Submission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            SchoolId = _currentUser.SchoolId,
            SubmittedAt = DateTime.UtcNow,
            Comments = comments,
            FileUrl = fileUrl,
            FileName = fileName
        };

        _context.Submissions.Add(submission);
        await _context.SaveChangesAsync();

        return submission.SubmissionId;
    }

    public async Task<List<SubmissionDto>> GetSubmissionsByAssignmentAsync(Guid assignmentId)
    {
        return await _context.Submissions
            .AsNoTracking()
            .Where(s => s.AssignmentId == assignmentId && s.SchoolId == _currentUser.SchoolId)
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Grade).ThenInclude(g => g!.GradedByUser)
            .OrderBy(s => s.Student.User.LastName)
            .Select(s => new SubmissionDto
            {
                SubmissionId = s.SubmissionId,
                AssignmentId = s.AssignmentId,
                StudentId = s.StudentId,
                StudentName = $"{s.Student.User.FirstName} {s.Student.User.LastName}",
                StudentNumber = s.Student.StudentNumber,
                SubmittedAt = s.SubmittedAt,
                FileUrl = s.FileUrl,
                FileName = s.FileName,
                Comments = s.Comments,
                Grade = s.Grade != null ? new GradeInfo
                {
                    GradeId = s.Grade.GradeId,
                    Score = s.Grade.Score,
                    Feedback = s.Grade.Feedback,
                    GradedAt = s.Grade.GradedAt
                } : null
            })
            .ToListAsync();
    }

    public async Task<SubmissionDto?> GetMySubmissionAsync(Guid assignmentId)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == Guid.Empty) return null;

        return await _context.Submissions
            .AsNoTracking()
            .Where(s => s.AssignmentId == assignmentId && s.StudentId == studentId)
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Grade)
            .Select(s => new SubmissionDto
            {
                SubmissionId = s.SubmissionId,
                AssignmentId = s.AssignmentId,
                StudentId = s.StudentId,
                StudentName = $"{s.Student.User.FirstName} {s.Student.User.LastName}",
                StudentNumber = s.Student.StudentNumber,
                SubmittedAt = s.SubmittedAt,
                FileUrl = s.FileUrl,
                FileName = s.FileName,
                Comments = s.Comments,
                Grade = s.Grade != null ? new GradeInfo
                {
                    GradeId = s.Grade.GradeId,
                    Score = s.Grade.Score,
                    Feedback = s.Grade.Feedback,
                    GradedAt = s.Grade.GradedAt
                } : null
            })
            .FirstOrDefaultAsync();
    }
}

public interface IGradeService
{
    Task CreateGradeAsync(CreateGradeRequest request);
    Task BulkGradeAsync(BulkGradeRequest request);
}

public class GradeService : IGradeService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public GradeService(SchoolPortalDbContext context, ICurrentUserService currentUser, INotificationService notifications)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task CreateGradeAsync(CreateGradeRequest request)
    {
        var submission = await _context.Submissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.SubmissionId == request.SubmissionId && s.SchoolId == _currentUser.SchoolId);

        if (submission == null)
        {
            throw new KeyNotFoundException("Submission not found");
        }

        if (request.Score < 0 || request.Score > submission.Assignment.MaxMarks)
        {
            throw new ArgumentException($"Score must be between 0 and {submission.Assignment.MaxMarks}");
        }

        var grade = new Grade
        {
            SubmissionId = request.SubmissionId,
            SchoolId = _currentUser.SchoolId,
            Score = request.Score,
            Feedback = request.Feedback,
            GradedByUserId = _currentUser.UserId,
            GradedAt = DateTime.UtcNow
        };

        _context.Grades.Add(grade);
        await _context.SaveChangesAsync();

        // Notify the student
        var studentUserId = await _context.Students
            .Where(s => s.StudentId == submission.StudentId)
            .Select(s => s.UserId)
            .FirstOrDefaultAsync();

        if (studentUserId != Guid.Empty)
        {
            _ = _notifications.NotifyUserAsync(studentUserId, new Notification(
                Type: "grade_posted",
                Title: "Grade Posted",
                Message: $"Your submission for {submission.Assignment.Title} has been graded: {request.Score}/{submission.Assignment.MaxMarks}",
                Link: $"/assignments/{submission.AssignmentId}"));
        }
    }

    public async Task BulkGradeAsync(BulkGradeRequest request)
    {
        var submissionIds = request.Grades.Select(g => g.SubmissionId).ToList();

        var submissions = await _context.Submissions
            .Include(s => s.Assignment)
            .Where(s => submissionIds.Contains(s.SubmissionId) && s.SchoolId == _currentUser.SchoolId)
            .ToListAsync();

        var existingGrades = await _context.Grades
            .Where(g => submissionIds.Contains(g.SubmissionId))
            .ToListAsync();

        var submissionLookup = submissions.ToDictionary(s => s.SubmissionId);
        var gradeLookup = existingGrades.ToDictionary(g => g.SubmissionId);

        foreach (var item in request.Grades)
        {
            if (!submissionLookup.TryGetValue(item.SubmissionId, out var submission)) continue;
            if (item.Score < 0 || item.Score > submission.Assignment.MaxMarks) continue;

            if (gradeLookup.TryGetValue(item.SubmissionId, out var existingGrade))
            {
                existingGrade.Score = item.Score;
                existingGrade.Feedback = item.Feedback;
                existingGrade.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.Grades.Add(new Grade
                {
                    SubmissionId = item.SubmissionId,
                    SchoolId = _currentUser.SchoolId,
                    Score = item.Score,
                    Feedback = item.Feedback,
                    GradedByUserId = _currentUser.UserId,
                    GradedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}

// Announcement Service
public interface IAnnouncementService
{
    Task<PaginatedResult<AnnouncementDto>> GetAnnouncementsAsync(DateTime? since, int page, int pageSize);
    Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request);
    Task<AnnouncementDto> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request);
    Task DeleteAnnouncementAsync(Guid id);
}

public class AnnouncementService : IAnnouncementService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AnnouncementService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<AnnouncementDto>> GetAnnouncementsAsync(DateTime? since, int page, int pageSize)
    {
        var query = _context.Announcements
            .AsNoTracking()
            .Where(a => a.SchoolId == _currentUser.SchoolId && a.IsActive);

        if (since.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= since.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AnnouncementDto
            {
                AnnouncementId = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Audience = a.Audience,
                AudienceValue = a.AudienceValue,
                CreatedByName = $"{a.CreatedByUser.FirstName} {a.CreatedByUser.LastName}",
                CreatedAt = a.CreatedAt,
                ExpiresAt = a.ExpiresAt,
                IsActive = a.IsActive
            })
            .ToListAsync();

        return new PaginatedResult<AnnouncementDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request)
    {
        if (request.Audience != "All" && string.IsNullOrWhiteSpace(request.AudienceValue))
        {
            throw new ArgumentException("AudienceValue is required when Audience is not 'All'");
        }

        var announcement = new Announcement
        {
            SchoolId = _currentUser.SchoolId,
            Title = request.Title,
            Content = request.Content,
            Audience = request.Audience,
            AudienceValue = request.AudienceValue,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        await _context.Entry(announcement).Reference(a => a.CreatedByUser).LoadAsync();

        return new AnnouncementDto
        {
            AnnouncementId = announcement.AnnouncementId,
            Title = announcement.Title,
            Content = announcement.Content,
            Audience = announcement.Audience,
            AudienceValue = announcement.AudienceValue,
            CreatedByName = $"{announcement.CreatedByUser.FirstName} {announcement.CreatedByUser.LastName}",
            CreatedAt = announcement.CreatedAt,
            ExpiresAt = announcement.ExpiresAt,
            IsActive = announcement.IsActive
        };
    }

    public async Task<AnnouncementDto> UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request)
    {
        if (request.Audience != "All" && string.IsNullOrWhiteSpace(request.AudienceValue))
            throw new ArgumentException("AudienceValue is required when Audience is not 'All'");

        var announcement = await _context.Announcements
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.AnnouncementId == id && a.SchoolId == _currentUser.SchoolId);

        if (announcement == null)
            throw new KeyNotFoundException("Announcement not found");

        announcement.Title = request.Title;
        announcement.Content = request.Content;
        announcement.Audience = request.Audience;
        announcement.AudienceValue = request.AudienceValue;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.IsActive = request.IsActive;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AnnouncementDto
        {
            AnnouncementId = announcement.AnnouncementId,
            Title = announcement.Title,
            Content = announcement.Content,
            Audience = announcement.Audience,
            AudienceValue = announcement.AudienceValue,
            CreatedByName = $"{announcement.CreatedByUser.FirstName} {announcement.CreatedByUser.LastName}",
            CreatedAt = announcement.CreatedAt,
            ExpiresAt = announcement.ExpiresAt,
            IsActive = announcement.IsActive
        };
    }

    public async Task DeleteAnnouncementAsync(Guid id)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == id && a.SchoolId == _currentUser.SchoolId);

        if (announcement == null)
            throw new KeyNotFoundException("Announcement not found");

        // Soft delete
        announcement.IsActive = false;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
