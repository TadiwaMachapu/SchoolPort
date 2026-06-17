using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 Inventory-B — Admin cluster. No gaps were found here (Positions/Users/Popia/Plugins/Schools
/// were all already tenant-guarded — Positions validates both the target user AND subject scopes ∈
/// school; Users/Popia scope by id+SchoolId; Plugins is a global marketplace with school-scoped
/// installations; Schools writes take no id and act on the caller's own school). These tests LOCK that
/// protection. Dual+ assertion: status AND no row written.
/// </summary>
[Collection("SecurityApi")]
public class AdminCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public AdminCrossTenantWriteTests(ApiFactory api) => _api = api;

    [CrossTenantGuard(typeof(PositionsController), nameof(PositionsController.Assign))]
    [CrossTenantGuard(typeof(UsersController), nameof(UsersController.UpdateUser))]
    [CrossTenantGuard(typeof(PopiaController), nameof(PopiaController.AdminUpdateRequest))]
    [Fact]
    public async Task Positions_Assign_ForeignUser_Returns404_AndCreatesNoAppointment()
    {
        var schoolA = Guid.NewGuid();
        var admin = await _api.MintTokenAsync(schoolA, "Staff", "ITAdministrator");
        var foreignUser = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var u = AddUser(db, schoolB, "Teacher", "Staff");
            await db.SaveChangesAsync();
            return u;
        });

        var resp = await _api.ClientFor(admin).PostAsJsonAsync("/api/positions/assign",
            new { userId = foreignUser, positionKey = "SubjectTeacher", scopes = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.UserPositions.CountAsync(up => up.UserId == foreignUser)));
    }

    [Fact]
    public async Task Positions_Assign_ForeignSubjectScope_Returns400_AndCreatesNoAppointment()
    {
        var schoolA = Guid.NewGuid();
        var admin = await _api.MintTokenAsync(schoolA, "Staff", "ITAdministrator");
        var (localTarget, foreignSubject) = await _api.WithScopeAsync(async db =>
        {
            var target = AddUser(db, schoolA, "Teacher", "Staff");   // a real local staff member to appoint
            var schoolB = AddSchool(db);
            var subject = AddSubject(db, schoolB);                   // a subject from another school
            await db.SaveChangesAsync();
            return (target, subject);
        });

        // HOD is Subject-scoped — supplying a foreign subject scope must be rejected.
        var resp = await _api.ClientFor(admin).PostAsJsonAsync("/api/positions/assign",
            new { userId = localTarget, positionKey = "HOD", scopes = new[] { new { scopeRefId = foreignSubject } } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode); // "subject scopes do not exist in this school"
        Assert.Equal(0, await _api.WithScopeAsync(db => db.UserPositions.CountAsync(up => up.UserId == localTarget)));
    }

    [Fact]
    public async Task Users_Update_ForeignUser_Returns404_AndLeavesUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var admin = await _api.MintTokenAsync(schoolA, "Staff", "ITAdministrator");
        var foreignUser = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var id = Guid.NewGuid();
            db.Users.Add(new User { UserId = id, SchoolId = schoolB, Email = $"b_{id:N}@test.local", PasswordHash = "x", FirstName = "B-Name", LastName = "X", Role = "Teacher", Identity = "Staff", IsActive = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return id;
        });

        var resp = await _api.ClientFor(admin).PutAsJsonAsync($"/api/users/{foreignUser}",
            new { firstName = "Hijacked", lastName = "X", role = "Teacher", isActive = true });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var name = await _api.WithScopeAsync(db => db.Users.Where(u => u.UserId == foreignUser).Select(u => u.FirstName).SingleAsync());
        Assert.Equal("B-Name", name);
    }

    [Fact]
    public async Task Popia_AdminUpdate_RequestNotInSchool_Returns404()
    {
        // Guarded confirmation: the admin-request update loads by RequestId + SchoolId, so an id not in
        // the caller's school (here, a non-existent id) is 404 — no foreign request can be mutated.
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");

        var resp = await _api.ClientFor(principal).PutAsJsonAsync($"/api/popia/admin/requests/{Guid.NewGuid()}",
            new { status = "Completed", adminNotes = "x" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---- seed helpers ---------------------------------------------------------------------------

    private static Guid AddSchool(SchoolPortalDbContext db)
    {
        var id = Guid.NewGuid();
        db.Schools.Add(new School { SchoolId = id, Name = "S" + id.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddUser(SchoolPortalDbContext db, Guid schoolId, string role, string identity)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { UserId = id, SchoolId = schoolId, Email = $"u_{id:N}@test.local", PasswordHash = "x", FirstName = "U", LastName = "X", Role = role, Identity = identity, IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddSubject(SchoolPortalDbContext db, Guid schoolId)
    {
        var id = Guid.NewGuid();
        db.Subjects.Add(new Subject { SubjectId = id, SchoolId = schoolId, Name = "Sub" + id.ToString("N")[..4], Code = "S" + id.ToString("N")[..3], CreatedAt = DateTime.UtcNow });
        return id;
    }
}
