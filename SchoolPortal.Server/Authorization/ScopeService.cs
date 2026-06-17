using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Layer-3 scope enforcement (Sprint 1.5.0 Step 7). Resolves the set of resources the current
/// user may see, so services/controllers filter sensitive data AT THE QUERY (not the UI), and
/// id-bearing endpoints can verify a specific resource is in scope (IDOR protection).
///
/// Scope sources (decision S-1):
/// - School-wide oversight (holds <see cref="PermissionKeys.MarksViewAll"/>: Principal, Deputy,
///   Auditor, DistrictOfficial) → unrestricted (null = no filter).
/// - SubjectTeacher / ClassTeacher → OPERATIONAL links (ClassSubject.TeacherId, Class.TeacherId).
/// - HOD / GradeHead → UserPositionScope (Subject / Grade).
/// - Learner → own student; Parent → linked children (Student.ParentUserId).
/// - SportCultureMIC → Activity.OwnerUserId == me OR null (transitional unassigned).
///
/// A null return from the Get* methods means "unrestricted" (school-wide). A non-null set is the
/// exact allow-list; an empty set means the user may see nothing.
/// </summary>
public interface IScopeService
{
    /// <summary>Class ids the user may access for class-level academic data; null = unrestricted.</summary>
    Task<IReadOnlySet<Guid>?> GetAccessibleClassIdsAsync();

    /// <summary>Student ids the user may access; null = unrestricted. Learner = self only,
    /// Parent = children only, staff = students in their accessible classes.</summary>
    Task<IReadOnlySet<Guid>?> GetAccessibleStudentIdsAsync();

    Task<bool> CanAccessClassAsync(Guid classId);
    Task<bool> CanAccessStudentAsync(Guid studentId);
    Task<bool> CanAccessActivityAsync(Guid activityId);

    /// <summary>Throws <see cref="ForbiddenAccessException"/> (→ 403) if the class is out of scope.</summary>
    Task EnsureClassAsync(Guid classId);
    Task EnsureStudentAsync(Guid studentId);
    Task EnsureActivityAsync(Guid activityId);
}

public sealed class ScopeService : IScopeService
{
    private readonly SchoolPortalDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ScopeService(SchoolPortalDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private bool IsSchoolWide => _currentUser.HasPermission(PermissionKeys.MarksViewAll);

    public async Task<IReadOnlySet<Guid>?> GetAccessibleClassIdsAsync()
    {
        if (IsSchoolWide) return null;

        var schoolId = _currentUser.SchoolId;
        var userId = _currentUser.UserId;
        var classIds = new HashSet<Guid>();

        // SubjectTeacher (taught class-subjects) + ClassTeacher (register class) — operational.
        var teacherId = await _db.Teachers.AsNoTracking()
            .Where(t => t.UserId == userId && t.SchoolId == schoolId)
            .Select(t => (Guid?)t.TeacherId).FirstOrDefaultAsync();
        if (teacherId is not null)
        {
            classIds.UnionWith(await _db.ClassSubjects.AsNoTracking()
                .Where(cs => cs.TeacherId == teacherId && cs.SchoolId == schoolId)
                .Select(cs => cs.ClassId).Distinct().ToListAsync());
            classIds.UnionWith(await _db.Classes.AsNoTracking()
                .Where(c => c.TeacherId == teacherId && c.SchoolId == schoolId)
                .Select(c => c.ClassId).ToListAsync());
        }

        // HOD subject scopes → classes teaching those subjects (UserPositionScope).
        var subjectIds = await PositionScopeRefIdsAsync(ScopeType.Subject);
        if (subjectIds.Count > 0)
            classIds.UnionWith(await _db.ClassSubjects.AsNoTracking()
                .Where(cs => cs.SchoolId == schoolId && subjectIds.Contains(cs.SubjectId))
                .Select(cs => cs.ClassId).Distinct().ToListAsync());

        // GradeHead grade scopes + PhaseHead phase scopes → classes at those grade levels
        // (UserPositionScope). Phase expands to its CAPS grades (Step 9 D4): a PhaseHead of FET
        // oversees Gr 10–12; Senior Phase oversees Gr 7–9.
        var grades = (await PositionScopeValuesAsync(ScopeType.Grade))
            .Select(v => int.TryParse(v, out var g) ? (int?)g : null)
            .Where(g => g is not null).Select(g => g!.Value).ToHashSet();
        foreach (var phase in await PositionScopeValuesAsync(ScopeType.Phase))
            grades.UnionWith(GradesForPhase(phase));
        if (grades.Count > 0)
            classIds.UnionWith(await _db.Classes.AsNoTracking()
                .Where(c => c.SchoolId == schoolId && c.GradeLevel != null && grades.Contains(c.GradeLevel.Value))
                .Select(c => c.ClassId).ToListAsync());

        // Learner → own enrolled classes; Parent → children's enrolled classes.
        if (_currentUser.Identity == IdentityKeys.Learner)
        {
            var sid = await _db.Students.AsNoTracking()
                .Where(s => s.UserId == userId && s.SchoolId == schoolId)
                .Select(s => (Guid?)s.StudentId).FirstOrDefaultAsync();
            if (sid is not null)
                classIds.UnionWith(await _db.Enrollments.AsNoTracking()
                    .Where(e => e.StudentId == sid && e.IsActive).Select(e => e.ClassId).ToListAsync());
        }
        else if (_currentUser.Identity == IdentityKeys.Parent)
        {
            var childIds = await _db.Students.AsNoTracking()
                .Where(s => s.ParentUserId == userId && s.SchoolId == schoolId)
                .Select(s => s.StudentId).ToListAsync();
            if (childIds.Count > 0)
                classIds.UnionWith(await _db.Enrollments.AsNoTracking()
                    .Where(e => childIds.Contains(e.StudentId) && e.IsActive).Select(e => e.ClassId).ToListAsync());
        }

        return classIds;
    }

    public async Task<IReadOnlySet<Guid>?> GetAccessibleStudentIdsAsync()
    {
        if (IsSchoolWide) return null;

        var schoolId = _currentUser.SchoolId;
        var userId = _currentUser.UserId;

        // Learner = self only; Parent = children only (never classmates — IDOR).
        if (_currentUser.Identity == IdentityKeys.Learner)
        {
            var sid = await _db.Students.AsNoTracking()
                .Where(s => s.UserId == userId && s.SchoolId == schoolId)
                .Select(s => s.StudentId).FirstOrDefaultAsync();
            return sid == Guid.Empty ? new HashSet<Guid>() : new HashSet<Guid> { sid };
        }
        if (_currentUser.Identity == IdentityKeys.Parent)
            return (await _db.Students.AsNoTracking()
                .Where(s => s.ParentUserId == userId && s.SchoolId == schoolId)
                .Select(s => s.StudentId).ToListAsync()).ToHashSet();

        // Staff = students enrolled in their accessible classes.
        var classIds = await GetAccessibleClassIdsAsync();
        if (classIds is null) return null;
        return (await _db.Enrollments.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.IsActive)
            .Select(e => e.StudentId).Distinct().ToListAsync()).ToHashSet();
    }

