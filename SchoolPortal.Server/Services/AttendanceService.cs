using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Shared.DTOs.Attendance;
using System.Text;

namespace SchoolPortal.Server.Services;

public class AttendanceService : IAttendanceService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AttendanceService> _logger;
    private const int BatchSize = 100;

    public AttendanceService(
        SchoolPortalDbContext context, 
        ICurrentUserService currentUser,
        ILogger<AttendanceService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<List<AttendanceDto>> GetAttendanceAsync(Guid classId, DateTime date)
    {
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

        return attendances;
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
