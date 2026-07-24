using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;
using SchoolPortal.Shared.DTOs.Schools;
using Xunit;

namespace SchoolPortal.Tests.Integration;

/// <summary>
/// Sprint 1.6.x — SuperAdminAuditLog. Every mutating SuperAdmin action writes exactly one audit
/// row with real before/after values inside the mutation's SaveChanges (atomic); reads and no-ops
/// write nothing; the read endpoint paginates/filters and joins the actor + school. Service-level
/// tests on the real-Postgres fixture (isolated DB per test).
/// </summary>
[Collection("Postgres")]
public class SuperAdminAuditLogTests
{
    private readonly PostgresFixture _fixture;
    public SuperAdminAuditLogTests(PostgresFixture fixture) => _fixture = fixture;

    // ── Harness ────────────────────────────────────────────────────
    private static SuperAdminService BuildService(SchoolPortalDbContext ctx, Guid actorSuperAdminId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actorSuperAdminId.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var config = new ConfigurationBuilder().Build();
        return new SuperAdminService(ctx, config, Mock.Of<IAuthService>(), http);
    }

    private static async Task<Guid> SeedSuperAdminAsync(SchoolPortalDbContext ctx, string first = "Grace", string last = "Mokoena")
    {
        var id = Guid.NewGuid();
        ctx.SuperAdmins.Add(new SuperAdmin
        {
            SuperAdminId = id,
            Email = $"super-{id:N}@platform.dev",
            PasswordHash = "x",
            FirstName = first,
            LastName = last,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedSchoolAsync(SchoolPortalDbContext ctx, string name = "Test School", bool active = true, SchoolFeatures? features = null)
    {
        var id = Guid.NewGuid();
        ctx.Schools.Add(new School
        {
            SchoolId = id,
            Name = name,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            Features = features ?? new SchoolFeatures(),
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    // Full 12-flag request that mirrors a SchoolFeatures snapshot (so tests flip exactly one).
    private static UpdateSchoolFeaturesRequest RequestFrom(SchoolFeatures f) => new()
    {
        Gradebook = f.Gradebook,
        VirtualClassroom = f.VirtualClassroom,
        SmartReports = f.SmartReports,
        SaSamsExport = f.SaSamsExport,
        SkillsProfile = f.SkillsProfile,
        Pathways = f.Pathways,
        MatricHub = f.MatricHub,
        SportsCulture = f.SportsCulture,
        SchoolPay = f.SchoolPay,
        SchoolChat = f.SchoolChat,
        WhatsApp = f.WhatsApp,
        PopiaCentre = f.PopiaCentre,
    };

    // ── Group 1: each mutation writes exactly one row with correct values ──

    [Fact]
    public async Task CreateSchool_WritesOneAuditRow_WithCreatedAction()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var svc = BuildService(ctx, actor);

        var dto = await svc.CreateSchoolAsync(new CreateSchoolRequest
        {
            Name = "Riverside High",
            Domain = "riverside.edu",
            AdminEmail = "admin@riverside.edu",
            AdminPassword = "Admin@1234!",
            AdminFirstName = "Ada", AdminLastName = "Zulu",
        });

        var rows = await ctx.SuperAdminAuditLogs.ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(SuperAdminAuditActions.SchoolCreated, row.ActionType);
        Assert.Equal(actor, row.SuperAdminId);
        Assert.Equal(dto.SchoolId, row.TargetSchoolId);
        Assert.Null(row.PreviousValue);
        using var doc = JsonDocument.Parse(row.NewValue!);
        Assert.Equal("Riverside High", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("admin@riverside.edu", doc.RootElement.GetProperty("adminEmail").GetString());
    }

    [Fact]
    public async Task UpdateFeatures_WritesOneAuditRow_WithOnlyChangedFlags()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var schoolId = await SeedSchoolAsync(ctx, features: new SchoolFeatures { Gradebook = true, VirtualClassroom = false });
        var svc = BuildService(ctx, actor);

        // Flip only VirtualClassroom false → true; keep the other 11 as-is.
        var req = RequestFrom(new SchoolFeatures { Gradebook = true, VirtualClassroom = true });
        await svc.UpdateFeaturesAsync(schoolId, req);

        var row = Assert.Single(await ctx.SuperAdminAuditLogs.ToListAsync());
        Assert.Equal(SuperAdminAuditActions.SchoolFeaturesUpdated, row.ActionType);
        var prev = JsonSerializer.Deserialize<Dictionary<string, bool>>(row.PreviousValue!)!;
        var next = JsonSerializer.Deserialize<Dictionary<string, bool>>(row.NewValue!)!;
        Assert.Equal(new[] { "virtualClassroom" }, prev.Keys.ToArray());   // ONLY the changed flag
        Assert.False(prev["virtualClassroom"]);
        Assert.True(next["virtualClassroom"]);
        Assert.Single(next);
    }

    [Fact]
    public async Task SetStatus_WritesOneAuditRow_WithReason()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var schoolId = await SeedSchoolAsync(ctx, active: true);
        var svc = BuildService(ctx, actor);

        await svc.SetStatusAsync(schoolId, isActive: false, reason: "Non-payment of platform fees");

        var row = Assert.Single(await ctx.SuperAdminAuditLogs.ToListAsync());
        Assert.Equal(SuperAdminAuditActions.SchoolStatusChanged, row.ActionType);
        Assert.Equal("Non-payment of platform fees", row.Reason);
        Assert.True(JsonSerializer.Deserialize<Dictionary<string, bool>>(row.PreviousValue!)!["isActive"]);
        Assert.False(JsonSerializer.Deserialize<Dictionary<string, bool>>(row.NewValue!)!["isActive"]);
    }

    // ── Group 2: no-ops and reads write nothing ──

    [Fact]
    public async Task UpdateFeatures_NoActualChange_WritesNoAuditRow()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var current = new SchoolFeatures { Gradebook = true, MatricHub = true };
        var schoolId = await SeedSchoolAsync(ctx, features: current);
        var svc = BuildService(ctx, actor);

        await svc.UpdateFeaturesAsync(schoolId, RequestFrom(current));   // identical → no-op

        Assert.Equal(0, await ctx.SuperAdminAuditLogs.CountAsync());
    }

    [Fact]
    public async Task SetStatus_SameStatus_WritesNoAuditRow()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var schoolId = await SeedSchoolAsync(ctx, active: true);
        var svc = BuildService(ctx, actor);

        await svc.SetStatusAsync(schoolId, isActive: true, reason: null);   // already active → no-op

        Assert.Equal(0, await ctx.SuperAdminAuditLogs.CountAsync());
    }

    [Fact]
    public async Task ReadOnlyActions_WriteNoAuditRows()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        await SeedSuperAdminAsync(ctx);
        await SeedSchoolAsync(ctx);
        var svc = BuildService(ctx, Guid.NewGuid());

        await svc.GetAllSchoolsAsync();
        await svc.GetStatsAsync();
        await svc.GetAuditLogAsync(null, null, null, null, 1, 50);

        Assert.Equal(0, await ctx.SuperAdminAuditLogs.CountAsync());
    }

    // ── Group 3: audit + effect are one atomic transaction ──

    [Fact]
    public async Task CreateSchool_UserCreationFails_SchoolCreationRollsBackToo()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var svc = BuildService(ctx, actor);

        // Valid school, but an admin email that overflows users.email varchar(255) → the USER insert
        // fails inside the single SaveChanges. Old behaviour (two saves) left an orphan school.
        var overlongEmail = new string('a', 300) + "@x.edu";
        await Assert.ThrowsAnyAsync<Exception>(() => svc.CreateSchoolAsync(new CreateSchoolRequest
        {
            Name = "Rollback High",
            AdminEmail = overlongEmail,
            AdminPassword = "Admin@1234!",
            AdminFirstName = "A", AdminLastName = "B",
        }));

        ctx.ChangeTracker.Clear();
        Assert.Equal(0, await ctx.Schools.CountAsync(s => s.Name == "Rollback High"));   // no orphan school
        Assert.Equal(0, await ctx.Users.CountAsync());
        Assert.Equal(0, await ctx.SuperAdminAuditLogs.CountAsync());                      // no audit row either
    }