    public async Task<bool> CanAccessClassAsync(Guid classId)
    {
        var ids = await GetAccessibleClassIdsAsync();
        return ids is null || ids.Contains(classId);
    }

    public async Task<bool> CanAccessStudentAsync(Guid studentId)
    {
        var ids = await GetAccessibleStudentIdsAsync();
        return ids is null || ids.Contains(studentId);
    }

    public async Task<bool> CanAccessActivityAsync(Guid activityId)
    {
        if (IsSchoolWide) return true;
        var owner = await _db.Activities.AsNoTracking()
            .Where(a => a.ActivityId == activityId && a.SchoolId == _currentUser.SchoolId)
            .Select(a => new { a.OwnerUserId })
            .FirstOrDefaultAsync();
        if (owner is null) return false;                                   // not found / other school
        return owner.OwnerUserId is null || owner.OwnerUserId == _currentUser.UserId;
    }

    public async Task EnsureClassAsync(Guid classId)
    {
        if (!await CanAccessClassAsync(classId)) throw new ForbiddenAccessException();
    }

    public async Task EnsureStudentAsync(Guid studentId)
    {
        if (!await CanAccessStudentAsync(studentId)) throw new ForbiddenAccessException();
    }

    public async Task EnsureActivityAsync(Guid activityId)
    {
        if (!await CanAccessActivityAsync(activityId)) throw new ForbiddenAccessException();
    }

    // CAPS phase → grade levels (Step 9 D4). Accepts "FET"/"SeniorPhase" (and a couple of
    // tolerant spellings); unknown phases expand to nothing (no access rather than over-grant).
    private static IEnumerable<int> GradesForPhase(string phase) =>
        phase.Replace(" ", "").ToLowerInvariant() switch
        {
            "fet" => new[] { 10, 11, 12 },
            "seniorphase" => new[] { 7, 8, 9 },
            _ => Array.Empty<int>(),
        };

    private async Task<HashSet<Guid>> PositionScopeRefIdsAsync(ScopeType type)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.SchoolId;
        return (await _db.UserPositionScopes.AsNoTracking()
            .Where(s => s.ScopeType == type && s.ScopeRefId != null
                     && s.UserPosition.UserId == userId && s.UserPosition.SchoolId == schoolId
                     && s.UserPosition.IsActive && s.UserPosition.EffectiveFrom <= now
                     && (s.UserPosition.EffectiveTo == null || s.UserPosition.EffectiveTo >= now))
            .Select(s => s.ScopeRefId!.Value).Distinct().ToListAsync()).ToHashSet();
    }

    private async Task<List<string>> PositionScopeValuesAsync(ScopeType type)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.SchoolId;
        return await _db.UserPositionScopes.AsNoTracking()
            .Where(s => s.ScopeType == type && s.ScopeValue != null
                     && s.UserPosition.UserId == userId && s.UserPosition.SchoolId == schoolId
                     && s.UserPosition.IsActive && s.UserPosition.EffectiveFrom <= now
                     && (s.UserPosition.EffectiveTo == null || s.UserPosition.EffectiveTo >= now))
            .Select(s => s.ScopeValue!).Distinct().ToListAsync();
    }
}
