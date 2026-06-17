using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Classes;
using SchoolPortal.Shared.DTOs.Subjects;

namespace SchoolPortal.Server.Services;

public interface ISubjectService
{
    Task<List<SubjectDto>> GetSubjectsAsync();
    Task<SubjectDto> GetSubjectByIdAsync(Guid id);
    Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request);
    Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectRequest request);
    Task DeleteSubjectAsync(Guid id);
    Task BulkAssignClassSubjectsAsync(BulkClassSubjectRequest request);
    Task<List<TeacherOptionDto>> GetTeachersAsync();
    Task<CapsSeedException> SeedCapsSubjectsAsync();
}

public record CapsSeedException(int Created, int Skipped);

public class SubjectService : ISubjectService
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SubjectService(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<SubjectDto>> GetSubjectsAsync()
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(s => s.SchoolId == _currentUser.SchoolId)
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto
            {
                SubjectId = s.SubjectId,
                Name = s.Name,
                Code = s.Code,
                Description = s.Description,
                CapsPhase = s.CapsPhase
            })
            .ToListAsync();
    }

    public async Task<SubjectDto> GetSubjectByIdAsync(Guid id)
    {
        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.SchoolId == _currentUser.SchoolId);

        if (subject == null)
            throw new KeyNotFoundException("Subject not found");

        return new SubjectDto
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description,
            CapsPhase = subject.CapsPhase
        };
    }

    public async Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request)
    {
        var subject = new Subject
        {
            SchoolId = _currentUser.SchoolId,
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            CapsPhase = request.CapsPhase,
            CreatedAt = DateTime.UtcNow
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        return new SubjectDto
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description,
            CapsPhase = subject.CapsPhase
        };
    }

    public async Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectRequest request)
    {
        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.SchoolId == _currentUser.SchoolId);

        if (subject == null)
            throw new KeyNotFoundException("Subject not found");

        subject.Name = request.Name;
        subject.Code = request.Code;
        subject.Description = request.Description;
        subject.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new SubjectDto
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Code = subject.Code,
            Description = subject.Description,
            CapsPhase = subject.CapsPhase
        };
    }

    public async Task DeleteSubjectAsync(Guid id)
    {
        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == id && s.SchoolId == _currentUser.SchoolId);

        if (subject == null)
            throw new KeyNotFoundException("Subject not found");

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync();
    }

    public async Task<CapsSeedException> SeedCapsSubjectsAsync()
    {
        var schoolId = _currentUser.SchoolId;

        var existingNames = await _context.Subjects
            .Where(s => s.SchoolId == schoolId)
            .Select(s => s.Name)
            .ToListAsync();
        var existing = existingNames.ToHashSet();

        var capsSubjects = BuildCapsSubjectList();

        var toCreate = capsSubjects
            .Where(s => !existing.Contains(s.Name))
            .Select(s => new Subject
            {
                SchoolId = schoolId,
                Name = s.Name,
                Code = s.Code,
                CapsPhase = s.CapsPhase,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (toCreate.Count > 0)
        {
            _context.Subjects.AddRange(toCreate);
            await _context.SaveChangesAsync();
        }

        return new CapsSeedException(toCreate.Count, capsSubjects.Count - toCreate.Count);
    }

    private static List<(string Name, string? Code, string? CapsPhase)> BuildCapsSubjectList()
    {
        var languages = new[]
        {
            "Afrikaans", "English", "IsiZulu", "IsiXhosa", "Sesotho", "Setswana",
            "Sepedi", "SiSwati", "IsiNdebele", "Tshivenda", "Xitsonga"
        };

        var subjects = new List<(string Name, string? Code, string? CapsPhase)>();

        foreach (var lang in languages)
        {
            subjects.Add(($"{lang} Home Language", null, null));
            subjects.Add(($"{lang} First Additional Language", null, null));
        }

        subjects.AddRange(new (string Name, string? Code, string? CapsPhase)[]
        {
            // Senior Phase (Gr 7–9)
            ("Mathematics", "MATH", "SeniorPhase"),
            ("Natural Sciences", "NSC", "SeniorPhase"),
            ("Social Sciences", "SS", "SeniorPhase"),
            ("Technology", "TECH", "SeniorPhase"),
            ("Economic and Management Sciences", "EMS", "SeniorPhase"),
            ("Life Orientation", "LO", "SeniorPhase"),
            ("Creative Arts", "CA", "SeniorPhase"),
            ("Physical Education", "PE", "SeniorPhase"),

            // FET Phase (Gr 10–12)
            ("Mathematics", "MATH", "FET"),
            ("Mathematical Literacy", "ML", "FET"),
            ("Life Sciences", "LS", "FET"),
            ("Physical Sciences", "PS", "FET"),
            ("Geography", "GEO", "FET"),
            ("History", "HIST", "FET"),
            ("Accounting", "ACC", "FET"),
            ("Business Studies", "BS", "FET"),
            ("Economics", "ECO", "FET"),
            ("Life Orientation", "LO", "FET"),
            ("Visual Arts", "VA", "FET"),
            ("Dramatic Arts", "DA", "FET"),
            ("Music", "MUS", "FET"),
            ("Consumer Studies", "CS", "FET"),
            ("Hospitality Studies", "HS", "FET"),
            ("Tourism", "TRM", "FET"),
            ("Agricultural Sciences", "AGR", "FET"),
            ("Computer Applications Technology", "CAT", "FET"),
            ("Information Technology", "IT", "FET"),
            ("Engineering Graphics and Design", "EGD", "FET"),
            ("Electrical Technology", "ELT", "FET"),
            ("Mechanical Technology", "MT", "FET"),
            ("Civil Technology", "CT", "FET"),
        });

        return subjects;
    }

    public async Task BulkAssignClassSubjectsAsync(BulkClassSubjectRequest request)
    {
        var schoolId = _currentUser.SchoolId;
        var classIds = request.ClassSubjects.Select(cs => cs.ClassId).Distinct().ToList();
        var subjectIds = request.ClassSubjects.Select(cs => cs.SubjectId).Distinct().ToList();
        var teacherIds = request.ClassSubjects
            .Where(cs => cs.TeacherId.HasValue).Select(cs => cs.TeacherId!.Value).Distinct().ToList();

        // Step 9.5 (H1 — tenant isolation): every referenced resource must belong to the caller's
        // school before any mutate/insert. Mirrors the Step 7 IDOR pattern — a cross-tenant id is
        // "not found in your school" (404), never silently mutated or linked. Without this, an
        // academics.manage holder could reassign another school's class-subject or attach a foreign
        // teacher by supplying its GUID (no FK prevents the cross-tenant linkage).
        var validClassIds = (await _context.Classes.AsNoTracking()
            .Where(c => c.SchoolId == schoolId && classIds.Contains(c.ClassId))
            .Select(c => c.ClassId).ToListAsync()).ToHashSet();
        if (classIds.Any(id => !validClassIds.Contains(id)))
            throw new KeyNotFoundException("One or more classes were not found in your school.");

        var validSubjectIds = (await _context.Subjects.AsNoTracking()
            .Where(s => s.SchoolId == schoolId && subjectIds.Contains(s.SubjectId))
            .Select(s => s.SubjectId).ToListAsync()).ToHashSet();
        if (subjectIds.Any(id => !validSubjectIds.Contains(id)))
            throw new KeyNotFoundException("One or more subjects were not found in your school.");

        if (teacherIds.Count > 0)
        {
            var validTeacherIds = (await _context.Teachers.AsNoTracking()
                .Where(t => t.SchoolId == schoolId && teacherIds.Contains(t.TeacherId))
                .Select(t => t.TeacherId).ToListAsync()).ToHashSet();
            if (teacherIds.Any(id => !validTeacherIds.Contains(id)))
                throw new KeyNotFoundException("One or more teachers were not found in your school.");
        }

        var existing = await _context.ClassSubjects
            .Where(cs => cs.SchoolId == schoolId && classIds.Contains(cs.ClassId) && subjectIds.Contains(cs.SubjectId))
            .ToListAsync();

        var existingLookup = existing.ToDictionary(cs => (cs.ClassId, cs.SubjectId));

        foreach (var item in request.ClassSubjects)
        {
            if (existingLookup.TryGetValue((item.ClassId, item.SubjectId), out var existingCs))
            {
                if (item.TeacherId.HasValue)
                    existingCs.TeacherId = item.TeacherId;
            }
            else
            {
                _context.ClassSubjects.Add(new ClassSubject
                {
                    ClassId = item.ClassId,
                    SubjectId = item.SubjectId,
                    TeacherId = item.TeacherId,
                    SchoolId = _currentUser.SchoolId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    // Step 9.5 (Build #6b): the school's teachers as assignable options for class-subject teacher
    // assignment. Returns the Teacher PK (what ClassSubject.TeacherId references), not the User id.
    public async Task<List<TeacherOptionDto>> GetTeachersAsync()
    {
        return await _context.Teachers
            .AsNoTracking()
            .Where(t => t.SchoolId == _currentUser.SchoolId)
            .Include(t => t.User)
            .OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName)
            .Select(t => new TeacherOptionDto
            {
                TeacherId = t.TeacherId,
                Name = $"{t.User.FirstName} {t.User.LastName}"
            })
            .ToListAsync();
    }
}
