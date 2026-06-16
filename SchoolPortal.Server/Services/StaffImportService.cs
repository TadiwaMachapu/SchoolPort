using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;

namespace SchoolPortal.Server.Services;

public record StaffImportError(int Row, string Reason);
public record StaffImportResult(int Created, List<StaffImportError> Failed);

public interface IStaffImportService
{
    Task<StaffImportResult> ImportAsync(Stream csv);
    string TemplateCsv();
}

/// <summary>
/// Sprint 1.5.0 Step 9 — bulk staff onboarding with positions + scopes. Header:
/// <c>name,email,identity,positions,scopes</c>. positions = ';'-separated keys; scopes =
/// ';'-separated <c>Position:spec</c> where spec is ','-separated (so the scopes field must be
/// quoted in the CSV). Scope writing matches ScopeService (D3): HOD→Subject(ScopeRefId),
/// GradeHead→Grade(value), PhaseHead→Phase(value); SubjectTeacher/ClassTeacher → operational
/// (ClassSubject.TeacherId / Class.TeacherId). External/System positions are rejected (D5).
/// Row-level errors; a failed row rolls back only itself and import continues.
/// </summary>
public sealed class StaffImportService : IStaffImportService
{
    private readonly SchoolPortalDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StaffImportService(SchoolPortalDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public string TemplateCsv() =>
        "name,email,identity,positions,scopes\n" +
        "Jane Mokoena,jane.mokoena@school.edu,Staff,SubjectTeacher;HOD,\"HOD:Mathematics;SubjectTeacher:10A-Mathematics,11B-Mathematics\"\n" +
        "Sipho Dlamini,sipho.dlamini@school.edu,Staff,GradeHead,GradeHead:10\n";

    public async Task<StaffImportResult> ImportAsync(Stream csv)
    {
        var schoolId = _currentUser.SchoolId;
        var created = 0;
        var failed = new List<StaffImportError>();

        using var reader = new StreamReader(csv);
        var header = await reader.ReadLineAsync();
        if (header is null) return new StaffImportResult(0, new() { new(1, "CSV file is empty.") });

        var cols = SplitCsv(header).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Idx(string name) => cols.IndexOf(name);
        foreach (var required in new[] { "name", "email", "identity", "positions", "scopes" })
            if (Idx(required) < 0) return new StaffImportResult(0, new() { new(1, $"Missing required column: {required}") });

        // Catalogue + school reference data, loaded once.
        var positions = await _db.Positions.AsNoTracking().ToDictionaryAsync(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var subjects = await _db.Subjects.AsNoTracking().Where(s => s.SchoolId == schoolId)
            .ToListAsync();
        var classes = await _db.Classes.AsNoTracking().Where(c => c.SchoolId == schoolId).ToListAsync();
        var classSubjects = await _db.ClassSubjects.AsNoTracking().Where(cs => cs.SchoolId == schoolId).ToListAsync();
        var existingEmails = (await _db.Users.AsNoTracking().Where(u => u.SchoolId == schoolId)
            .Select(u => u.Email).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rowNum = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            rowNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = SplitCsv(line);

            try
            {
                var name = Field(f, Idx("name"));
                var email = Field(f, Idx("email"));
                var identity = Field(f, Idx("identity"));
                var positionsRaw = Field(f, Idx("positions"));
                var scopesRaw = Field(f, Idx("scopes"));

                if (string.IsNullOrWhiteSpace(name)) throw new ImportRowException("Name is required.");
                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ImportRowException("Email is invalid.");
                if (existingEmails.Contains(email)) throw new ImportRowException($"A user with email '{email}' already exists.");
                if (!string.Equals(identity, IdentityKeys.Staff, StringComparison.OrdinalIgnoreCase))
                    throw new ImportRowException("Staff import requires identity 'Staff' (learners/parents/external/system are not imported here).");

                var posKeys = positionsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var scopeMap = ParseScopes(scopesRaw);

                // Validate positions before any write.
                var resolved = new List<Position>();
                foreach (var key in posKeys)
                {
                    if (!positions.TryGetValue(key, out var pos)) throw new ImportRowException($"Unknown position '{key}'.");
                    if (pos.IsExternal || pos.IsSystem) throw new ImportRowException($"'{pos.DisplayName}' is external/system and cannot be assigned via CSV (use the position UI).");
                    resolved.Add(pos);
                }
                // Scoped oversight positions must carry a scope.
                foreach (var pos in resolved)
                {
                    var needsScope = pos.ScopeType is ScopeType.Subject or ScopeType.Grade or ScopeType.Phase;
                    if (needsScope && !scopeMap.ContainsKey(pos.Key))
                        throw new ImportRowException($"'{pos.DisplayName}' requires a scope (e.g. {pos.Key}:{ExampleScope(pos.ScopeType)}).");
                }

                // ── Persist this row (its own transaction) ──
                await using var tx = await _db.Database.BeginTransactionAsync();

                var (first, last) = SplitName(name);
                var role = resolved.Any(p => p.Key is PositionKeys.Principal or PositionKeys.DeputyPrincipal or PositionKeys.ITAdministrator)
                    ? "Admin" : "Teacher";
                var user = new User
                {
                    UserId = Guid.NewGuid(),
                    SchoolId = schoolId,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword($"Temp@{Guid.NewGuid():N}"[..12] + "1"),
                    FirstName = first,
                    LastName = last,
                    Role = role,
                    Identity = IdentityKeys.Staff,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Users.Add(user);

                Teacher? teacher = null;  // created lazily if an operational class link is needed

                foreach (var pos in resolved)
                {
                    var scopeSpecs = scopeMap.TryGetValue(pos.Key, out var s) ? s : new List<string>();
                    var up = new UserPosition
                    {
                        UserPositionId = Guid.NewGuid(),
                        SchoolId = schoolId,
                        UserId = user.UserId,
                        PositionId = pos.PositionId,
                        EffectiveFrom = DateTime.UtcNow,
                        EffectiveTo = null,
                        GrantedByUserId = _currentUser.UserId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        Scopes = BuildScopeRows(pos, scopeSpecs, subjects),
                    };
                    _db.UserPositions.Add(up);

                    // Operational class wiring for teaching positions (D3).
                    if (pos.ScopeType is ScopeType.Class && scopeSpecs.Count > 0)
                    {
                        teacher ??= await EnsureTeacherAsync(user.UserId, schoolId);
                        WireOperationalClasses(pos, scopeSpecs, teacher, classes, classSubjects);
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                existingEmails.Add(email);
                created++;
            }
            catch (ImportRowException ex)
            {
                _db.ChangeTracker.Clear();
                failed.Add(new StaffImportError(rowNum, ex.Message));
            }
            catch (Exception ex)
            {
                _db.ChangeTracker.Clear();
                failed.Add(new StaffImportError(rowNum, ex.Message));
            }
        }

        return new StaffImportResult(created, failed);
    }

    // ── Scope building ───────────────────────────────────────────────────────────

    private List<UserPositionScope> BuildScopeRows(Position pos, List<string> specs, List<Subject> subjects)
    {
        var rows = new List<UserPositionScope>();
        switch (pos.ScopeType)
        {
            case ScopeType.Subject:
                foreach (var name in specs)
                {
                    var subj = subjects.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ImportRowException($"Subject '{name}' not found for {pos.Key} scope.");
                    rows.Add(new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), ScopeType = ScopeType.Subject, ScopeRefId = subj.SubjectId });
                }
                break;
            case ScopeType.Grade:
                foreach (var g in specs)
                {
                    if (!int.TryParse(g, out var n) || n < 1 || n > 12) throw new ImportRowException($"Invalid grade '{g}' for {pos.Key} scope.");
                    rows.Add(new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), ScopeType = ScopeType.Grade, ScopeValue = n.ToString() });
                }
                break;
            case ScopeType.Phase:
                foreach (var p in specs)
                {
                    var phase = p.Replace(" ", "").ToLowerInvariant() switch { "fet" => "FET", "seniorphase" => "SeniorPhase", _ => (string?)null }
                        ?? throw new ImportRowException($"Invalid phase '{p}' for {pos.Key} scope (expected FET or SeniorPhase).");
                    rows.Add(new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), ScopeType = ScopeType.Phase, ScopeValue = phase });
                }
                break;
        }
        return rows;
    }

    private void WireOperationalClasses(Position pos, List<string> specs, Teacher teacher, List<Class> classes, List<ClassSubject> classSubjects)
    {
        foreach (var spec in specs)
        {
            if (pos.Key == PositionKeys.ClassTeacher)
            {
                var cls = classes.FirstOrDefault(c => string.Equals(c.Name, spec, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ImportRowException($"Class '{spec}' not found for ClassTeacher scope.");
                var tracked = _db.Classes.Local.FirstOrDefault(c => c.ClassId == cls.ClassId)
                    ?? _db.Attach(cls).Entity;
                tracked.TeacherId = teacher.TeacherId;
            }
            else // SubjectTeacher / LOTeacher → "Class-Subject"
            {
                var dash = spec.IndexOf('-');
                if (dash <= 0 || dash >= spec.Length - 1) throw new ImportRowException($"'{spec}' must be Class-Subject (e.g. 10A-Mathematics).");
                var className = spec[..dash].Trim();
                var subjectName = spec[(dash + 1)..].Trim();
                var cls = classes.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ImportRowException($"Class '{className}' not found (from '{spec}').");
                var cs = classSubjects.FirstOrDefault(x => x.ClassId == cls.ClassId
                        && string.Equals(SubjectNameFor(x.SubjectId), subjectName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ImportRowException($"Class-subject '{spec}' not found (does {className} offer {subjectName}?).");
                var tracked = _db.ClassSubjects.Local.FirstOrDefault(x => x.ClassSubjectId == cs.ClassSubjectId)
                    ?? _db.Attach(cs).Entity;
                tracked.TeacherId = teacher.TeacherId;
            }
        }
        string? SubjectNameFor(Guid subjectId) => _subjectNameCache.TryGetValue(subjectId, out var n) ? n : null;
    }

    private Dictionary<Guid, string> _subjectNameCache = new();

    private async Task<Teacher> EnsureTeacherAsync(Guid userId, Guid schoolId)
    {
        if (_subjectNameCache.Count == 0)
            _subjectNameCache = await _db.Subjects.AsNoTracking().Where(s => s.SchoolId == schoolId)
                .ToDictionaryAsync(s => s.SubjectId, s => s.Name);

        var existing = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == userId && t.SchoolId == schoolId);
        if (existing is not null) return existing;
        var teacher = new Teacher { TeacherId = Guid.NewGuid(), UserId = userId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow };
        _db.Teachers.Add(teacher);
        return teacher;
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────────

    // "HOD:Mathematics;SubjectTeacher:10A-Maths,11B-Maths" → { HOD:[Mathematics], SubjectTeacher:[10A-Maths,11B-Maths] }
    private static Dictionary<string, List<string>> ParseScopes(string raw)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0) continue;
            var posKey = entry[..colon].Trim();
            var specs = entry[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (specs.Count > 0) map[posKey] = specs;
        }
        return map;
    }

    private static (string First, string Last) SplitName(string name)
    {
        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "");
    }

    private static string ExampleScope(ScopeType t) => t switch
    {
        ScopeType.Subject => "Mathematics", ScopeType.Grade => "10", ScopeType.Phase => "FET", _ => ""
    };

    private static string Field(List<string> fields, int idx) => idx >= 0 && idx < fields.Count ? fields[idx].Trim() : "";

    // Minimal RFC-4180-ish splitter: honours double-quoted fields (so the scopes field may contain commas).
    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }

    private sealed class ImportRowException : Exception
    {
        public ImportRowException(string message) : base(message) { }
    }
}
