using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
// Step 6: was [Authorize]. Both endpoints are read-only term lookups → platform.access.
[RequirePermission(PermissionKeys.PlatformAccess)]
public class TermsController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TermsController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTerms()
    {
        var terms = await _context.Terms
            .AsNoTracking()
            .Where(t => t.SchoolId == _currentUser.SchoolId)
            .Include(t => t.AcademicYear)
            .OrderByDescending(t => t.AcademicYear.Year)
            .ThenBy(t => t.TermNumber)
            .Select(t => new
            {
                t.TermId,
                t.TermNumber,
                t.StartDate,
                t.EndDate,
                t.IsCurrent,
                Year = t.AcademicYear.Year
            })
            .ToListAsync();

        return Ok(terms);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentTerm()
    {
        var term = await _context.Terms
            .AsNoTracking()
            .Where(t => t.SchoolId == _currentUser.SchoolId && t.IsCurrent)
            .Include(t => t.AcademicYear)
            .Select(t => new
            {
                t.TermId,
                t.TermNumber,
                t.StartDate,
                t.EndDate,
                t.IsCurrent,
                Year = t.AcademicYear.Year
            })
            .FirstOrDefaultAsync();

        if (term == null) return NotFound();
        return Ok(term);
    }
}
