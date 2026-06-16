using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Shared.DTOs.Positions;

namespace SchoolPortal.Server.Services;

public interface IPositionService
{
    Task<List<PositionCatalogueItemDto>> GetCatalogueAsync();
    Task<List<PositionOverviewItemDto>> GetOverviewAsync();
    Task<UserPositionsDto> GetUserAssignmentsAsync(Guid userId);
    Task<PositionAssignmentDto> AssignAsync(AssignPositionRequest request);
    Task<PositionAssignmentDto> UpdateAsync(Guid userPositionId, UpdateAssignmentRequest request);
    Task RevokeAsync(Guid userPositionId);
}

/// <summary>
/// Sprint 1.5.0 Step 9 — position assignment/management. Writes UserPosition + UserPositionScope so
/// Step 7's ScopeService filtering activates: HOD→Subject(ScopeRefId), GradeHead→Grade(ScopeValue),
/// PhaseHead→Phase(ScopeValue). Class-scoped teaching positions (SubjectTeacher/ClassTeacher) are
/// scoped OPERATIONALLY (ClassSubject.TeacherId / Class.TeacherId) elsewhere — this service records
/// the appointment but writes no scope rows for them (D3). External/System positions must carry an
/// expiry (and System a consent record) (D5).
/// </summary>
public sealed class PositionService : IPositionService
{
    private readonly SchoolPortalDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PositionService(SchoolPortalDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<PositionCatalogueItemDto>> GetCatalogueAsync()
    {
        var enabled = await EnabledPositionKeysAsync();
        var positions = await _db.Positions.AsNoTracking().ToListAsync();
        return positions
            .OrderBy(p => p.Category).ThenBy(p => p.DisplayName)
            .Select(p => new PositionCatalogueItemDto
            {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Category = p.Category,
                ScopeType = (int)p.ScopeType,
                ScopeTypeName = p.ScopeType.ToString(),
                IsExternal = p.IsExternal,
                IsSystem = p.IsSystem,
                RequiresTimeLimit = p.RequiresTimeLimit,
                RequiresConsent = p.RequiresConsent,
                DefaultDurationHours = p.DefaultDurationHours,
                InPreset = enabled.Contains(p.Key),
            })
            .ToList();
    }

    public async Task<List<PositionOverviewItemDto>> GetOverviewAsync()
    {
        var schoolId = _currentUser.SchoolId;
        var now = DateTime.UtcNow;

        var rows = await _db.UserPositions.AsNoTracking()
            .Where(up => up.SchoolId == schoolId && up.IsActive
                      && up.EffectiveFrom <= now && (up.EffectiveTo == null || up.EffectiveTo >= now))
            .Select(up => new
            {
                up.UserPositionId,
                up.UserId,
                UserName = up.User.FirstName + " " + up.User.LastName,
                PositionKey = up.Position.Key,
                up.Position.DisplayName,
                up.Position.Category,
                up.EffectiveFrom,
                up.EffectiveTo,
                Scopes = up.Scopes.Select(s => new { s.ScopeType, s.ScopeRefId, s.ScopeValue }).ToList(),
            })
            .ToListAsync();

        var labels = await BuildScopeLabelLookupAsync(rows.SelectMany(r => r.Scopes.Select(s => (s.ScopeType, s.ScopeRefId, s.ScopeValue))));

        return rows
            .GroupBy(r => new { r.PositionKey, r.DisplayName, r.Category })
            .OrderBy(g => g.Key.Category).ThenBy(g => g.Key.DisplayName)
            .Select(g => new PositionOverviewItemDto
            {
                PositionKey = g.Key.PositionKey,
                DisplayName = g.Key.DisplayName,
                Category = g.Key.Category,
                Holders = g.OrderBy(h => h.UserName).Select(h => new PositionHolderDto
                {
                    UserPositionId = h.UserPositionId,
                    UserId = h.UserId,
                    UserName = h.UserName,
                    EffectiveFrom = h.EffectiveFrom,
                    EffectiveTo = h.EffectiveTo,
                    Scopes = h.Scopes.Select(s => ToScopeDto(s.ScopeType, s.ScopeRefId, s.ScopeValue, labels)).ToList(),
                }).ToList(),
            })
            .ToList();
    }

    public async Task<UserPositionsDto> GetUserAssignmentsAsync(Guid userId)
    {
        var schoolId = _currentUser.SchoolId;
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId && u.SchoolId == schoolId)
            .Select(u => new { u.UserId, Name = u.FirstName + " " + u.LastName, u.Email, u.Identity })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("User not found");

        var rows = await _db.UserPositions.AsNoTracking()
            .Where(up => up.UserId == userId && up.SchoolId == schoolId)
            .Select(up => new
            {
                up.UserPositionId, PositionKey = up.Position.Key, up.Position.DisplayName,
                up.Position.Category, up.Position.ScopeType, up.EffectiveFrom, up.EffectiveTo, up.IsActive,
                Scopes = up.Scopes.Select(s => new { s.ScopeType, s.ScopeRefId, s.ScopeValue }).ToList(),
            })
            .ToListAsync();

        var labels = await BuildScopeLabelLookupAsync(rows.SelectMany(r => r.Scopes.Select(s => (s.ScopeType, s.ScopeRefId, s.ScopeValue))));

        return new UserPositionsDto
        {
            UserId = user.UserId,
            UserName = user.Name,
            Email = user.Email,
            Identity = user.Identity ?? "",
            Assignments = rows
                .OrderByDescending(r => r.IsActive).ThenBy(r => r.DisplayName)
                .Select(r => new PositionAssignmentDto
                {
                    UserPositionId = r.UserPositionId,
                    PositionKey = r.PositionKey,
                    DisplayName = r.DisplayName,
                    Category = r.Category,
                    ScopeType = (int)r.ScopeType,
                    EffectiveFrom = r.EffectiveFrom,
                    EffectiveTo = r.EffectiveTo,
                    IsActive = r.IsActive,
                    Scopes = r.Scopes.Select(s => ToScopeDto(s.ScopeType, s.ScopeRefId, s.ScopeValue, labels)).ToList(),
                }).ToList(),
        };
    }

