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
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId);

        if (school == null)
        {
            throw new KeyNotFoundException("School not found");
        }

        return new SchoolDto
        {
            SchoolId = school.SchoolId,
            Name = school.Name,
            Domain = school.Domain,
            BrandingLogoUrl = school.BrandingLogoUrl,
            BrandingPrimaryColor = school.BrandingPrimaryColor,
            IsActive = school.IsActive
        };
    }
}

// Class Service
public interface IClassService
{
    Task<PaginatedResult<ClassDto>> GetClassesAsync(int? year, string? q, int page, int pageSize);
    Task<ClassDto> GetClassByIdAsync(int id);
    Task<ClassDto> CreateClassAsync(CreateClassRequest request);
    Task BulkEnrollAsync(BulkEnrollmentRequest request);
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

    public async Task<ClassDto> GetClassByIdAsync(int id)
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
            EnrollmentCount = 0
        };
    }

    public async Task BulkEnrollAsync(BulkEnrollmentRequest request)
    {
        foreach (var item in request.Enrollments)
        {
            var existing = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.ClassId == item.ClassId && e.StudentId == item.StudentId);

            if (existing == null)
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
            else if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.EnrolledAt = DateTime.UtcNow;
                existing.DroppedAt = null;
            }
        }

        await _context.SaveChangesAsync();
    }
}

// Subject Service
public interface ISubjectService
{
    Task<List<SubjectDto>> GetSubjectsAsync();
    Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request);
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

    public async Task BulkAssignClassSubjectsAsync(BulkClassSubjectRequest request)
    {
        foreach (var item in request.ClassSubjects)
        {
            var existing = await _context.ClassSubjects
                .FirstOrDefaultAsync(cs => cs.ClassId == item.ClassId && cs.SubjectId == item.SubjectId);

            if (existing == null)
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
            else if (item.TeacherId.HasValue)
            {
                existing.TeacherId = item.TeacherId;
            }
        }

        await _context.SaveChangesAsync();
    }
}

// Submission & Grade Service
public interface ISubmissionService
{
    Task<int> CreateSubmissionAsync(int assignmentId, string? comments);
    Task<List<SubmissionDto>> GetSubmissionsByAssignmentAsync(int assignmentId);
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

    public async Task<int> CreateSubmissionAsync(int assignmentId, string? comments)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == 0)
        {
            throw new InvalidOperationException("Student not found");
        }

        var submission = new Submission
        {
            AssignmentId = assignmentId,
            StudentId = studentId,
            SchoolId = _currentUser.SchoolId,
            SubmittedAt = DateTime.UtcNow,
            Comments = comments
        };

        _context.Submissions.Add(submission);
        await _context.SaveChangesAsync();

        return submission.SubmissionId;
    }

    public async Task<List<SubmissionDto>> GetSubmissionsByAssignmentAsync(int assignmentId)
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

    public GradeService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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
    }

    public async Task BulkGradeAsync(BulkGradeRequest request)
    {
        foreach (var item in request.Grades)
        {
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .FirstOrDefaultAsync(s => s.SubmissionId == item.SubmissionId && s.SchoolId == _currentUser.SchoolId);

            if (submission == null) continue;

            if (item.Score < 0 || item.Score > submission.Assignment.MaxMarks) continue;

            var existingGrade = await _context.Grades
                .FirstOrDefaultAsync(g => g.SubmissionId == item.SubmissionId);

            if (existingGrade != null)
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
}
