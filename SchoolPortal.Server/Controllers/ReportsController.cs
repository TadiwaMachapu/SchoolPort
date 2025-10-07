using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolPortal.Server.Services;
using System.Data;

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
    public async Task<IActionResult> GetAttendanceSummary([FromQuery] int? classId, [FromQuery] int? year)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var results = new List<Dictionary<string, object>>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand("SELECT * FROM vw_AttendanceSummary WHERE SchoolId = @SchoolId", connection);
        command.Parameters.AddWithValue("@SchoolId", _currentUser.SchoolId);

        if (classId.HasValue)
        {
            command.CommandText += " AND ClassId = @ClassId";
            command.Parameters.AddWithValue("@ClassId", classId.Value);
        }

        if (year.HasValue)
        {
            command.CommandText += " AND YEAR(Date) = @Year";
            command.Parameters.AddWithValue("@Year", year.Value);
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
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
    public async Task<IActionResult> GetGradebookSimple([FromQuery] int? classId)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var results = new List<Dictionary<string, object>>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand("SELECT * FROM vw_GradebookSimple WHERE SchoolId = @SchoolId", connection);
        command.Parameters.AddWithValue("@SchoolId", _currentUser.SchoolId);

        if (classId.HasValue)
        {
            command.CommandText += " AND ClassId = @ClassId";
            command.Parameters.AddWithValue("@ClassId", classId.Value);
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return Ok(results);
    }
}
