using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Shared.DTOs.Assignments;
using SchoolPortal.Shared.DTOs.Common;

namespace SchoolPortal.Server.Services;

public class AssignmentService : IAssignmentService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AssignmentService> _logger;
    private readonly INotificationService _notifications;
    private readonly IScopeService _scope;

    public AssignmentService(SchoolPortalDbContext context, ICurrentUserService currentUser,
        ILogger<AssignmentService> logger, INotificationService notifications, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _notifications = notifications;
        _scope = scope;
    }

    public async Task<PaginatedResult<AssignmentDto>> GetAssignmentsAsync(Guid? classId, DateTime? dueFrom, DateTime? dueTo, string? status, int page, int pageSize)
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

        // Step 7: scope to the caller's accessible classes (Learner → enrolled, Teacher → taught,
        // Parent → children's classes, oversight → all). Explicit classId filter still applies below.
        var accessibleClassIds = await _scope.GetAccessibleClassIdsAsync();
        if (accessibleClassIds is not null)
            query = query.Where(a => accessibleClassIds.Contains(a.ClassSubject.ClassId));

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

    public async Task<AssignmentDto> GetAssignmentByIdAsync(Guid id)
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

        // Notify all students in the class
        _ = _notifications.NotifyRoleAsync(_currentUser.SchoolId, "Student", new Notification(
            Type: "new_assignment",
            Title: "New Assignment",
            Message: $"{assignment.Title} is due {assignment.DueAt:MMM d}",
            Link: $"/assignments/{assignment.AssignmentId}"));

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

    public async Task<AssignmentDto> UpdateAssignmentAsync(Guid id, UpdateAssignmentRequest request)
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
        if (assignment.RowVersion != request.RowVersion)
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
