using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Seeds;
using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Step 7 Layer-3 scope enforcement on real Postgres: query-level scoping + IDOR. Covers
/// SubjectTeacher (own classes / IDOR), oversight (view_all = no filter), Learner (own only),
/// Parent (children only / IDOR), HOD (subject scope via UserPositionScope), and MIC activity
/// ownership (own + unassigned).
/// </summary>
[Collection("Postgres")]
public class ScopeServiceTests
{
    private readonly PostgresFixture _pg;
    public ScopeServiceTests(PostgresFixture pg) => _pg = pg;

    private static readonly Guid School = Guid.NewGuid();
    private static readonly Guid TeacherUser = Guid.NewGuid();
    private static readonly Guid HodUser = Guid.NewGuid();
    private static readonly Guid PrincipalUser = Guid.NewGuid();
    private static readonly Guid LearnerUser = Guid.NewGuid();
    private static readonly Guid ParentUser = Guid.NewGuid();
    private static readonly Guid ChildUser = Guid.NewGuid();
    private static readonly Guid MicUser = Guid.NewGuid();
    private static readonly Guid OtherUser = Guid.NewGuid();

    private static readonly Guid ClassA = Guid.NewGuid();   // teacher T teaches Maths here
    private static readonly Guid ClassB = Guid.NewGuid();   // someone else's class
    private static readonly Guid Maths = Guid.NewGuid();
    private static readonly Guid TeacherRec = Guid.NewGuid();
    private static readonly Guid StudentA = Guid.NewGuid(); // learnerUser, enrolled ClassA
    private static readonly Guid ChildStudent = Guid.NewGuid(); // parentUser's child, enrolled ClassA
    private static readonly Guid StudentB = Guid.NewGuid(); // enrolled ClassB only
    private static readonly Guid ActivityOwned = Guid.NewGuid();
    private static readonly Guid ActivityNull = Guid.NewGuid();
    private static readonly Guid ActivityOther = Guid.NewGuid();

