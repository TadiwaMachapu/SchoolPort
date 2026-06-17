using System.Net;
using System.Text;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.PermissionContract;

/// <summary>
/// Step 10 Inventory A (part 2) — permission enforcement, behaviorally, on the real HTTP pipeline.
/// Per distinct permission key, a representative endpoint is hit three ways:
///   holder    → authorized (NOT 401/403 — may be 2xx/400/404 from the action, but it passed the gate)
///   non-holder → 403 (authenticated but lacking the permission; the handler denies)
///   no token   → 401
/// "Holder authorized = not 401/403" avoids needing a valid body per endpoint while still proving the
/// gate lets holders through. Sensitive keys (finance.*, system.*, analytics, principal_summary) are
/// DB-resolved by the handler — covered the same way. Where it proves a real tightening, the non-holder
/// is a specific position (rank teacher for analytics.view_school; Bursar for finance.create_invoice).
/// platform.access is universal (every identity holds it) so it has no non-holder case.
/// </summary>
[Collection("SecurityApi")]
public class PermissionBehavioralTests
{
    private readonly ApiFactory _api;
    public PermissionBehavioralTests(ApiFactory api) => _api = api;

    // perm | method | url | holderIdentity | holderPositions(csv) | nonHolderIdentity("" = none/universal) | nonHolderPositions(csv)
    public static IEnumerable<object[]> Matrix() => new[]
    {
        Row("platform.access",                 "GET",  "/api/me",                              "Staff",   "",                  "",       ""),
        Row("academics.manage",                "POST", "/api/classes",                         "Staff",   "Principal",         "Learner",""),
        Row("marks.view_class",                "GET",  "/api/gradebook/{g}",                   "Staff",   "SubjectTeacher",    "Learner",""),
        Row("marks.view_own",                  "GET",  "/api/gradebook/my-grades",             "Learner", "",                  "Staff",  ""),
        Row("marks.capture",                   "POST", "/api/grades",                          "Staff",   "SubjectTeacher",    "Learner",""),
        Row("assessment.create",               "POST", "/api/assignments",                     "Staff",   "SubjectTeacher",    "Learner",""),
        Row("attendance.view_class",           "GET",  "/api/attendance?classId={g}&date=2026-01-01", "Staff", "SubjectTeacher","Learner",""),
        Row("attendance.view_own",             "GET",  "/api/attendance/mine",                 "Learner", "",                  "Staff",  ""),
        Row("attendance.capture",              "POST", "/api/attendance/bulk",                 "Staff",   "SubjectTeacher",    "Learner",""),
        Row("attendance.view_child",           "GET",  "/api/parent/children/{g}/attendance",  "Parent",  "",                  "Staff",  ""),
        Row("marks.view_child",                "GET",  "/api/parent/children/{g}/grades",      "Parent",  "",                  "Staff",  ""),
        Row("pathways.view_own",               "GET",  "/api/pathways/mine",                   "Learner", "",                  "Staff",  ""),
        Row("pathways.view_child",             "GET",  "/api/parent/pathways",                 "Parent",  "",                  "Staff",  ""),
        Row("assignments.submit",             "POST", "/api/submissions",                     "Learner", "",                  "Staff",  ""),
        Row("assignments.view_assigned",       "GET",  "/api/submissions/by-assignment/{g}/mine","Learner","",                 "Staff",  ""),
        Row("courses.manage",                  "POST", "/api/courses",                         "Staff",   "SubjectTeacher",    "Learner",""),
        Row("announcements.publish",           "POST", "/api/announcements",                   "Staff",   "SubjectTeacher",    "Learner",""),
        Row("calendar.manage",                 "POST", "/api/calendar/events",                 "Staff",   "SubjectTeacher",    "Learner",""),
        Row("timetable.manage",                "POST", "/api/calendar/timetable",              "Staff",   "Principal",         "Learner",""),
        Row("activities.manage",               "GET",  "/api/activities",                      "Staff",   "Principal",         "Learner",""),
        Row("skills.endorse",                  "GET",  "/api/skills/learner/{g}",              "Staff",   "SubjectTeacher",    "Learner",""),
        Row("communications.message_class",    "POST", "/api/messages/threads/class/{g}",      "Staff",   "ClassTeacher",      "Learner",""),
        // Intentional tightening: rank-and-file SubjectTeacher must NOT have school-wide analytics.
        Row("analytics.view_school",           "GET",  "/api/analytics/overview",              "Staff",   "Principal",         "Staff",  "SubjectTeacher"),
        Row("reporting.view",                  "GET",  "/api/reports/term-report/{g}/{g}",     "Staff",   "SubjectTeacher",    "Learner",""),
        // principal_summary ANDs over reporting.view; a SubjectTeacher has view but not principal_summary.
        Row("reporting.principal_summary",     "POST", "/api/reports/principal-summary?classId={g}&termId={g}", "Staff", "Principal", "Staff", "SubjectTeacher"),
        Row("school.manage",                   "PUT",  "/api/schools/info",                    "Staff",   "Principal",         "Learner",""),
        Row("system.feature_flags",            "PUT",  "/api/schools/features",                "Staff",   "Principal",         "Learner",""),
        Row("ai.use",                          "POST", "/api/ai/plagiarism-check",             "Staff",   "SubjectTeacher",    "Learner",""),
        Row("finance.view_all",                "GET",  "/api/fees",                            "Staff",   "FinanceManager",    "Learner",""),
        // SoD: Bursar must NOT create invoices (revoked).
        Row("finance.create_invoice",          "POST", "/api/fees",                            "Staff",   "FinanceManager",    "Staff",  "BursarDebtorsClerk"),
        Row("finance.capture_payment",         "POST", "/api/fees/{g}/payments",               "Staff",   "FinanceManager",    "Learner",""),
        Row("finance.view_own",                "GET",  "/api/fees/my-statement",               "Learner", "",                  "Staff",  ""),
        Row("system.users_manage",             "GET",  "/api/users",                           "Staff",   "ITAdministrator",   "Learner",""),
        Row("system.positions_assign",         "GET",  "/api/positions/catalogue",             "Staff",   "ITAdministrator",   "Learner",""),
        Row("system.integrations",             "GET",  "/api/plugins/installed",               "Staff",   "ITAdministrator",   "Learner",""),
        Row("system.data_export",              "GET",  "/api/sasams/export/learners",          "Staff",   "Principal",         "Learner",""),
        Row("system.popia_admin",              "GET",  "/api/popia/admin/consents",            "Staff",   "Principal",         "Learner",""),
        Row("system.refresh_views",            "POST", "/api/admin/refresh-views",             "Staff",   "Principal",         "Learner",""),
        Row("communications.whatsapp_admin",   "GET",  "/api/whatsapp/settings",               "Staff",   "ITAdministrator",   "Learner",""),
        Row("communications.whatsapp_trigger", "POST", "/api/whatsapp/parents/absence-reminders","Staff",  "Principal",        "Learner",""),
    };

