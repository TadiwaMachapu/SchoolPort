using System.Text;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Services;

/// <summary>
/// Sprint 1.5.0 backfill: maps existing legacy-Role users onto the new Identity + Position
/// model. <see cref="BuildPlanAsync"/> is pure-read (the DRY RUN); <see cref="ApplyAsync"/>
/// performs the writes and is idempotent + transactional. The legacy User.Role is never
/// modified — Identity is added alongside it.
///
/// Mapping: Admin → Staff + Principal · Teacher → Staff + SubjectTeacher (scoped to the
/// classes they teach, inferred from ClassSubject.TeacherId) · Student → Learner ·
/// Parent → Parent.
/// </summary>
public class IdentityBackfillService
{
    private readonly SchoolPortalDbContext _db;
    private readonly ILogger<IdentityBackfillService> _logger;

    public IdentityBackfillService(SchoolPortalDbContext db, ILogger<IdentityBackfillService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public record ScopePlan(ScopeType ScopeType, Guid? ScopeRefId, string? ScopeValue, string Label);
    public record PositionPlan(string PositionKey, IReadOnlyList<ScopePlan> Scopes);
    public record UserPlan(
        Guid UserId, Guid SchoolId, string Name, string Email,
        string OldRole, string? NewIdentity,
        IReadOnlyList<PositionPlan> Positions, IReadOnlyList<string> Flags);
    public record BackfillPlan(IReadOnlyList<UserPlan> Users);

    /// <summary>Pure-read DRY RUN: computes the mapping for every user. No writes.</summary>
    public async Task<BackfillPlan> BuildPlanAsync()
    {
        // Project only the columns the plan needs — NOT the full entity. This lets the
        // read-only dry run execute before the migration adds users.identity (selecting the
        // whole entity would emit `u.identity` and fail on a pre-migration database).
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.SchoolId).ThenBy(u => u.Role).ThenBy(u => u.LastName)
            .Select(u => new { u.UserId, u.SchoolId, u.FirstName, u.LastName, u.Email, u.Role })
            .ToListAsync();

        // teacherId -> class ids they teach (for SubjectTeacher scope inference)
        var teachers = await _db.Teachers.AsNoTracking()
            .Select(t => new { t.TeacherId, t.UserId })
            .ToListAsync();
        var teacherByUser = teachers.ToDictionary(t => t.UserId, t => t.TeacherId);

        var classByTeacher = (await _db.ClassSubjects.AsNoTracking()
                .Where(cs => cs.TeacherId != null)
                .Select(cs => new { cs.TeacherId, cs.ClassId })
                .ToListAsync())
            .GroupBy(x => x.TeacherId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ClassId).Distinct().ToList());

        var classNames = await _db.Classes.AsNoTracking()
            .Select(c => new { c.ClassId, c.Name }).ToDictionaryAsync(c => c.ClassId, c => c.Name);

        var adminCountBySchool = users.Where(u => u.Role == "Admin")
            .GroupBy(u => u.SchoolId).ToDictionary(g => g.Key, g => g.Count());

        var plans = new List<UserPlan>();
        foreach (var u in users)
        {
            var flags = new List<string>();
            string? identity = null;
            var positions = new List<PositionPlan>();

            switch (u.Role)
            {
                case "Admin":
                    identity = "Staff";
                    positions.Add(new PositionPlan("Principal", Array.Empty<ScopePlan>()));
                    if (adminCountBySchool.GetValueOrDefault(u.SchoolId) > 1)
                        flags.Add("Multiple Admins in this school all map to Principal — review (consider DeputyPrincipal).");
                    break;

                case "Teacher":
                    identity = "Staff";
                    var scopes = new List<ScopePlan>();
                    if (teacherByUser.TryGetValue(u.UserId, out var tId) &&
                        classByTeacher.TryGetValue(tId, out var classIds))
                    {
                        foreach (var cid in classIds)
                            scopes.Add(new ScopePlan(ScopeType.Class, cid, null,
                                classNames.GetValueOrDefault(cid, cid.ToString())));
                    }
                    if (scopes.Count == 0)
                        flags.Add("Teacher has no class assignments — SubjectTeacher will be granted with no scope (grants nothing until scoped).");
                    positions.Add(new PositionPlan("SubjectTeacher", scopes));
                    break;

                case "Student":
                    identity = "Learner";
                    break;

                case "Parent":
                    identity = "Parent";
                    break;

                default:
                    flags.Add($"Unrecognised legacy role '{u.Role}' — no identity assigned; needs manual review.");
                    break;
            }

            plans.Add(new UserPlan(
                u.UserId, u.SchoolId, $"{u.FirstName} {u.LastName}", u.Email,
                u.Role, identity, positions, flags));
        }

