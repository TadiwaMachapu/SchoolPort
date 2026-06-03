using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
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
            throw new KeyNotFoundException("Submission not found");

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