    private static object[] Row(string perm, string method, string url, string hId, string hPos, string nhId, string nhPos)
        => new object[] { perm, method, url, hId, hPos, nhId, nhPos };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Permission_HolderAllowed_NonHolderForbidden_NoTokenUnauthorized(
        string perm, string method, string url, string hId, string hPos, string nhId, string nhPos)
    {
        var resolved = url.Replace("{g}", Guid.NewGuid().ToString());

        // Holder → must pass the gate (not 401/403).
        var holder = await _api.MintTokenAsync(Guid.NewGuid(), hId, Split(hPos));
        var hResp = await Send(_api.ClientFor(holder), method, resolved);
        Assert.False(hResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"[{perm}] holder ({hId}/{hPos}) was denied with {(int)hResp.StatusCode} — the gate over-denies a holder.");

        // No token → 401.
        var anonResp = await Send(_api.AnonymousClient(), method, resolved);
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // Non-holder → 403 (skipped for universal platform.access, which every identity holds).
        if (nhId.Length > 0)
        {
            var nonHolder = await _api.MintTokenAsync(Guid.NewGuid(), nhId, Split(nhPos));
            var nhResp = await Send(_api.ClientFor(nonHolder), method, resolved);
            Assert.Equal(HttpStatusCode.Forbidden, nhResp.StatusCode);
        }
    }

    [Fact]
    public async Task SuperAdminEndpoint_DeniesNormalTokenAndNoToken()
    {
        // /api/super uses a separate platform scheme ([RequireSuperAdmin]); a normal school JWT and an
        // anonymous caller must both be denied. (Holder requires a SuperAdmin login — out of scope here.)
        var staff = await _api.MintTokenAsync(Guid.NewGuid(), "Staff", "Principal");
        var normal = await Send(_api.ClientFor(staff), "GET", "/api/super/stats");
        Assert.True(normal.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"normal token reached SuperAdmin endpoint with {(int)normal.StatusCode}");
        var anon = await Send(_api.AnonymousClient(), "GET", "/api/super/stats");
        Assert.True(anon.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

    private static string[] Split(string csv) =>
        string.IsNullOrEmpty(csv) ? Array.Empty<string>() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string url)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json"); // empty body — auth runs before binding
        return client.SendAsync(req);
    }
}