    private static ICurrentUserService User(Guid userId, string identity, bool viewAll)
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(x => x.SchoolId).Returns(School);
        m.Setup(x => x.UserId).Returns(userId);
        m.Setup(x => x.Identity).Returns(identity);
        m.Setup(x => x.HasPermission(PermissionKeys.MarksViewAll)).Returns(viewAll);
        return m.Object;
    }

    private static async Task SeedAsync(SchoolPortalDbContext db)
    {
        await PositionsSeedData.SeedAsync(db, NullLogger.Instance);

        db.Schools.Add(new School { SchoolId = School, Name = "Scope High", IsActive = true, CreatedAt = DateTime.UtcNow });

        User U(Guid id, string email, string role) => new()
        { UserId = id, SchoolId = School, Email = email, PasswordHash = "x", FirstName = "F", LastName = email, Role = role, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(U(TeacherUser, "t", "Teacher"), U(HodUser, "h", "Teacher"), U(PrincipalUser, "p", "Admin"),
            U(LearnerUser, "l", "Student"), U(ParentUser, "pa", "Parent"), U(ChildUser, "ch", "Student"),
            U(MicUser, "m", "Teacher"), U(OtherUser, "o", "Teacher"));

        db.Teachers.Add(new Teacher { TeacherId = TeacherRec, UserId = TeacherUser, SchoolId = School, CreatedAt = DateTime.UtcNow });

        db.Subjects.Add(new Subject { SubjectId = Maths, SchoolId = School, Name = "Maths", Code = "MATH", CreatedAt = DateTime.UtcNow });
        db.Classes.AddRange(
            new Class { ClassId = ClassA, SchoolId = School, Name = "10A", GradeLevel = 10, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassB, SchoolId = School, Name = "10B", GradeLevel = 10, CreatedAt = DateTime.UtcNow });
        db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = Guid.NewGuid(), ClassId = ClassA, SubjectId = Maths, TeacherId = TeacherRec, SchoolId = School, CreatedAt = DateTime.UtcNow });

        Student S(Guid id, Guid userId, string num, Guid? parent = null) => new()
        { StudentId = id, SchoolId = School, UserId = userId, StudentNumber = num, ParentUserId = parent, CreatedAt = DateTime.UtcNow };
        db.Students.AddRange(S(StudentA, LearnerUser, "A1"), S(ChildStudent, ChildUser, "C1", ParentUser), S(StudentB, OtherUser, "B1"));

        Enrollment E(Guid cls, Guid stu) => new() { EnrollmentId = Guid.NewGuid(), ClassId = cls, StudentId = stu, SchoolId = School, IsActive = true };
        db.Enrollments.AddRange(E(ClassA, StudentA), E(ClassA, ChildStudent), E(ClassB, StudentB));

        Activity A(Guid id, Guid? owner) => new()
        { ActivityId = id, SchoolId = School, Name = "Act", ActivityType = "Sport", Date = DateTime.UtcNow, OwnerUserId = owner, CreatedAt = DateTime.UtcNow };
        db.Activities.AddRange(A(ActivityOwned, MicUser), A(ActivityNull, null), A(ActivityOther, OtherUser));

        // HOD position for HodUser, scoped to Maths.
        var hodPositionId = await db.Positions.Where(p => p.Key == "HOD").Select(p => p.PositionId).FirstAsync();
        var up = new UserPosition { UserPositionId = Guid.NewGuid(), SchoolId = School, UserId = HodUser, PositionId = hodPositionId, EffectiveFrom = DateTime.UtcNow.AddDays(-1), IsActive = true, CreatedAt = DateTime.UtcNow };
        db.UserPositions.Add(up);
        db.UserPositionScopes.Add(new UserPositionScope { UserPositionScopeId = Guid.NewGuid(), UserPositionId = up.UserPositionId, ScopeType = ScopeType.Subject, ScopeRefId = Maths });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Scope_EnforcesClassStudentAndActivityBoundaries()
    {
        var (db, source) = await _pg.CreateIsolatedDatabaseAsync();
        try
        {
            await SeedAsync(db);

            // SubjectTeacher → only ClassA; IDOR on ClassB.
            var teacher = new ScopeService(db, User(TeacherUser, IdentityKeys.Staff, viewAll: false));
            var tClasses = await teacher.GetAccessibleClassIdsAsync();
            Assert.NotNull(tClasses);
            Assert.Contains(ClassA, tClasses!);
            Assert.DoesNotContain(ClassB, tClasses!);
            Assert.True(await teacher.CanAccessClassAsync(ClassA));
            Assert.False(await teacher.CanAccessClassAsync(ClassB));       // IDOR
            Assert.True(await teacher.CanAccessStudentAsync(StudentA));    // in ClassA
            Assert.False(await teacher.CanAccessStudentAsync(StudentB));   // ClassB only — IDOR

            // Oversight (marks.view_all) → unrestricted.
            var principal = new ScopeService(db, User(PrincipalUser, IdentityKeys.Staff, viewAll: true));
            Assert.Null(await principal.GetAccessibleClassIdsAsync());
            Assert.True(await principal.CanAccessClassAsync(ClassB));
            Assert.True(await principal.CanAccessStudentAsync(StudentB));

            // Learner → self only; cannot see a classmate (IDOR).
            var learner = new ScopeService(db, User(LearnerUser, IdentityKeys.Learner, viewAll: false));
            var lStudents = await learner.GetAccessibleStudentIdsAsync();
            Assert.Equal(new[] { StudentA }, lStudents!);
            Assert.False(await learner.CanAccessStudentAsync(ChildStudent)); // IDOR

            // Parent → children only; cannot see another learner (IDOR).
            var parent = new ScopeService(db, User(ParentUser, IdentityKeys.Parent, viewAll: false));
            Assert.True(await parent.CanAccessStudentAsync(ChildStudent));
            Assert.False(await parent.CanAccessStudentAsync(StudentA));      // IDOR

            // HOD (subject scope) → classes teaching Maths (ClassA).
            var hod = new ScopeService(db, User(HodUser, IdentityKeys.Staff, viewAll: false));
            Assert.True(await hod.CanAccessClassAsync(ClassA));
            Assert.False(await hod.CanAccessClassAsync(ClassB));

            // MIC activity → owns + unassigned (null); not another's.
            var mic = new ScopeService(db, User(MicUser, IdentityKeys.Staff, viewAll: false));
            Assert.True(await mic.CanAccessActivityAsync(ActivityOwned));
            Assert.True(await mic.CanAccessActivityAsync(ActivityNull));
            Assert.False(await mic.CanAccessActivityAsync(ActivityOther));   // IDOR
            Assert.True(await principal.CanAccessActivityAsync(ActivityOther)); // oversight sees all
        }
        finally { await db.DisposeAsync(); await source.DisposeAsync(); }
    }
}