        return new BackfillPlan(plans);
    }

    /// <summary>Renders the dry-run plan as a reviewable markdown report.</summary>
    public string RenderMarkdown(BackfillPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Sprint 1.5.0 — Identity/Position Backfill DRY RUN");
        sb.AppendLine($"_Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · {plan.Users.Count} users · NO CHANGES WRITTEN_\n");

        var byIdentity = plan.Users.GroupBy(u => u.NewIdentity ?? "(none)")
            .OrderBy(g => g.Key);
        sb.AppendLine("## Summary");
        sb.AppendLine("| New identity | Users |");
        sb.AppendLine("|---|---|");
        foreach (var g in byIdentity) sb.AppendLine($"| {g.Key} | {g.Count()} |");
        var flagged = plan.Users.Where(u => u.Flags.Count > 0).ToList();
        sb.AppendLine($"\n**Flagged for review:** {flagged.Count}\n");

        foreach (var school in plan.Users.GroupBy(u => u.SchoolId))
        {
            sb.AppendLine($"## School {school.Key}");
            sb.AppendLine("| Email | Old role | New identity | Positions (scopes) | Flags |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var u in school.OrderBy(x => x.OldRole).ThenBy(x => x.Email))
            {
                var pos = u.Positions.Count == 0
                    ? "—"
                    : string.Join("; ", u.Positions.Select(p =>
                        p.Scopes.Count == 0 ? p.PositionKey
                        : $"{p.PositionKey} [{string.Join(", ", p.Scopes.Select(s => s.Label))}]"));
                var flags = u.Flags.Count == 0 ? "" : "⚠ " + string.Join(" ", u.Flags);
                sb.AppendLine($"| {u.Email} | {u.OldRole} | {u.NewIdentity ?? "(none)"} | {pos} | {flags} |");
            }
            sb.AppendLine();
        }

        if (flagged.Count > 0)
        {
            sb.AppendLine("## ⚠ Review these before applying");
            foreach (var u in flagged)
                foreach (var f in u.Flags)
                    sb.AppendLine($"- **{u.Email}** ({u.OldRole}): {f}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Applies the plan: sets User.Identity and creates UserPosition + UserPositionScope rows.
    /// Idempotent (skips users already backfilled / positions already present) and transactional.
    /// Run ONLY after the dry-run report has been reviewed and approved.
    /// </summary>
    public async Task<int> ApplyAsync()
    {
        var plan = await BuildPlanAsync();
        var positionIdByKey = await _db.Positions.AsNoTracking()
            .ToDictionaryAsync(p => p.Key, p => p.PositionId);

        await using var tx = await _db.Database.BeginTransactionAsync();
        var changed = 0;

        foreach (var up in plan.Users)
        {
            if (up.NewIdentity is null) continue; // unrecognised role — skip, already flagged

            var user = await _db.Users.FirstAsync(x => x.UserId == up.UserId);
            if (user.Identity is null)
            {
                user.Identity = up.NewIdentity;
                changed++;
            }

            foreach (var p in up.Positions)
            {
                var positionId = positionIdByKey[p.PositionKey];
                var exists = await _db.UserPositions.AnyAsync(x =>
                    x.UserId == up.UserId && x.PositionId == positionId && x.SchoolId == up.SchoolId);
                if (exists) continue;

                var assignment = new UserPosition
                {
                    SchoolId = up.SchoolId,
                    UserId = up.UserId,
                    PositionId = positionId,
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveTo = null,
                    GrantedByUserId = null,           // system backfill
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Scopes = p.Scopes.Select(s => new UserPositionScope
                    {
                        ScopeType = s.ScopeType,
                        ScopeRefId = s.ScopeRefId,
                        ScopeValue = s.ScopeValue
                    }).ToList()
                };
                _db.UserPositions.Add(assignment);
                changed++;
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        _logger.LogInformation("Identity backfill applied: {Changed} changes across {Users} users.",
            changed, plan.Users.Count);
        return changed;
    }

    public record VerifyResult(
        int TotalUsers, int RolePopulated, int IdentitySet,
        IReadOnlyDictionary<string, int> IdentityBreakdown,
        int UserPositions, IReadOnlyDictionary<string, int> PositionBreakdown);

    /// <summary>Read-only after-state check: confirms Role is still populated (fallback intact)
    /// and reports the identity + position rows that were written.</summary>
    public async Task<VerifyResult> VerifyAsync()
    {
        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Role, u.Identity }).ToListAsync();

        var positionKeys = await _db.UserPositions.AsNoTracking()
            .Join(_db.Positions, up => up.PositionId, p => p.PositionId, (up, p) => p.Key)
            .ToListAsync();

        return new VerifyResult(
            TotalUsers: users.Count,
            RolePopulated: users.Count(u => !string.IsNullOrEmpty(u.Role)),
            IdentitySet: users.Count(u => !string.IsNullOrEmpty(u.Identity)),
            IdentityBreakdown: users.Where(u => !string.IsNullOrEmpty(u.Identity))
                .GroupBy(u => u.Identity!).ToDictionary(g => g.Key, g => g.Count()),
            UserPositions: positionKeys.Count,
            PositionBreakdown: positionKeys.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count()));
    }
}
