using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Shared.DTOs.Grades;

namespace SchoolPortal.Server.Services;

public interface IMarkCaptureService
{
    Task<List<TaskSummaryDto>> GetTasksAsync(Guid classSubjectId);
    Task<TaskMarksDto> GetTaskMarksAsync(Guid classSubjectId, Guid taskId);
    Task<BulkCaptureResultDto> BulkCaptureAsync(BulkCaptureRequest request);
    Task<TaskSummaryDto> CreateTaskAsync(CreateTaskRequest request);
    Task<TaskSummaryDto> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request);
}

/// <summary>
/// Sprint 1.5.2.5 — the marks-capture write path (the most security-critical write after
/// finance). Tenant/scope discipline on every entry point: class-subject resolved by id +
/// SchoolId (404), class checked via IScopeService (403), and every body studentId must be
/// enrolled in that class (404) — so foreign taskId/classSubjectId/studentId all dead-end.
/// Absent rule (Henco markbook): IsAbsent=true REQUIRES a null score and null criteria scores —
/// absent is not zero. Enforced here before any DB write; also protects the audit-log path.
/// </summary>
public class MarkCaptureService : IMarkCaptureService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IScopeService _scope;

    public MarkCaptureService(SchoolPortalDbContext context, ICurrentUserService currentUser, IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _scope = scope;
    }

    public async Task<List<TaskSummaryDto>> GetTasksAsync(Guid classSubjectId)
    {
        var cs = await ResolveClassSubjectAsync(classSubjectId);

        var classSize = await _context.Enrollments.CountAsync(e => e.ClassId == cs.ClassId && e.IsActive);

        var tasks = await _context.Assignments
            .AsNoTracking()
            .Where(a => a.ClassSubjectId == classSubjectId && a.SchoolId == _currentUser.SchoolId)
            .OrderByDescending(a => a.DueAt)
            .Select(a => new TaskSummaryDto
            {
                AssignmentId = a.AssignmentId,
                Title = a.Title,
                TaskType = a.TaskType.ToString(),
                TermNumber = a.TermNumber,
                MaxMarks = a.MaxMarks,
                HasRubric = a.HasRubric,
                SbaWeight = a.SbaWeight,
                DueAt = a.DueAt,
                CapturedCount = a.Grades.Count(g => g.Score != null || g.IsAbsent),
                ClassSize = classSize,
                ApprovalStatus = a.ApprovalRecords
                    .OrderByDescending(r => r.SubmittedAt ?? DateTime.MinValue)
                    .Select(r => r.Status.ToString())
                    .FirstOrDefault(),
            })
            .ToListAsync();

        return tasks;
    }

    public async Task<TaskMarksDto> GetTaskMarksAsync(Guid classSubjectId, Guid taskId)
    {
        var cs = await ResolveClassSubjectAsync(classSubjectId);
        var task = await ResolveTaskAsync(taskId, classSubjectId);

        var criteria = await ActiveCriteriaAsync(taskId);

        var learners = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassId == cs.ClassId && e.IsActive)
            .Select(e => new
            {
                e.StudentId,
                e.Student.User.FirstName,
                e.Student.User.LastName,
                e.Student.StudentNumber,
            })
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .ToListAsync();

        var grades = await _context.Grades
            .AsNoTracking()
            .Include(g => g.CriteriaScores)
            .Where(g => g.AssignmentId == taskId && g.SchoolId == _currentUser.SchoolId)
            .ToDictionaryAsync(g => g.StudentId);

        var approvalStatus = await _context.ApprovalRecords
            .Where(r => r.AssignmentId == taskId)
            .OrderByDescending(r => r.SubmittedAt ?? DateTime.MinValue)
            .Select(r => r.Status.ToString())
            .FirstOrDefaultAsync();

        return new TaskMarksDto
        {
            AssignmentId = task.AssignmentId,
            ClassSubjectId = classSubjectId,
            Title = task.Title,
            TaskType = task.TaskType.ToString(),
            MaxMarks = task.MaxMarks,
            HasRubric = task.HasRubric,
            TermNumber = task.TermNumber,
            SbaWeight = task.SbaWeight,
            ApprovalStatus = approvalStatus,
            Criteria = criteria.Select(c => new CriteriaDto
            {
                CriteriaId = c.CriteriaId, Name = c.Name, MaxMark = c.MaxMark, DisplayOrder = c.DisplayOrder,
            }).ToList(),
            Learners = learners.Select(l =>
            {
                grades.TryGetValue(l.StudentId, out var g);
                return new LearnerMarkDto
                {
                    StudentId = l.StudentId,
                    Name = l.FirstName,
                    Surname = l.LastName,
                    StudentNumber = l.StudentNumber,
                    Score = g?.Score,
                    IsAbsent = g?.IsAbsent ?? false,
                    CriteriaScores = criteria.Select(c => new LearnerCriteriaScoreDto
                    {
                        CriteriaId = c.CriteriaId,
                        Score = g?.CriteriaScores.FirstOrDefault(s => s.CriteriaId == c.CriteriaId)?.Score,
                    }).ToList(),
                };
            }).ToList(),
        };
    }

    public async Task<BulkCaptureResultDto> BulkCaptureAsync(BulkCaptureRequest request)
    {
        var cs = await ResolveClassSubjectAsync(request.ClassSubjectId);
        var task = await ResolveTaskAsync(request.TaskId, request.ClassSubjectId);
        var criteria = await ActiveCriteriaAsync(task.AssignmentId);
        var criteriaById = criteria.ToDictionary(c => c.CriteriaId);

        // Body studentId guard: ONE query loads every active enrolled learner of this class-
        // subject's class into a HashSet (IScopeService.GetEnrolledStudentIdsAsync); each entry
        // validates in memory — 4 security queries total regardless of class size, never 4+N.
        // Foreign-school and same-school-but-other-class studentIds both dead-end as 404.
        var enrolledIds = await _scope.GetEnrolledStudentIdsAsync(request.ClassSubjectId, _currentUser.SchoolId);

        foreach (var entry in request.Entries)
            if (!enrolledIds.Contains(entry.StudentId))
                throw new KeyNotFoundException("Student not enrolled in this class");

        // Validate every entry BEFORE any write — a bulk save is all-or-nothing.
        foreach (var entry in request.Entries)
            ValidateEntry(entry, task, criteriaById);

        var studentIds = request.Entries.Select(e => e.StudentId).ToList();
        var existing = await _context.Grades
            .Include(g => g.CriteriaScores)
            .Where(g => g.AssignmentId == task.AssignmentId && studentIds.Contains(g.StudentId))
            .ToDictionaryAsync(g => g.StudentId);

        var now = DateTime.UtcNow;
        int saved = 0, changed = 0;

        foreach (var entry in request.Entries)
        {
            var newScore = ComputeScore(entry, task);

            if (existing.TryGetValue(entry.StudentId, out var grade))
            {
                // Audit CORRECTIONS only. "Had a captured mark" = a score existed OR the learner
                // was marked absent; a row whose score was still null (pending — e.g. a partially
                // entered rubric) being filled in for the first time is INITIAL CAPTURE, not a
                // change, and creates no log. The absent rule above still holds: an absent row
                // always audits a null NewScore.
                var hadCapturedMark = grade.Score != null || grade.IsAbsent;
                var differs = grade.Score != newScore || grade.IsAbsent != entry.IsAbsent;
                if (hadCapturedMark && differs)
                {
                    _context.MarkCaptureAuditLogs.Add(new MarkCaptureAuditLog
                    {
                        AuditId = Guid.NewGuid(),
                        GradeId = grade.GradeId,
                        SchoolId = _currentUser.SchoolId,
                        ChangedByUserId = _currentUser.UserId,
                        PreviousScore = grade.Score,
                        NewScore = newScore,
                        PreviousIsAbsent = grade.IsAbsent,
                        NewIsAbsent = entry.IsAbsent,
                        ChangeReason = request.ChangeReason,
                        ChangedAt = now,
                    });
                    changed++;
                }

                grade.Score = newScore;
                grade.IsAbsent = entry.IsAbsent;
                grade.UpdatedAt = now;
                grade.GradedByUserId = _currentUser.UserId;
                UpsertCriteriaScores(grade, entry, criteria);
            }
            else
            {
                grade = new Grade
                {
                    GradeId = Guid.NewGuid(),
                    // Capture-grid marks have no submission — SubmissionId stays null.
                    StudentId = entry.StudentId,
                    AssignmentId = task.AssignmentId,
                    SchoolId = _currentUser.SchoolId,
                    Score = newScore,
                    IsAbsent = entry.IsAbsent,
                    GradedByUserId = _currentUser.UserId,
                    GradedAt = now,
                };
                _context.Grades.Add(grade);
                UpsertCriteriaScores(grade, entry, criteria);
            }
            saved++;
        }

        await EnsureOpenApprovalRecordAsync(task.AssignmentId, now);
        await _context.SaveChangesAsync();

        return new BulkCaptureResultDto
        {
            Saved = saved,
            Changed = changed,
            Warnings = await ComputeWarningsAsync(task),
        };
    }

    public async Task<TaskSummaryDto> CreateTaskAsync(CreateTaskRequest request)
    {
        var cs = await ResolveClassSubjectAsync(request.ClassSubjectId);
        var taskType = ValidateTaskFields(request.Title, request.TaskType, request.TermNumber, request.SbaWeight);

        List<TaskCriteriaInput> criteria = request.Criteria ?? new();
        decimal maxMarks;
        if (request.HasRubric)
        {
            if (criteria.Count == 0)
                throw new ArgumentException("A rubric task needs at least one criterion");
            foreach (var c in criteria)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) throw new ArgumentException("Every criterion needs a name");
                if (c.MaxMark <= 0) throw new ArgumentException("Criterion max mark must be positive");
            }
            maxMarks = criteria.Sum(c => c.MaxMark); // total always derives from the rubric
        }
        else
        {
            if (request.MaxMarks <= 0) throw new ArgumentException("Total marks must be positive");
            maxMarks = request.MaxMarks;
        }

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            ClassSubjectId = cs.ClassSubjectId,
            SchoolId = _currentUser.SchoolId,
            Title = request.Title.Trim(),
            TaskType = taskType,
            TermNumber = request.TermNumber,
            MaxMarks = maxMarks,
            HasRubric = request.HasRubric,
            SbaWeight = request.SbaWeight,
            DueAt = request.DueAt ?? DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Assignments.Add(assignment);

        var order = 1;
        foreach (var c in criteria)
        {
            _context.AssessmentCriteria.Add(new AssessmentCriteria
            {
                CriteriaId = Guid.NewGuid(),
                AssignmentId = assignment.AssignmentId,
                SchoolId = _currentUser.SchoolId,
                Name = c.Name.Trim(),
                MaxMark = c.MaxMark,
                DisplayOrder = order++,
            });
        }

        await _context.SaveChangesAsync();

        var classSize = await _context.Enrollments.CountAsync(e => e.ClassId == cs.ClassId && e.IsActive);
        return ToSummary(assignment, classSize);
    }

    public async Task<TaskSummaryDto> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request)
    {
        var task = await _context.Assignments
            .FirstOrDefaultAsync(a => a.AssignmentId == taskId && a.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Task not found");
        var cs = await ResolveClassSubjectAsync(task.ClassSubjectId);

        var taskType = ValidateTaskFields(request.Title, request.TaskType, request.TermNumber, request.SbaWeight);

        // HasRubric and the criteria set are immutable after creation (Week 2 scope) — editing a
        // rubric under captured criteria scores needs its own flow.
        if (!task.HasRubric)
        {
            if (request.MaxMarks <= 0) throw new ArgumentException("Total marks must be positive");
            task.MaxMarks = request.MaxMarks;
        }

        task.Title = request.Title.Trim();
        task.TaskType = taskType;
        task.TermNumber = request.TermNumber;
        task.SbaWeight = request.SbaWeight;
        if (request.DueAt != null) task.DueAt = request.DueAt.Value;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var classSize = await _context.Enrollments.CountAsync(e => e.ClassId == cs.ClassId && e.IsActive);
        return ToSummary(task, classSize);
    }

    // ── Validation ───────────────────────────────────────────────────────────────

    private static void ValidateEntry(BulkCaptureEntry entry, Assignment task,
        IReadOnlyDictionary<Guid, AssessmentCriteria> criteriaById)
    {
        if (entry.IsAbsent)
        {
            // Absent ≠ zero. An absent learner has NO score of any kind.
            if (entry.Score != null || entry.CriteriaScores?.Any(c => c.Score != null) == true)
                throw new ArgumentException("An absent learner cannot have a score — clear the marks or untick absent");
            return;
        }

        if (task.HasRubric)
        {
            foreach (var c in entry.CriteriaScores ?? new List<LearnerCriteriaScoreDto>())
            {
                if (!criteriaById.TryGetValue(c.CriteriaId, out var criterion))
                    throw new KeyNotFoundException("Criterion does not belong to this task");
                if (c.Score is < 0)
                    throw new ArgumentException($"'{criterion.Name}' score cannot be negative");
                if (c.Score > criterion.MaxMark)
                    throw new ArgumentException($"'{criterion.Name}' score cannot exceed {criterion.MaxMark}");
            }
        }
        else
        {
            if (entry.Score is < 0)
                throw new ArgumentException("Score cannot be negative");
            if (entry.Score > task.MaxMarks)
                throw new ArgumentException($"Score cannot exceed {task.MaxMarks}");
        }
    }

    private static TaskType ValidateTaskFields(string title, string taskType, int? termNumber, decimal? sbaWeight)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task name is required");
        if (!Enum.TryParse<TaskType>(taskType, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Unknown task type '{taskType}'");
        if (termNumber is < 1 or > 4)
            throw new ArgumentException("Term must be 1, 2, 3 or 4");
        if (sbaWeight is < 0 or > 100)
            throw new ArgumentException("SBA weight must be between 0 and 100");
        return parsed;
    }

    /// <summary>Rubric tasks: the total is ALWAYS the server-side sum of entered criteria (null
    /// when nothing is entered yet) — the client's score field is ignored. Simple tasks: the
    /// entered score (null = pending).</summary>
    private static decimal? ComputeScore(BulkCaptureEntry entry, Assignment task)
    {
        if (entry.IsAbsent) return null;
        if (!task.HasRubric) return entry.Score;
        var entered = entry.CriteriaScores?.Where(c => c.Score != null).Select(c => c.Score!.Value).ToList();
        return entered is { Count: > 0 } ? entered.Sum() : null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<ClassSubject> ResolveClassSubjectAsync(Guid classSubjectId)
    {
        var cs = await _context.ClassSubjects
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClassSubjectId == classSubjectId && c.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Class subject not found");

        // 403 when the class exists in-school but is outside the caller's teaching scope.
        await _scope.EnsureClassAsync(cs.ClassId);
        return cs;
    }

    private async Task<Assignment> ResolveTaskAsync(Guid taskId, Guid classSubjectId)
    {
        // Task must belong to the (already scope-checked) class-subject — a foreign or
        // mismatched taskId in the body dead-ends here.
        return await _context.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == taskId
                && a.ClassSubjectId == classSubjectId
                && a.SchoolId == _currentUser.SchoolId)
            ?? throw new KeyNotFoundException("Task not found");
    }

    private async Task<List<AssessmentCriteria>> ActiveCriteriaAsync(Guid taskId)
    {
        return await _context.AssessmentCriteria
            .AsNoTracking()
            .Where(c => c.AssignmentId == taskId && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    private void UpsertCriteriaScores(Grade grade, BulkCaptureEntry entry, List<AssessmentCriteria> criteria)
    {
        if (criteria.Count == 0) return;

        var incoming = (entry.CriteriaScores ?? new List<LearnerCriteriaScoreDto>())
            .ToDictionary(c => c.CriteriaId, c => entry.IsAbsent ? null : c.Score);

        foreach (var criterion in criteria)
        {
            incoming.TryGetValue(criterion.CriteriaId, out var score);
            var row = grade.CriteriaScores.FirstOrDefault(s => s.CriteriaId == criterion.CriteriaId);
            if (row != null)
            {
                row.Score = score;
            }
            else
            {
                _context.CriteriaScores.Add(new CriteriaScore
                {
                    CriteriaScoreId = Guid.NewGuid(),
                    GradeId = grade.GradeId,
                    CriteriaId = criterion.CriteriaId,
                    SchoolId = _currentUser.SchoolId,
                    Score = score,
                });
            }
        }
    }

    /// <summary>Every captured task carries exactly one OPEN approval record (Draft until the
    /// teacher submits — Week 3). The partial unique index backs this up at the DB level.</summary>
    private async Task EnsureOpenApprovalRecordAsync(Guid assignmentId, DateTime now)
    {
        var hasOpen = await _context.ApprovalRecords.AnyAsync(r =>
            r.AssignmentId == assignmentId &&
            (r.Status == ApprovalStatus.Draft || r.Status == ApprovalStatus.Submitted));
        if (hasOpen) return;

        _context.ApprovalRecords.Add(new ApprovalRecord
        {
            ApprovalRecordId = Guid.NewGuid(),
            AssignmentId = assignmentId,
            SchoolId = _currentUser.SchoolId,
            SubmittedByUserId = _currentUser.UserId,
            Status = ApprovalStatus.Draft,
        });
    }

    /// <summary>Non-blocking review flags (spec: warn on unusual class average, never block).</summary>
    private async Task<List<string>> ComputeWarningsAsync(Assignment task)
    {
        var warnings = new List<string>();
        if (task.MaxMarks <= 0) return warnings;

        var scores = await _context.Grades
            .AsNoTracking()
            .Where(g => g.AssignmentId == task.AssignmentId && !g.IsAbsent && g.Score != null)
            .Select(g => g.Score!.Value)
            .ToListAsync();
        if (scores.Count < 5) return warnings; // too few marks for a meaningful distribution

        var avgPct = scores.Average() / task.MaxMarks * 100m;
        if (avgPct >= 85m)
            warnings.Add($"Class average is unusually high ({avgPct:0}%) — worth a second look before submitting for review");
        else if (avgPct <= 30m)
            warnings.Add($"Class average is unusually low ({avgPct:0}%) — worth a second look before submitting for review");
        return warnings;
    }

    private static TaskSummaryDto ToSummary(Assignment a, int classSize) => new()
    {
        AssignmentId = a.AssignmentId,
        Title = a.Title,
        TaskType = a.TaskType.ToString(),
        TermNumber = a.TermNumber,
        MaxMarks = a.MaxMarks,
        HasRubric = a.HasRubric,
        SbaWeight = a.SbaWeight,
        DueAt = a.DueAt,
        CapturedCount = 0,
        ClassSize = classSize,
        ApprovalStatus = null,
    };
}
