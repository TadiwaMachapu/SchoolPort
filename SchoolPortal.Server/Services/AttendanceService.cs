using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Shared.DTOs.Attendance;
using System.Text;

namespace SchoolPortal.Server.Services;

public class AttendanceService : IAttendanceService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AttendanceService> _logger;
    private readonly INotificationService _notifications;
    private readonly IScopeService _scope;
    private const int BatchSize = 100;

    public AttendanceService(
        SchoolPortalDbContext context,
        ICurrentUserService currentUser,
        ILogger<AttendanceService> logger,
        INotificationService notifications,
        IScopeService scope)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _notifications = notifications;
        _scope = scope;
    }

    public async Task<List<AttendanceDto>> GetAttendanceAsync(Guid classId, DateTime date)
    {
        // Step 7 IDOR: a class outside the caller's scope returns empty, never another class's data.
        if (!await _scope.CanAccessClassAsync(classId))
            return new List<AttendanceDto>();

        var attendances = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.ClassId == classId && a.SchoolId == _currentUser.SchoolId && a.Date.Date == date.Date)
            .Include(a => a.Student)
                .ThenInclude(s => s.User)
            .OrderBy(a => a.Student.User.LastName)
            .ThenBy(a => a.Student.User.FirstName)
            .Select(a => new AttendanceDto
            {
                AttendanceId = a.AttendanceId,
                ClassId = a.ClassId,
                StudentId = a.StudentId,
                StudentName = a.Student.User.FirstName + " " + a.Student.User.LastName,
                StudentNumber = a.Student.StudentNumber,
                Date = a.Date,
                Status = a.Status,
                Notes = a.Notes
            })
            .ToListAsync();

        if (attendances.Count > 0)
            return attendances;

        // No attendance taken yet — pre-populate all enrolled students as Present
        // so the teacher can use the "mark all present → tap exceptions" flow.
        // Load the entity graph first, then project in-memory to avoid EF Core
        // translation issues with constants (Guid.Empty, null) inside Select().
        var enrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.SchoolId == _currentUser.SchoolId && e.IsActive)
            .Include(e => e.Student)
                .ThenInclude(s => s.User)
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .ToListAsync();

        return enrollments.Select(e => new AttendanceDto
        {
            AttendanceId = Guid.Empty,
            ClassId = classId,
            StudentId = e.StudentId,
            StudentName = e.Student.User.FirstName + " " + e.Student.User.LastName,
            StudentNumber = e.Student.StudentNumber,
            Date = date,
            Status = 1,
            Notes = null
        }).ToList();
    }

    public async Task<List<MyAttendanceSummaryDto>> GetMyAttendanceAsync(int? month, int? year)
    {
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.SchoolId;

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SchoolId == schoolId)
            ?? throw new KeyNotFoundException("Student record not found");

        var from = new DateTime(year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month, 1);
        var to   = from.AddMonths(1).AddDays(-1);

        var enrolledClassIds = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == student.StudentId && e.SchoolId == schoolId && e.IsActive)
            .Select(e => e.ClassId)
            .ToListAsync();

        var classes = await _context.Classes
            .AsNoTracking()
            .Where(c => enrolledClassIds.Contains(c.ClassId))
            .Select(c => new { c.ClassId, c.Name })
            .ToListAsync();

        var records = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.StudentId == student.StudentId && a.SchoolId == schoolId
                     && a.Date >= from && a.Date <= to)
            .Select(a => new { a.ClassId, a.Date, a.Status, a.Notes })
            .ToListAsync();

        return classes.Select(c =>
        {
            var classRecords = records.Where(r => r.ClassId == c.ClassId).ToList();
            var present = classRecords.Count(r => r.Status == 1);
            var absent  = classRecords.Count(r => r.Status == 0);
            var late    = classRecords.Count(r => r.Status == 2);
            var total   = classRecords.Count;
            return new MyAttendanceSummaryDto
            {
                ClassId       = c.ClassId,
                ClassName     = c.Name,
                TotalDays     = total,
                Present       = present,
                Absent        = absent,
                Late          = late,
                AttendanceRate = total == 0 ? 100 : Math.Round((present + late * 0.5) / total * 100, 1),
                Records       = classRecords
                    .OrderByDescending(r => r.Date)
                    .Select(r => new AttendanceDayDto { Date = r.Date, Status = r.Status, Notes = r.Notes })
                    .ToList()
            };
        }).ToList();
    }

    public async Task BulkUpsertAttendanceAsync(BulkAttendanceRequest request)
    {
        // Validate statuses
        foreach (var item in request.Attendances)
        {
            if (item.Status < 0 || item.Status > 2)
            {
                throw new ArgumentException($"Invalid status value: {item.Status}. Must be 0 (Absent), 1 (Present), or 2 (Late)");
            }
        }

        // Step 7 IDOR (write): every class being captured must be in the caller's scope → 403 otherwise.
        foreach (var classId in request.Attendances.Select(a => a.ClassId).Distinct())
            await _scope.EnsureClassAsync(classId);

        var schoolId = _currentUser.SchoolId;
        var totalRecords = request.Attendances.Count;
        var processedRecords = 0;

        // Process in batches for efficiency with large datasets
        var batches = request.Attendances
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / BatchSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            await ExecuteBatchUpsertAsync(batch, schoolId);
            processedRecords += batch.Count;
        }

        _logger.LogInformation("Bulk upserted {Count} attendance records for SchoolId {SchoolId}",
            totalRecords, schoolId);

        // Notify parents of absent students
        var absentStudentIds = request.Attendances
            .Where(a => a.Status == 0)
            .Select(a => a.StudentId)
            .Distinct()
            .ToList();

        if (absentStudentIds.Count > 0)
        {
            var parentUserIds = await _context.Students
                .Where(s => absentStudentIds.Contains(s.StudentId) && s.ParentUserId != null)
                .Select(s => s.ParentUserId!.Value)
                .Distinct()
                .ToListAsync();

            var date = request.Attendances.FirstOrDefault()?.Date ?? DateTime.UtcNow;
            foreach (var parentUserId in parentUserIds)
            {
                _ = _notifications.NotifyUserAsync(parentUserId, new Notification(
                    Type: "attendance_absent",
                    Title: "Attendance Alert",
                    Message: $"Your child was marked absent on {date:MMM d, yyyy}.",
                    Link: "/dashboard"));
            }
        }
    }

    private async Task ExecuteBatchUpsertAsync(List<AttendanceItem> items, Guid schoolId)
    {
        if (items.Count == 0) return;

        var sql = new StringBuilder();
        var parameters = new List<NpgsqlParameter>();
        var paramIndex = 0;

        sql.AppendLine(@"
INSERT INTO attendances (school_id, class_id, student_id, date, status, notes, created_at, row_version)
VALUES ");

        var valuesClauses = new List<string>();

        foreach (var item in items)
        {
            var schoolIdParam = $"@p{paramIndex++}";
            var classIdParam = $"@p{paramIndex++}";
            var studentIdParam = $"@p{paramIndex++}";
            var dateParam = $"@p{paramIndex++}";
            var statusParam = $"@p{paramIndex++}";
            var notesParam = $"@p{paramIndex++}";

            valuesClauses.Add($"({schoolIdParam}, {classIdParam}, {studentIdParam}, {dateParam}, {statusParam}, {notesParam}, NOW(), 1)");

            parameters.Add(new NpgsqlParameter(schoolIdParam, schoolId));
            parameters.Add(new NpgsqlParameter(classIdParam, item.ClassId));
            parameters.Add(new NpgsqlParameter(studentIdParam, item.StudentId));
            parameters.Add(new NpgsqlParameter(dateParam, item.Date.Date));
            parameters.Add(new NpgsqlParameter(statusParam, item.Status));
            parameters.Add(new NpgsqlParameter(notesParam, (object?)item.Notes ?? DBNull.Value));
        }

        sql.AppendLine(string.Join(",\n", valuesClauses));
        sql.AppendLine(@"
ON CONFLICT (school_id, class_id, student_id, date)
DO UPDATE SET
    status = EXCLUDED.status,
    notes = EXCLUDED.notes,
    updated_at = NOW(),
    row_version = attendances.row_version + 1;");

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters.ToArray());
    }
}