    [Fact]
    public async Task Mutation_WhenAuditActorMissing_RollsBackTheMutation()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        // Deliberately DO NOT seed the super admin → the audit row's super_admin_id FK will fail,
        // which must roll back the status change too (proving they share one transaction).
        var schoolId = await SeedSchoolAsync(ctx, active: true);
        var svc = BuildService(ctx, actorSuperAdminId: Guid.NewGuid());

        await Assert.ThrowsAnyAsync<Exception>(() => svc.SetStatusAsync(schoolId, isActive: false, reason: null));

        ctx.ChangeTracker.Clear();
        Assert.True((await ctx.Schools.FirstAsync(s => s.SchoolId == schoolId)).IsActive);   // status unchanged
        Assert.Equal(0, await ctx.SuperAdminAuditLogs.CountAsync());
    }

    // ── Group 4: pagination, filtering, and the actor/school join ──

    [Fact]
    public async Task GetAuditLog_Paginates_MostRecentFirst()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx);
        var schoolId = await SeedSchoolAsync(ctx);
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
            ctx.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
            {
                AuditId = Guid.NewGuid(), SuperAdminId = actor, TargetSchoolId = schoolId,
                ActionType = SuperAdminAuditActions.SchoolStatusChanged, CreatedAt = baseTime.AddMinutes(i),
            });
        await ctx.SaveChangesAsync();
        var svc = BuildService(ctx, actor);

        var page1 = await svc.GetAuditLogAsync(null, null, null, null, page: 1, pageSize: 2);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(baseTime.AddMinutes(4), page1.Items[0].CreatedAt);   // most recent first
        Assert.True(page1.Items[0].CreatedAt > page1.Items[1].CreatedAt);

        var page3 = await svc.GetAuditLogAsync(null, null, null, null, page: 3, pageSize: 2);
        Assert.Single(page3.Items);   // 5 rows, pageSize 2 → last page has 1
    }

    [Fact]
    public async Task GetAuditLog_FiltersBySchoolActionAndDate_AndJoinsActorAndSchool()
    {
        var (ctx, source) = await _fixture.CreateIsolatedDatabaseAsync();
        await using var _c = ctx; await using var _s = source;
        var actor = await SeedSuperAdminAsync(ctx, first: "Grace", last: "Mokoena");
        var schoolA = await SeedSchoolAsync(ctx, name: "Alpha College");
        var schoolB = await SeedSchoolAsync(ctx, name: "Beta Academy");
        var t = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        ctx.SuperAdminAuditLogs.AddRange(
            new SuperAdminAuditLog { AuditId = Guid.NewGuid(), SuperAdminId = actor, TargetSchoolId = schoolA, ActionType = SuperAdminAuditActions.SchoolStatusChanged,   CreatedAt = t },
            new SuperAdminAuditLog { AuditId = Guid.NewGuid(), SuperAdminId = actor, TargetSchoolId = schoolA, ActionType = SuperAdminAuditActions.SchoolFeaturesUpdated, CreatedAt = t.AddDays(10) },
            new SuperAdminAuditLog { AuditId = Guid.NewGuid(), SuperAdminId = actor, TargetSchoolId = schoolB, ActionType = SuperAdminAuditActions.SchoolStatusChanged,   CreatedAt = t.AddDays(20) });
        await ctx.SaveChangesAsync();
        var svc = BuildService(ctx, actor);

        // Filter by school
        var bySchool = await svc.GetAuditLogAsync(schoolA, null, null, null, 1, 50);
        Assert.Equal(2, bySchool.TotalCount);
        Assert.All(bySchool.Items, r => Assert.Equal(schoolA, r.TargetSchoolId));
        // Joined actor + school
        Assert.All(bySchool.Items, r => Assert.Equal("Grace Mokoena", r.SuperAdminName));
        Assert.All(bySchool.Items, r => Assert.Equal("Alpha College", r.TargetSchoolName));
        Assert.All(bySchool.Items, r => Assert.False(string.IsNullOrEmpty(r.SuperAdminEmail)));

        // Filter by action type
        var byAction = await svc.GetAuditLogAsync(null, SuperAdminAuditActions.SchoolStatusChanged, null, null, 1, 50);
        Assert.Equal(2, byAction.TotalCount);
        Assert.All(byAction.Items, r => Assert.Equal(SuperAdminAuditActions.SchoolStatusChanged, r.ActionType));

        // Filter by date window (only the middle row)
        var byDate = await svc.GetAuditLogAsync(null, null, t.AddDays(5), t.AddDays(15), 1, 50);
        Assert.Equal(1, byDate.TotalCount);
        Assert.Equal(SuperAdminAuditActions.SchoolFeaturesUpdated, byDate.Items[0].ActionType);
    }
}
