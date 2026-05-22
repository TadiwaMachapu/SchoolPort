using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Teacher")]
public class ReportsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(IConfiguration configuration, ICurrentUserService currentUser)
    {
        _configuration = configuration;
        _currentUser = currentUser;
    }

    [HttpGet("attendance-summary")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttendanceSummary([FromQuery] Guid? classId, [FromQuery] int? year)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM vw_attendance_summary WHERE school_id = @school_id";
        var parameters = new List<NpgsqlParameter> { new("@school_id", _currentUser.SchoolId) };

        if (classId.HasValue)
        {
            sql += " AND class_id = @class_id";
            parameters.Add(new NpgsqlParameter("@class_id", classId.Value));
        }

        if (year.HasValue)
        {
            sql += " AND year = @year";
            parameters.Add(new NpgsqlParameter("@year", year.Value));
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return Ok(results);
    }

    [HttpGet("gradebook-simple")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradebookSimple([FromQuery] Guid? classId)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM vw_gradebook_simple WHERE school_id = @school_id";
        var parameters = new List<NpgsqlParameter> { new("@school_id", _currentUser.SchoolId) };

        if (classId.HasValue)
        {
            sql += " AND class_id = @class_id";
            parameters.Add(new NpgsqlParameter("@class_id", classId.Value));
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return Ok(results);
    }
}
