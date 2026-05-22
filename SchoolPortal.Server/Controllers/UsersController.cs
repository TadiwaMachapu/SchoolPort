using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Common;
using SchoolPortal.Shared.DTOs.Users;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolPortalDbContext _context;

    public UsersController(IUserService userService, ICurrentUserService currentUser, SchoolPortalDbContext context)
    {
        _userService = userService;
        _currentUser = currentUser;
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PaginatedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _userService.GetUsersAsync(role, q, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Returns lightweight user records for all active users in the current school.
    /// Available to any authenticated user so that Teachers and Students can find message recipients.
    /// </summary>
    [HttpGet("directory")]
    [ProducesResponseType(typeof(IEnumerable<DirectoryUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDirectory([FromQuery] string? q)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.SchoolId == _currentUser.SchoolId && u.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(u =>
                u.Email.Contains(q) ||
                u.FirstName.Contains(q) ||
                u.LastName.Contains(q));
        }

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new DirectoryUserDto
            {
                UserId = u.UserId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                Email = u.Email
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Returns a CSV template with headers for bulk user import.
    /// </summary>
    [HttpGet("import-csv")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetImportCsvTemplate()
    {
        var csv = "FirstName,LastName,Email,Role\n";
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "users_import_template.csv");
    }

    /// <summary>
    /// Accepts a multipart CSV upload and bulk-creates users. Returns a summary of created/failed rows.
    /// </summary>
    [HttpPost("import-csv")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        if (!file.ContentType.Contains("csv") && !file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "File must be a CSV." });

        var created = 0;
        var failed = new List<object>();
        var validRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Teacher", "Student", "Parent" };

        using var reader = new System.IO.StreamReader(file.OpenReadStream());
        var headerLine = await reader.ReadLineAsync();
        if (headerLine == null)
            return BadRequest(new { message = "CSV file is empty." });

        // Validate header
        var headers = headerLine.Split(',');
        var expectedHeaders = new[] { "FirstName", "LastName", "Email", "Role" };
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
            headerMap[headers[i].Trim()] = i;

        foreach (var h in expectedHeaders)
        {
            if (!headerMap.ContainsKey(h))
                return BadRequest(new { message = $"Missing required column: {h}" });
        }

        var rowNumber = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            if (cols.Length < 4)
            {
                failed.Add(new { row = rowNumber, reason = "Not enough columns (expected 4: FirstName,LastName,Email,Role)" });
                continue;
            }

            var firstName = cols[headerMap["FirstName"]].Trim().Trim('"');
            var lastName  = cols[headerMap["LastName"]].Trim().Trim('"');
            var email     = cols[headerMap["Email"]].Trim().Trim('"');
            var role      = cols[headerMap["Role"]].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(firstName))
            { failed.Add(new { row = rowNumber, reason = "FirstName is required" }); continue; }
            if (string.IsNullOrWhiteSpace(lastName))
            { failed.Add(new { row = rowNumber, reason = "LastName is required" }); continue; }
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            { failed.Add(new { row = rowNumber, reason = "Email is invalid" }); continue; }
            if (!validRoles.Contains(role))
            { failed.Add(new { row = rowNumber, reason = $"Role must be one of: Admin, Teacher, Student, Parent (got '{role}')" }); continue; }

            // Normalise role to proper casing
            var normalisedRole = validRoles.First(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

            // Generate a temporary password — admin should prompt user to reset via school process
            var tempPassword = $"Temp@{Guid.NewGuid().ToString("N")[..8]}1";

            try
            {
                await _userService.CreateUserAsync(new CreateUserRequest
                {
                    FirstName = firstName,
                    LastName  = lastName,
                    Email     = email,
                    Role      = normalisedRole,
                    Password  = tempPassword
                });
                created++;
            }
            catch (Exception ex)
            {
                failed.Add(new { row = rowNumber, reason = ex.Message });
            }
        }

        return Ok(new { created, failed });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateUserAsync(request);
        return CreatedAtAction(nameof(GetUsers), new { }, user);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateUserAsync(id, request);
        return Ok(user);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        await _userService.DeleteUserAsync(id);
        return NoContent();
    }
}

public class DirectoryUserDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Email { get; set; } = null!;
}
