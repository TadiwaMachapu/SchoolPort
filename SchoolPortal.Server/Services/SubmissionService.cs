using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Shared.DTOs.Grades;
using SchoolPortal.Shared.DTOs.Submissions;

namespace SchoolPortal.Server.Services;

public interface ISubmissionService
{
    /// <summary>Validates the assignment belongs to the caller's school (throws KeyNotFound → 404).
    /// Call BEFORE any file upload so a foreign/invalid id can't leave an orphan object in storage.</summary>
    Task EnsureAssignmentInSchoolAsync(Guid assignmentId);
    Task<Guid> CreateSubmissionAsync(Guid assignmentId, string? comments, string? fileUrl = null, string? fileName = null);
    Task<List<SubmissionDto>> GetSubmissionsByAssignmentAsync(Guid assignmentId);
    Task<SubmissionDto?> GetMySubmissionAsync(Guid assignmentId);
    Task<List<PendingSubmissionDto>> GetPendingSubmissionsAsync(int limit = 20);
}

public class SubmissionService : ISubmissionService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IScopeService _scope;

    public SubmissionService(SchoolPortalDbContext context, ICurrentUserService currentUser, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _scope = scope;
    }

    // Step 10 (Teaching cluster, H1-class): assignmentId is a body id — it must belong to the
    // caller's school, else a learner could create a submission linked to another school's
    // assignment (the FK resolves across tenants; SchoolId alone would silently mislink).
    public async Task EnsureAssignmentInSchoolAsync(Guid assignmentId)
    {
        if (!await _context.Assignments.AnyAsync(a => a.AssignmentId == assignmentId && a.SchoolId == _currentUser.SchoolId))
            throw new KeyNotFoundException("Assignment not found");
    }

    public async Task<Guid> CreateSubmissionAsync(Guid assignmentId, string? comments, string? fileUrl = null, string? fileName = null)
    {
        var studentId = await _context.Students
            .Where(s => s.UserId == _currentUser.UserId)
            .Select(s => s.StudentId)
            .FirstOrDefaultAsync();

        if (studentId == Guid.Empty)
            throw new InvalidOperationException("Student not found");

        // Defense-in-depth (also enforced pre-upload in the controller via EnsureAssignmentInSchoolAsync).
        await EnsureAssignmentInSchoolAsync(assignmentId);

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
        // Step 7 IDOR: only return submissions if the assignment's class is in the caller's scope.
        var classId = await _context.Assignments.AsNoTracking()
            .Where(a => a.AssignmentId == assignmentId && a.SchoolId == _currentUser.SchoolId)
            .Select(a => (Guid?)a.ClassSubject.ClassId).FirstOrDefaultAsync();
        if (classId is null || !await _scope.CanAccessClassAsync(classId.Value))
            return new List<SubmissionDto>();

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

    public async Task<List<PendingSubmissionDto>> GetPendingSubmissionsAsync(int limit = 20)
    {
        var schoolId = _currentUser.SchoolId;
        var query = _context.Submissions
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && s.Grade == null);

        // Step 7: scope to the caller's accessible classes (teacher → own classes; oversight → all).
        var accessibleClassIds = await _scope.GetAccessibleClassIdsAsync();
        if (accessibleClassIds is not null)
            query = query.Where(s => accessibleClassIds.Contains(s.Assignment.ClassSubject.ClassId));

        return await query
            .OrderBy(s => s.SubmittedAt)
            .Take(limit)
            .Select(s => new PendingSubmissionDto
            {
                SubmissionId = s.SubmissionId,
                AssignmentId = s.AssignmentId,
                AssignmentTitle = s.Assignment.Title,
                MaxMarks = s.Assignment.MaxMarks,
                StudentName = $"{s.Student.User.FirstName} {s.Student.User.LastName}",
                ClassName = s.Assignment.ClassSubject.Class.Name,
                SubjectName = s.Assignment.ClassSubject.Subject.Name,
                SubmittedAt = s.SubmittedAt,
                HasFile = s.FileUrl != null
            })
            .ToListAsync();
    }
}