    public async Task<PositionAssignmentDto> AssignAsync(AssignPositionRequest request)
    {
        var schoolId = _currentUser.SchoolId;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == request.UserId && u.SchoolId == schoolId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Identity is IdentityKeys.Learner or IdentityKeys.Parent)
            throw new ArgumentException("Positions can only be assigned to staff / external / system identities, not learners or parents.");

        var position = await _db.Positions.FirstOrDefaultAsync(p => p.Key == request.PositionKey)
            ?? throw new ArgumentException($"Unknown position '{request.PositionKey}'.");

        var effectiveFrom = request.EffectiveFrom ?? DateTime.UtcNow;
        var effectiveTo = request.EffectiveTo;

        // D5: External/System (and any time-limited) positions can never be permanent.
        if ((position.IsExternal || position.IsSystem || position.RequiresTimeLimit) && effectiveTo is null)
            throw new ArgumentException($"{position.DisplayName} is time-limited and requires an end date (EffectiveTo).");
        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
            throw new ArgumentException("EffectiveTo must be after EffectiveFrom.");
        if (position.RequiresConsent && request.ConsentRecordId is null)
            throw new ArgumentException($"{position.DisplayName} requires a consent record.");

        // No duplicate active appointment of the same position.
        var dup = await _db.UserPositions.AnyAsync(up =>
            up.UserId == request.UserId && up.SchoolId == schoolId
            && up.PositionId == position.PositionId && up.IsActive);
        if (dup) throw new ArgumentException($"{user.FirstName} {user.LastName} already holds {position.DisplayName}.");

        var scopes = await BuildScopesAsync(position, request.Scopes, schoolId);

        var userPosition = new UserPosition
        {
            UserPositionId = Guid.NewGuid(),
            SchoolId = schoolId,
            UserId = request.UserId,
            PositionId = position.PositionId,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            GrantedByUserId = _currentUser.UserId,
            ConsentRecordId = request.ConsentRecordId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Scopes = scopes,
        };

