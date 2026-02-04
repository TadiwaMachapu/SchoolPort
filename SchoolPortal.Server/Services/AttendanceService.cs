using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Shared.DTOs.Attendance;
using System.Data;

namespace SchoolPortal.Server.Services;

public class AttendanceService : IAttendanceService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        SchoolPortalDbContext context, 
        ICurrentUserService currentUser,
        IConfiguration configuration,
        ILogger<AttendanceService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _configuration = configuration;
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
                StudentName = $"{a.Student.User.FirstName} {a.Student.User.LastName}",
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

        // Use TVP for bulk upsert
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Create DataTable for TVP
        var attendanceTable = new DataTable();
        attendanceTable.Columns.Add("ClassId", typeof(int));
        attendanceTable.Columns.Add("StudentId", typeof(int));
        attendanceTable.Columns.Add("Date", typeof(DateTime));
        attendanceTable.Columns.Add("Status", typeof(int));
        attendanceTable.Columns.Add("Notes", typeof(string));
        attendanceTable.Columns.Add("SchoolId", typeof(int));

        foreach (var item in request.Attendances)
        {
            attendanceTable.Rows.Add(
                item.ClassId,
                item.StudentId,
                item.Date.Date,
                item.Status,
                item.Notes ?? (object)DBNull.Value,
                _currentUser.SchoolId
            );
        }

        using var command = new SqlCommand("dbo.usp_Attendance_BulkUpsert", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var parameter = command.Parameters.AddWithValue("@AttendanceData", attendanceTable);
        parameter.SqlDbType = SqlDbType.Structured;
        parameter.TypeName = "dbo.AttendanceTableType";

        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Bulk upserted {Count} attendance records for SchoolId {SchoolId}", 
            request.Attendances.Count, _currentUser.SchoolId);
    }
}
