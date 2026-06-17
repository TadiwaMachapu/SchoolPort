using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 burn-down — Activities cluster (4 endpoints). No gaps: Step 7's CanAccessActivityAsync plus
/// SchoolId-scoped loads (and the participant lookup resolving the learner by UserId+SchoolId) already
/// block cross-tenant writes; the oversight path still hits the id+SchoolId load → 404. Caller is a
/// Principal (holds activities.manage). Foreign activity/participant → 404, no row mutated.
/// </summary>
[Collection("SecurityApi")]
public class ActivitiesCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public ActivitiesCrossTenantWriteTests(ApiFactory api) => _api = api;

    [CrossTenantGuard(typeof(ActivitiesController), nameof(ActivitiesController.Update))]
    [CrossTenantGuard(typeof(ActivitiesController), nameof(ActivitiesController.Delete))]
    [CrossTenantGuard(typeof(ActivitiesController), nameof(ActivitiesController.AddParticipant))]
    [CrossTenantGuard(typeof(ActivitiesController), nameof(ActivitiesController.RemoveParticipant))]
    [Fact]
    public async Task Update_ForeignActivity_Returns404_AndUnchanged()
    {
        var (principal, foreignActivity) = await PrincipalAndForeign(async (db, b) => Seed.Activity(db, b, "B-Act"));
        var resp = await _api.ClientFor(principal).PutAsJsonAsync($"/api/activities/{foreignActivity}",
            new { name = "Hijacked", activityType = "Sport", date = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("B-Act", await _api.WithScopeAsync(db => db.Activities.Where(a => a.ActivityId == foreignActivity).Select(a => a.Name).SingleAsync()));
    }

    [Fact]
    public async Task Delete_ForeignActivity_Returns404_AndStillExists()
    {
        var (principal, foreignActivity) = await PrincipalAndForeign(async (db, b) => Seed.Activity(db, b));
        var resp = await _api.ClientFor(principal).DeleteAsync($"/api/activities/{foreignActivity}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Activities.AnyAsync(a => a.ActivityId == foreignActivity)));
    }

    [Fact]
    public async Task AddParticipant_ForeignActivity_Returns404_AndNoParticipant()
    {
        var (principal, foreignActivity) = await PrincipalAndForeign(async (db, b) => Seed.Activity(db, b));
        var resp = await _api.ClientFor(principal).PostAsJsonAsync($"/api/activities/{foreignActivity}/participants",
            new { userId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.ActivityParticipants.CountAsync(p => p.ActivityId == foreignActivity)));
    }

    [Fact]
    public async Task RemoveParticipant_ForeignActivity_Returns404_AndStillExists()
    {
        Guid foreignParticipant = Guid.Empty;
        var (principal, foreignActivity) = await PrincipalAndForeign(async (db, b) =>
        {
            var act = Seed.Activity(db, b);
            var stu = Seed.Student(db, b);
            foreignParticipant = Seed.ActivityParticipant(db, b, act, stu);
            return act;
        });
        var resp = await _api.ClientFor(principal).DeleteAsync($"/api/activities/{foreignActivity}/participants/{foreignParticipant}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.ActivityParticipants.AnyAsync(p => p.ActivityParticipantId == foreignParticipant)));
    }

    private async Task<(SeededUser Principal, Guid Foreign)> PrincipalAndForeign(Func<SchoolPortal.Data.SchoolPortalDbContext, Guid, Task<Guid>> seedForeign)
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreign = await _api.WithScopeAsync(async db =>
        {
            var b = Seed.School(db);
            var id = await seedForeign(db, b);
            await db.SaveChangesAsync();
            return id;
        });
        return (principal, foreign);
    }
}