        _db.UserPositions.Add(userPosition);
        await _db.SaveChangesAsync();

        return await ToAssignmentDtoAsync(userPosition.UserPositionId);
    }

    public async Task<PositionAssignmentDto> UpdateAsync(Guid userPositionId, UpdateAssignmentRequest request)
    {
        var schoolId = _currentUser.SchoolId;
        var up = await _db.UserPositions
            .Include(x => x.Position)
            .Include(x => x.Scopes)
            .FirstOrDefaultAsync(x => x.UserPositionId == userPositionId && x.SchoolId == schoolId)
            ?? throw new KeyNotFoundException("Assignment not found");

        if (request.EffectiveFrom is not null) up.EffectiveFrom = request.EffectiveFrom.Value;
        if (request.EffectiveTo is not null) up.EffectiveTo = request.EffectiveTo;
        if (request.IsActive is not null) up.IsActive = request.IsActive.Value;

        if ((up.Position.IsExternal || up.Position.IsSystem || up.Position.RequiresTimeLimit) && up.EffectiveTo is null)
            throw new ArgumentException($"{up.Position.DisplayName} is time-limited and requires an end date.");
        if (up.EffectiveTo is not null && up.EffectiveTo <= up.EffectiveFrom)
            throw new ArgumentException("EffectiveTo must be after EffectiveFrom.");

        if (request.Scopes is not null)
        {
            _db.UserPositionScopes.RemoveRange(up.Scopes);
            up.Scopes = await BuildScopesAsync(up.Position, request.Scopes, schoolId);
        }

        await _db.SaveChangesAsync();
        return await ToAssignmentDtoAsync(up.UserPositionId);
    }

    public async Task RevokeAsync(Guid userPositionId)
    {
        var schoolId = _currentUser.SchoolId;
        var up = await _db.UserPositions
            .FirstOrDefaultAsync(x => x.UserPositionId == userPositionId && x.SchoolId == schoolId)
            ?? throw new KeyNotFoundException("Assignment not found");

        up.IsActive = false;                 // soft-revoke — preserves the audit trail (UserPosition design)
        up.EffectiveTo ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> EnabledPositionKeysAsync()
    {
        var settings = await _db.Schools.AsNoTracking()
            .Where(s => s.SchoolId == _currentUser.SchoolId)
            .Select(s => s.Settings)
            .FirstOrDefaultAsync();
        return (settings?.EnabledPositionKeys ?? new List<string>()).ToHashSet();
    }

    // Builds the scope rows for a position from request input, validating each against the
    // position's ScopeType. Subject → ScopeRefId; Grade/Phase → ScopeValue. Class/Activity/None →
    // no rows (operational or unscoped); any provided scope input is ignored for those.
    private async Task<List<UserPositionScope>> BuildScopesAsync(Position position, List<ScopeInput> inputs, Guid schoolId)
    {
        var result = new List<UserPositionScope>();
        if (position.ScopeType is ScopeType.Subject)
        {
            var refIds = inputs.Where(i => i.ScopeRefId is not null).Select(i => i.ScopeRefId!.Value).Distinct().ToList();
            if (refIds.Count == 0) throw new ArgumentException($"{position.DisplayName} requires at least one subject scope.");
            var valid = (await _db.Subjects.AsNoTracking()
                .Where(s => s.SchoolId == schoolId && refIds.Contains(s.SubjectId))
                .Select(s => s.SubjectId).ToListAsync()).ToHashSet();
            var missing = refIds.Where(id => !valid.Contains(id)).ToList();
            if (missing.Count > 0) throw new ArgumentException("One or more subject scopes do not exist in this school.");
            result.AddRange(refIds.Select(id => new UserPositionScope
            { UserPositionScopeId = Guid.NewGuid(), ScopeType = ScopeType.Subject, ScopeRefId = id }));
        }
        else if (position.ScopeType is ScopeType.Grade)
        {
            var grades = inputs.Select(i => i.ScopeValue?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Distinct().ToList();
            if (grades.Count == 0) throw new ArgumentException($"{position.DisplayName} requires at least one grade scope.");
            foreach (var g in grades)
                if (!int.TryParse(g, out var n) || n < 1 || n > 12)
                    throw new ArgumentException($"Invalid grade '{g}'. Expected 1–12.");
            result.AddRange(grades.Select(g => new UserPositionScope
            { UserPositionScopeId = Guid.NewGuid(), ScopeType = ScopeType.Grade, ScopeValue = g }));
        }
        else if (position.ScopeType is ScopeType.Phase)
        {
            var phases = inputs.Select(i => NormalizePhase(i.ScopeValue)).Where(v => v is not null).Distinct().ToList();
            if (phases.Count == 0) throw new ArgumentException($"{position.DisplayName} requires at least one phase scope (FET or SeniorPhase).");
            result.AddRange(phases.Select(p => new UserPositionScope
            { UserPositionScopeId = Guid.NewGuid(), ScopeType = ScopeType.Phase, ScopeValue = p }));
        }
        // ScopeType.Class / Activity / None: appointment carries no UserPositionScope rows here.
        return result;
    }

    private static string? NormalizePhase(string? value) =>
        value?.Replace(" ", "").ToLowerInvariant() switch
        {
            "fet" => "FET",
            "seniorphase" => "SeniorPhase",
            _ => null,
        };

    private async Task<PositionAssignmentDto> ToAssignmentDtoAsync(Guid userPositionId)
    {
        var r = await _db.UserPositions.AsNoTracking()
            .Where(up => up.UserPositionId == userPositionId)
            .Select(up => new
            {
                up.UserPositionId, PositionKey = up.Position.Key, up.Position.DisplayName,
                up.Position.Category, up.Position.ScopeType, up.EffectiveFrom, up.EffectiveTo, up.IsActive,
                Scopes = up.Scopes.Select(s => new { s.ScopeType, s.ScopeRefId, s.ScopeValue }).ToList(),
            })
            .FirstAsync();
        var labels = await BuildScopeLabelLookupAsync(r.Scopes.Select(s => (s.ScopeType, s.ScopeRefId, s.ScopeValue)));
        return new PositionAssignmentDto
        {
            UserPositionId = r.UserPositionId,
            PositionKey = r.PositionKey,
            DisplayName = r.DisplayName,
            Category = r.Category,
            ScopeType = (int)r.ScopeType,
            EffectiveFrom = r.EffectiveFrom,
            EffectiveTo = r.EffectiveTo,
            IsActive = r.IsActive,
            Scopes = r.Scopes.Select(s => ToScopeDto(s.ScopeType, s.ScopeRefId, s.ScopeValue, labels)).ToList(),
        };
    }

    // Resolves Subject scope ref-ids to subject names (one batched query) for display labels.
    private async Task<Dictionary<Guid, string>> BuildScopeLabelLookupAsync(
        IEnumerable<(ScopeType type, Guid? refId, string? value)> scopes)
    {
        var subjectIds = scopes.Where(s => s.type == ScopeType.Subject && s.refId is not null)
            .Select(s => s.refId!.Value).Distinct().ToList();
        if (subjectIds.Count == 0) return new();
        return await _db.Subjects.AsNoTracking()
            .Where(s => subjectIds.Contains(s.SubjectId))
            .ToDictionaryAsync(s => s.SubjectId, s => s.Name);
    }

    private static ScopeDto ToScopeDto(ScopeType type, Guid? refId, string? value, Dictionary<Guid, string> subjectNames)
    {
        var label = type switch
        {
            ScopeType.Subject => refId is not null && subjectNames.TryGetValue(refId.Value, out var n) ? n : "Subject",
            ScopeType.Grade => $"Grade {value}",
            ScopeType.Phase => value ?? "Phase",
            _ => type.ToString(),
        };
        return new ScopeDto { ScopeType = (int)type, ScopeRefId = refId, ScopeValue = value, Label = label };
    }
}
