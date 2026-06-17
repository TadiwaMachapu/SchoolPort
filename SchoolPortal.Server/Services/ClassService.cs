using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Services;

public interface IClassService
{
    Task<PaginatedResult<ClassDto>> GetClassesAsync(int? year, string? q, int page, int pageSize, bool scopeToAccessible = false);
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
    private readonly IScopeService _scope;

    public ClassService(SchoolPortalDbContext context, ICurrentUserService currentUser, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _scope = scope;
    }

    public async Task<PaginatedResult<ClassDto>> GetClassesAsync(int? year, string? q, int page, int pageSize, bool scopeToAccessible = false)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == _currentUser.SchoolId);

        // Step 7 / 9.5: narrow to the caller's in-scope classes (driven by IScopeService, not the
        // legacy role string). The controller sets this for everyone except academics.manage holders
        // asking for the full list. School-wide oversight (null scope) sees all even when scoped.
        if (scopeToAccessible)
        {
            var accessible = await _scope.GetAccessibleClassIdsAsync();
            if (accessible is not null)
                query = query.Where(c => accessible.Contains(c.ClassId));
        }

        if (year.HasValue)
            query = query.Where(c => c.AcademicYear == year.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q));

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
            throw new KeyNotFoundException("Class not found");

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
