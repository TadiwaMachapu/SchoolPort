using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 Inventory-B — Finance cluster (maximum scrutiny: real money). Covers cross-tenant writes
/// (body ids), the money-crossing-tenant payment case (no payment row, no balance change), cross-user
/// fee-statement isolation, and the two committed Segregation-of-Duties properties verified at the
/// API / resolved-permission level. Dual assertion throughout: status AND no-row-mutated.
/// </summary>
[Collection("SecurityApi")]
public class FinanceCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public FinanceCrossTenantWriteTests(ApiFactory api) => _api = api;

    // ---- Cross-tenant writes (body ids) ---------------------------------------------------------

    [CrossTenantGuard(typeof(FeesController), nameof(FeesController.CreateFee))]
    [CrossTenantGuard(typeof(FeesController), nameof(FeesController.UpdateFee))]
    [CrossTenantGuard(typeof(FeesController), nameof(FeesController.DeleteFee))]
    [CrossTenantGuard(typeof(FeesController), nameof(FeesController.RecordPayment))]
    [Fact]
    public async Task CreateFee_ForeignTerm_Returns404_AndCreatesNoFee()
    {
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");
        var foreignTerm = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var term = AddTerm(db, schoolB);
            await db.SaveChangesAsync();
            return term;
        });

        var resp = await _api.ClientFor(fm).PostAsJsonAsync("/api/fees",
            new { name = "Tuition", amountZar = 1000m, dueDate = DateTime.UtcNow.AddDays(30), termId = foreignTerm });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Fees.CountAsync(f => f.SchoolId == schoolA)));
    }

    [Fact]
    public async Task UpdateFee_ForeignTermInBody_Returns404_AndLeavesFeeUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");
        var (localFee, foreignTerm) = await _api.WithScopeAsync(async db =>
        {
            var fee = AddFee(db, schoolA, "A-Fee", 500m);
            var schoolB = AddSchool(db);
            var term = AddTerm(db, schoolB);
            await db.SaveChangesAsync();
            return (fee, term);
        });

        var resp = await _api.ClientFor(fm).PutAsJsonAsync($"/api/fees/{localFee}",
            new { name = "A-Fee", amountZar = 500m, dueDate = DateTime.UtcNow.AddDays(30), termId = foreignTerm });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var termId = await _api.WithScopeAsync(db => db.Fees.Where(f => f.FeeId == localFee).Select(f => f.TermId).SingleAsync());
        Assert.Null(termId); // foreign term was NOT linked
    }

    [Fact]
    public async Task UpdateFee_ForeignFeeId_Returns404_AndUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");
        var foreignFee = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var fee = AddFee(db, schoolB, "B-Fee", 750m);
            await db.SaveChangesAsync();
            return fee;
        });

        var resp = await _api.ClientFor(fm).PutAsJsonAsync($"/api/fees/{foreignFee}",
            new { name = "Hijacked", amountZar = 1m, dueDate = DateTime.UtcNow.AddDays(30), termId = (Guid?)null });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var (name, amount) = await _api.WithScopeAsync(db => db.Fees.Where(f => f.FeeId == foreignFee).Select(f => new ValueTuple<string, decimal>(f.Name, f.AmountZar)).SingleAsync());
        Assert.Equal("B-Fee", name);
        Assert.Equal(750m, amount);
    }

    [Fact]
    public async Task DeleteFee_ForeignFeeId_Returns404_AndStillExists()
    {
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");
        var foreignFee = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var fee = AddFee(db, schoolB, "B-Fee", 750m);
            await db.SaveChangesAsync();
            return fee;
        });

        var resp = await _api.ClientFor(fm).DeleteAsync($"/api/fees/{foreignFee}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Fees.AnyAsync(f => f.FeeId == foreignFee)));
    }

    // ---- Money crossing tenants — the worst kind ------------------------------------------------

    [Fact]
    public async Task RecordPayment_ForeignStudent_Returns404_NoPaymentRow_NoBalanceChange()
    {
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");
        var (localFee, foreignStudent) = await _api.WithScopeAsync(async db =>
        {
            var fee = AddFee(db, schoolA, "Tuition", 1000m);   // a real fee in the caller's school
            var schoolB = AddSchool(db);
            var student = AddStudent(db, schoolB);             // a foreign learner
            await db.SaveChangesAsync();
            return (fee, student);
        });

        var resp = await _api.ClientFor(fm).PostAsJsonAsync($"/api/fees/{localFee}/payments",
            new { studentId = foreignStudent, amountPaidZar = 500m, paidAt = (DateTime?)null, notes = "x" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        // Money must NOT move: no payment row at all, and nothing collected against the fee.
        Assert.Equal(0, await _api.WithScopeAsync(db => db.FeePayments.CountAsync(p => p.FeeId == localFee)));
        Assert.Equal(0, await _api.WithScopeAsync(db => db.FeePayments.CountAsync(p => p.StudentId == foreignStudent)));
        var collected = await _api.WithScopeAsync(async db => await db.FeePayments.Where(p => p.FeeId == localFee).SumAsync(p => (decimal?)p.AmountPaidZar) ?? 0m);
        Assert.Equal(0m, collected);
    }

    [Fact]
    public async Task RecordPayment_ForeignFeeId_Returns404_NoPaymentRow()
    {
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");
        var (foreignFee, localStudent) = await _api.WithScopeAsync(async db =>
        {
            var student = AddStudent(db, schoolA);
            var schoolB = AddSchool(db);
            var fee = AddFee(db, schoolB, "B-Fee", 1000m);
            await db.SaveChangesAsync();
            return (fee, student);
        });

        var resp = await _api.ClientFor(fm).PostAsJsonAsync($"/api/fees/{foreignFee}/payments",
            new { studentId = localStudent, amountPaidZar = 500m, paidAt = (DateTime?)null, notes = "x" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.FeePayments.CountAsync(p => p.FeeId == foreignFee)));
    }

    // ---- Cross-user: fee-statement isolation (no id param → not coercible) -----------------------

    [Fact]
    public async Task MyStatement_LearnerSeesOnlyOwnPayments_NotAnotherStudentsInSameSchool()
    {
        var schoolA = Guid.NewGuid();
        var learnerA = await _api.MintTokenAsync(schoolA, "Learner");
        var learnerB = await _api.MintTokenAsync(schoolA, "Learner");
        var feeId = await _api.WithScopeAsync(async db =>
        {
            var studentA = AddStudentFor(db, schoolA, learnerA.UserId);
            AddStudentFor(db, schoolA, learnerB.UserId);
            var fee = AddFee(db, schoolA, "Tuition", 1000m);
            // A payment by student A only.
            db.FeePayments.Add(new FeePayment { FeePaymentId = Guid.NewGuid(), FeeId = fee, StudentId = studentA, SchoolId = schoolA, AmountPaidZar = 400m, PaidAt = DateTime.UtcNow, RecordedByUserId = learnerA.UserId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return fee;
        });

        var aRows = await (await _api.ClientFor(learnerA).GetAsync("/api/fees/my-statement")).Content.ReadFromJsonAsync<List<StmtRow>>();
        var bRows = await (await _api.ClientFor(learnerB).GetAsync("/api/fees/my-statement")).Content.ReadFromJsonAsync<List<StmtRow>>();

        Assert.Equal(400m, aRows!.Single(r => r.FeeId == feeId).AmountPaid);  // A sees their own payment
        Assert.Equal(0m, bRows!.Single(r => r.FeeId == feeId).AmountPaid);    // B sees zero — no leakage, no coercion vector (endpoint takes no id)
    }

    // ---- Segregation of Duties (the two committed properties) ------------------------------------

    [Fact]
    public async Task SoD_Bursar_CannotCreateFee_Returns403_AndCreatesNoFee()
    {
        var schoolA = Guid.NewGuid();
        var bursar = await _api.MintTokenAsync(schoolA, "Staff", "BursarDebtorsClerk");

        var resp = await _api.ClientFor(bursar).PostAsJsonAsync("/api/fees",
            new { name = "Tuition", amountZar = 1000m, dueDate = DateTime.UtcNow.AddDays(30), termId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode); // finance.create_invoice was revoked from Bursar
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Fees.CountAsync(f => f.SchoolId == schoolA)));
    }

    [Fact]
    public async Task SoD_FinanceManager_DoesNotHold_ExemptApprove()
    {
        // No exempt-approve endpoint exists yet (Sprint 1.5.4), so this is verified at the
        // resolved-permission level via /api/me — the real token resolution, not just the seed.
        var schoolA = Guid.NewGuid();
        var fm = await _api.MintTokenAsync(schoolA, "Staff", "FinanceManager");

        var resp = await _api.ClientFor(fm).GetAsync("/api/me");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var perms = doc.RootElement.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.DoesNotContain("finance.exempt_approve", perms);   // the committed SoD revocation — FM may NOT approve exemptions
        Assert.Contains("finance.exempt_initiate", perms);        // sanity: FM STILL initiates (we didn't over-revoke)
        Assert.Contains("finance.capture_payment", perms);        // sanity: FM keeps operational capture
    }

    private sealed record StmtRow(Guid FeeId, decimal AmountPaid);

    // ---- seed helpers (add-only; caller saves) --------------------------------------------------

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

    private static Guid AddStudent(SchoolPortalDbContext db, Guid schoolId) =>
        AddStudentFor(db, schoolId, AddUser(db, schoolId, "Student", "Learner"));

    private static Guid AddStudentFor(SchoolPortalDbContext db, Guid schoolId, Guid userId)
    {
        var studentId = Guid.NewGuid();
        db.Students.Add(new Student { StudentId = studentId, SchoolId = schoolId, UserId = userId, StudentNumber = "N" + userId.ToString("N")[..6], CreatedAt = DateTime.UtcNow });
        return studentId;
    }

    private static Guid AddTerm(SchoolPortalDbContext db, Guid schoolId)
    {
        var yearId = Guid.NewGuid();
        db.AcademicYears.Add(new AcademicYear { AcademicYearId = yearId, SchoolId = schoolId, Year = 2026, StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow });
        var termId = Guid.NewGuid();
        db.Terms.Add(new Term { TermId = termId, AcademicYearId = yearId, SchoolId = schoolId, TermNumber = 1, StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow });
        return termId;
    }

    private static Guid AddFee(SchoolPortalDbContext db, Guid schoolId, string name, decimal amount)
    {
        var id = Guid.NewGuid();
        db.Fees.Add(new Fee { FeeId = id, SchoolId = schoolId, Name = name, AmountZar = amount, DueDate = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow });
        return id;
    }
}
