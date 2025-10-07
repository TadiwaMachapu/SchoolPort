using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Services;

public class AssignmentService : IAssignmentService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(SchoolPortalDbContext context, ICurrentUserService currentUser, ILogger<AssignmentService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PaginatedResult<AssignmentDto>> GetAssignmentsAsync(int? classId, DateTime? dueFrom, DateTime? dueTo, string? status, int page, int pageSize)
    {
        var query = _context.Assignments
            .AsNoTracking()
            .Where(a => a.SchoolId == _currentUser.SchoolId)
            .Include(a => a.ClassSubject)
                .ThenInclude(cs => cs.Class)
            .Include(a => a.ClassSubject)
                .ThenInclude(cs => cs.Subject)
            .Include(a => a.CreatedByUser)
            .AsQueryable();

        if (classId.HasValue)
        {
            query = query.Where(a => a.ClassSubject.ClassId == classId.Value);
        }

        if (dueFrom.HasValue)
        {
            query = query.Where(a => a.DueAt >= dueFrom.Value);
        }

        if (dueTo.HasValue)
        {
            query = query.Where(a => a.DueAt <= dueTo.Value);
        }

        // Filter by role
        if (_currentUser.Role == "Student")
        {
            // Students only see assignments for their enrolled classes
            var studentId = await _context.Students
                .Where(s => s.UserId == _currentUser.UserId)
                .Select(s => s.StudentId)
                .FirstOrDefaultAsync();

            var enrolledClassIds = await _context.Enrollments
                .Where(e => e.StudentId == studentId && e.IsActive)
                .Select(e => e.ClassId)
                .ToListAsync();

            query = query.Where(a => enrolledClassIds.Contains(a.ClassSubject.ClassId));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.DueAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AssignmentDto
            {
                AssignmentId = a.AssignmentId,
                Title = a.Title,
                Description = a.Description,
                DueAt = a.DueAt,
                MaxMarks = a.MaxMarks,
                CreatedAt = a.CreatedAt,
                ClassName = a.ClassSubject.Class.Name,
                SubjectName = a.ClassSubject.Subject.Name,
                CreatedByName = $"{a.CreatedByUser.FirstName} {a.CreatedByUser.LastName}",
                RowVersion = a.RowVersion
            })
            .ToListAsync();

        return new PaginatedResult<AssignmentDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AssignmentDto> GetAssignmentByIdAsync(int id)
    {
        var assignment = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.AssignmentId == id && a.SchoolId == _currentUser.SchoolId)
            .Include(a => a.ClassSubject)
                .ThenInclude(cs => cs.Class)
            .Include(a => a.ClassSubject)
                .ThenInclude(cs => cs.Subject)
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync();

        if (assignment == null)
        {
            throw new KeyNotFoundException("Assignment not found");
        }

        return new AssignmentDto
        {
            AssignmentId = assignment.AssignmentId,
            Title = assignment.Title,
            Description = assignment.Description,
            DueAt = assignment.DueAt,
            MaxMarks = assignment.MaxMarks,
            CreatedAt = assignment.CreatedAt,
            ClassName = assignment.ClassSubject.Class.Name,
            SubjectName = assignment.ClassSubject.Subject.Name,
            CreatedByName = $"{assignment.CreatedByUser.FirstName} {assignment.CreatedByUser.LastName}",
            RowVersion = assignment.RowVersion
        };
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest request)
    {
        // Validation
        if (request.DueAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("Due date must be in the future");
        }

        if (request.MaxMarks <= 0)
        {
            throw new ArgumentException("MaxMarks must be greater than 0");
        }

        // Verify ClassSubject belongs to the school
        var classSubject = await _context.ClassSubjects
            .Include(cs => cs.Class)
            .Include(cs => cs.Subject)
            .FirstOrDefaultAsync(cs => cs.ClassSubjectId == request.ClassSubjectId && cs.SchoolId == _currentUser.SchoolId);

        if (classSubject == null)
        {
            throw new KeyNotFoundException("ClassSubject not found");
        }

        var assignment = new Assignment
        {
            ClassSubjectId = request.ClassSubjectId,
            SchoolId = _currentUser.SchoolId,
            Title = request.Title,
            Description = request.Description,
            DueAt = request.DueAt,
            MaxMarks = request.MaxMarks,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync();

        // Reload to get related data
        await _context.Entry(assignment).Reference(a => a.CreatedByUser).LoadAsync();

        return new AssignmentDto
        {
            AssignmentId = assignment.AssignmentId,
            Title = assignment.Title,
            Description = assignment.Description,
            DueAt = assignment.DueAt,
            MaxMarks = assignment.MaxMarks,
            CreatedAt = assignment.CreatedAt,
            ClassName = classSubject.Class.Name,
            SubjectName = classSubject.Subject.Name,
            CreatedByName = $"{assignment.CreatedByUser.FirstName} {assignment.CreatedByUser.LastName}",
            RowVersion = assignment.RowVersion
        };
    }

    public async Task<AssignmentDto> UpdateAssignmentAsync(int id, UpdateAssignmentRequest request)
    {
        var assignment = await _context.Assignments
            .Include(a => a.ClassSubject)
                .ThenInclude(cs => cs.Class)
            .Include(a => a.ClassSubject)
                .ThenInclude(cs => cs.Subject)
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.AssignmentId == id && a.SchoolId == _currentUser.SchoolId);

        if (assignment == null)
        {
            throw new KeyNotFoundException("Assignment not found");
        }

        // Concurrency check
        if (!assignment.RowVersion.SequenceEqual(request.RowVersion))
        {
            throw new DbUpdateConcurrencyException("The assignment has been modified by another user");
        }

        // Validation
        if (request.DueAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("Due date must be in the future");
        }

        if (request.MaxMarks <= 0)
        {
            throw new ArgumentException("MaxMarks must be greater than 0");
        }

        assignment.Title = request.Title;
        assignment.Description = request.Description;
        assignment.DueAt = request.DueAt;
        assignment.MaxMarks = request.MaxMarks;
        assignment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AssignmentDto
        {
            AssignmentId = assignment.AssignmentId,
            Title = assignment.Title,
            Description = assignment.Description,
            DueAt = assignment.DueAt,
            MaxMarks = assignment.MaxMarks,
            CreatedAt = assignment.CreatedAt,
            ClassName = assignment.ClassSubject.Class.Name,
            SubjectName = assignment.ClassSubject.Subject.Name,
            CreatedByName = $"{assignment.CreatedByUser.FirstName} {assignment.CreatedByUser.LastName}",
            RowVersion = assignment.RowVersion
        };
    }
}
