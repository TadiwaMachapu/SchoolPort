namespace SchoolPortal.Server.Authorization;

/// <summary>Compile-time-safe position keys mirroring the seeded catalogue.</summary>
public static class PositionKeys
{
    // SMT
    public const string Principal = "Principal";
    public const string DeputyPrincipal = "DeputyPrincipal";
    public const string HOD = "HOD";
    public const string PhaseHead = "PhaseHead";
    public const string GradeHead = "GradeHead";
    // Teaching
    public const string SubjectTeacher = "SubjectTeacher";
    public const string ClassTeacher = "ClassTeacher";
    public const string LOTeacher = "LOTeacher";
    public const string SportCultureMIC = "SportCultureMIC";
    // Finance
    public const string FinanceManager = "FinanceManager";
    public const string BursarDebtorsClerk = "BursarDebtorsClerk";
    public const string Cashier = "Cashier";
    // Operational
    public const string ITAdministrator = "ITAdministrator";
    // External
    public const string Auditor = "Auditor";
    public const string DistrictOfficial = "DistrictOfficial";
    // System
    public const string SystemSupport = "SystemSupport";
}

/// <summary>Layer-1 identity values (User.Identity / "identity" claim).</summary>
public static class IdentityKeys
{
    public const string Staff = "Staff";
    public const string Learner = "Learner";
    public const string Parent = "Parent";
    public const string External = "External";
    public const string System = "System";
}
