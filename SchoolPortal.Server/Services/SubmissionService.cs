using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Grades;
using SchoolPortal.Shared.DTOs.Submissions;

namespace SchoolPortal.Server.Services;

public interface ISubmissionService
{
    Task<Guid> CreateSubmissionAsync(Guid assignmentId, string? comments, string? fileUrl = null, string? fileName = null);
    Task<List<SubmissionDto>> GetSubmissionsByAssignmentAsync(Guid assignmentId);
    Task<SubmissionDto?> GetMySubmissionAsync(Guid assignmentId);
    Task<List<PendingSubmissionDto>> GetPendingSubmissionsAsync(int limit = 20);
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
            throw new InvalidOperationException("Student not found");

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

    public async Task<List<PendingSubmissionDto>> GetPendingSubmissionsAsync(int limit = 20)
    {
        var schoolId = _currentUser.SchoolId;
        var query = _context.Submissions
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && s.Grade == null);

        // Teachers only see submissions for their own assignments
        if (_currentUser.Role == "Teacher")
        {
            var teacherId = await _context.Teachers
                .Where(t => t.UserId == _currentUser.UserId)
                .Select(t => t.TeacherId)
                .FirstOrDefaultAsync();
            query = query.Where(s =>
                s.Assignment.ClassSubject.TeacherId == teacherId ||
                s.Assignment.ClassSubject.Class.TeacherId == teacherId);
        }

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
