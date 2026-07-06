using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Authorization;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(SchoolPortalDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Sprint 1.5.0.5 — manual refresh of the analytics/reporting materialized views. Sensitive
    // (recomputes over ALL school data → DB-resolved per request). Refreshed CONCURRENTLY so reads
    // aren't blocked; each view's UNIQUE index makes CONCURRENTLY possible. Intended for natural
    // moments (end of term, before report generation), NOT on every grade save — bulk mark capture
    // would otherwise thrash a full refresh per row. Sprint 1.5.3 adds a debounced background
    // refresh (see CLAUDE.md).
    [HttpPost("refresh-views")]
    [RequirePermission(PermissionKeys.SystemRefreshViews)]
    public async Task<IActionResult> RefreshViews()
    {
        // Fixed, in-code list (no user input) — safe to concatenate into the DDL. CONCURRENTLY must
        // not run inside a transaction; ExecuteSqlRawAsync issues each statement in autocommit.
        var views = new[]
        {
            "vw_subject_term_averages",
            "vw_matric_aps_summary",
            "vw_school_performance_summary",
        };

        var started = DateTime.UtcNow;
#pragma warning disable EF1003 // EF 10 analyzer: concatenated SQL — safe here, fixed in-code view list (no user input)
        foreach (var view in views)
            await _context.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY " + view + ";");
#pragma warning restore EF1003

        var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
        _logger.LogInformation("Refreshed {Count} materialized views in {Ms}ms.", views.Length, elapsedMs);
        return Ok(new { refreshed = views, elapsedMs });
    }
}
