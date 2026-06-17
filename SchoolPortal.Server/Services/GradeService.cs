using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Shared.DTOs.Grades;

namespace SchoolPortal.Server.Services;

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
    private readonly IScopeService _scope;

    public GradeService(SchoolPortalDbContext context, ICurrentUserService currentUser, INotificationService notifications, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _notifications = notifications;
        _scope = scope;
    }

    public async Task CreateGradeAsync(CreateGradeRequest request)
    {
        var submission = await _context.Submissions
            .Include(s => s.Assignment).ThenInclude(a => a.ClassSubject)
            .FirstOrDefaultAsync(s => s.SubmissionId == request.SubmissionId && s.SchoolId == _currentUser.SchoolId);

        if (submission == null)
            throw new KeyNotFoundException("Submission not found");

        // Step 7 IDOR (write): can only grade submissions in your in-scope classes → 403 otherwise.
        await _scope.EnsureClassAsync(submission.Assignment.ClassSubject.ClassId);

        if (request.Score < 0 || request.Score > submission.Assignment.MaxMarks)
            throw new ArgumentException($"Score must be between 0 and {submission.Assignment.MaxMarks}");

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
            .Include(s => s.Assignment).ThenInclude(a => a.ClassSubject)
            .Where(s => submissionIds.Contains(s.SubmissionId) && s.SchoolId == _currentUser.SchoolId)
            .ToListAsync();

        // Step 7 IDOR (write): every submission's class must be in the caller's scope → 403 otherwise.
        foreach (var classId in submissions.Select(s => s.Assignment.ClassSubject.ClassId).Distinct())
            await _scope.EnsureClassAsync(classId);

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
