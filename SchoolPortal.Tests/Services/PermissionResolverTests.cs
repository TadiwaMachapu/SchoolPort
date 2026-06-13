using System.Collections.Frozen;
using SchoolPortal.Server.Authorization;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Pure in-memory tests for the STEP 3 claim-path resolution: window enforcement,
/// position union, identity-implicit grants, and deny-by-default parsing. The DB path
/// and seed sync are covered by the Postgres integration tests; the full authorisation
/// suite lands in Step 10.
/// </summary>
public class PermissionResolverTests
{
    private static readonly DateTime Now = new(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

    // Claim-path resolution needs no DbContext; the resolver only touches the DB on the
    // sensitive path, so passing a null context is safe for these tests.
    private static PermissionResolver CreateResolver(
        Dictionary<string, string[]>? map = null)
    {
        // PermissionCatalogueCache loads from DB normally; for unit tests we use the
        // public surface only — an unloaded cache returns empty sets, so tests that need
        // a map use reflection-free seeding via LoadAsync-equivalent below.
        var catalogue = new PermissionCatalogueCache();
        if (map is not null)
            CatalogueTestHelper.Seed(catalogue, map);
        return new PermissionResolver(db: null!, catalogue);
    }

    private static PositionClaim Pos(string key, DateTime? from = null, DateTime? to = null,
        params ScopeClaim[] scopes) => new()
    {
        Key = key,
        EffectiveFrom = from ?? Now.AddDays(-30),
        EffectiveTo = to,
        Scopes = scopes.ToList(),
    };

    private static readonly Dictionary<string, string[]> Map = new()
    {
        [PositionKeys.SubjectTeacher] = new[] { PermissionKeys.MarksCapture, PermissionKeys.AttendanceCapture },
        [PositionKeys.HOD] = new[] { PermissionKeys.MarksViewSubject, PermissionKeys.ReportApprove },
    };

    [Fact]
    public void ExpiredPosition_GrantsNothing()
    {
        var resolver = CreateResolver(Map);
        var expired = Pos(PositionKeys.SubjectTeacher, to: Now.AddDays(-1));

        var set = resolver.ResolveFromClaims(IdentityKeys.Staff, new[] { expired }, Now);

        // The expired position contributes nothing; only the identity baseline remains.
        Assert.False(set.HasPosition(PositionKeys.SubjectTeacher));
        Assert.False(set.HasPermission(PermissionKeys.MarksCapture));
        Assert.False(set.HasPermission(PermissionKeys.AttendanceCapture));
        Assert.Equal(new[] { PermissionKeys.PlatformAccess }, set.Permissions);
    }

    [Fact]
    public void NotYetEffectivePosition_GrantsNothing()
    {
        var resolver = CreateResolver(Map);
        var future = Pos(PositionKeys.SubjectTeacher, from: Now.AddDays(1));

        var set = resolver.ResolveFromClaims(IdentityKeys.Staff, new[] { future }, Now);

        // The not-yet-effective position contributes nothing; only the identity baseline remains.
        Assert.False(set.HasPermission(PermissionKeys.MarksCapture));
        Assert.Equal(new[] { PermissionKeys.PlatformAccess }, set.Permissions);
    }

    [Fact]
    public void PositionUnion_IsUnion_AndRemovalRemovesOnlyItsPermissions()
    {
        var resolver = CreateResolver(Map);
        var both = new[] { Pos(PositionKeys.SubjectTeacher), Pos(PositionKeys.HOD) };

        var union = resolver.ResolveFromClaims(IdentityKeys.Staff, both, Now);
        Assert.True(union.HasPermission(PermissionKeys.MarksCapture));
        Assert.True(union.HasPermission(PermissionKeys.ReportApprove));

        var withoutHod = resolver.ResolveFromClaims(IdentityKeys.Staff,
            new[] { Pos(PositionKeys.SubjectTeacher) }, Now);
        Assert.True(withoutHod.HasPermission(PermissionKeys.MarksCapture));   // kept
        Assert.False(withoutHod.HasPermission(PermissionKeys.ReportApprove)); // HOD's gone
        Assert.False(withoutHod.HasPermission(PermissionKeys.MarksViewSubject));
    }

    [Fact]
    public void IdentityImplicit_GrantedWithoutAnyPosition()
    {
        var resolver = CreateResolver();

        var learner = resolver.ResolveFromClaims(IdentityKeys.Learner, Array.Empty<PositionClaim>(), Now);
        Assert.True(learner.HasPermission(PermissionKeys.MarksViewOwn));
        Assert.True(learner.HasPermission(PermissionKeys.AssignmentsSubmit));
        Assert.True(learner.HasPermission(PermissionKeys.FinanceViewOwn));

        var parent = resolver.ResolveFromClaims(IdentityKeys.Parent, Array.Empty<PositionClaim>(), Now);
        Assert.True(parent.HasPermission(PermissionKeys.MarksViewChild));
        Assert.True(parent.HasPermission(PermissionKeys.FinancePay));
    }

    [Fact]
    public void IdentityImplicit_NotLeakedAcrossIdentity()
    {
        var resolver = CreateResolver();

        var staff = resolver.ResolveFromClaims(IdentityKeys.Staff, Array.Empty<PositionClaim>(), Now);
        Assert.False(staff.HasPermission(PermissionKeys.MarksViewOwn));   // Learner's, not leaked
        Assert.False(staff.HasPermission(PermissionKeys.MarksViewChild)); // Parent's, not leaked
        // Staff with no positions holds ONLY the baseline platform.access; all domain
        // permissions come via positions (D1 / Step 6).
        Assert.Equal(new[] { PermissionKeys.PlatformAccess }, staff.Permissions);
    }

    [Fact]
    public void MalformedOrMissingPosClaim_ParsesToEmpty_DenyByDefault()
    {
        Assert.Empty(PositionClaim.Parse(null));
        Assert.Empty(PositionClaim.Parse(""));
        Assert.Empty(PositionClaim.Parse("not-json{{{"));
        Assert.Empty(PositionClaim.Parse("{\"k\":\"object-not-array\"}"));
    }

    [Fact]
    public void PosClaim_RoundTrips_WithScopesAndDates()
    {
        var original = new[]
        {
            Pos(PositionKeys.HOD, to: Now.AddDays(30),
                scopes: new ScopeClaim { ScopeType = Data.Entities.ScopeType.Subject, ScopeRefId = Guid.NewGuid() }),
        };

        var parsed = PositionClaim.Parse(PositionClaim.Serialize(original));

        var p = Assert.Single(parsed);
        Assert.Equal(PositionKeys.HOD, p.Key);
        Assert.Equal(original[0].EffectiveTo, p.EffectiveTo);
        var s = Assert.Single(p.Scopes);
        Assert.Equal(Data.Entities.ScopeType.Subject, s.ScopeType);
        Assert.Equal(original[0].Scopes[0].ScopeRefId, s.ScopeRefId);
    }

    [Theory]
    [InlineData(PermissionKeys.FinanceRefund, IdentityKeys.Staff, true)]      // sensitive permission
    [InlineData(PermissionKeys.FinanceCapturePayment, IdentityKeys.Staff, true)]
    [InlineData(PermissionKeys.SystemPositionsAssign, IdentityKeys.Staff, true)]
    [InlineData(PermissionKeys.MarksCapture, IdentityKeys.Staff, false)]      // routine
    [InlineData(PermissionKeys.MarksViewAll, IdentityKeys.External, true)]    // External: always
    [InlineData(PermissionKeys.SystemAuditLogView, IdentityKeys.System, true)] // System: always
    public void RequiresDatabaseResolution_MatchesSectionCBoundary(
        string permission, string identity, bool expected) =>
        Assert.Equal(expected, PermissionResolver.RequiresDatabaseResolution(permission, identity));
}

/// <summary>Seeds a PermissionCatalogueCache for unit tests without a database.</summary>
internal static class CatalogueTestHelper
{
    public static void Seed(PermissionCatalogueCache cache, Dictionary<string, string[]> map)
    {
        // The cache's only mutation path is LoadAsync(db); for unit tests we set the
        // backing field directly to avoid dragging a database into claim-path tests.
        var field = typeof(PermissionCatalogueCache)
            .GetField("_permissionsByPosition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(cache, map.ToDictionary(kv => kv.Key, kv => kv.Value.ToHashSet().ToFrozenSet())
            .ToFrozenDictionary());
    }
}
