namespace SchoolPortal.Server.Authorization;

/// <summary>
/// Compile-time-safe permission keys mirroring the seeded catalogue
/// (PositionsSeedData). The catalogue is the runtime source of truth; these constants
/// exist so controllers/services never hand-type permission strings.
/// </summary>
public static class PermissionKeys
{
    // Academic
    public const string MarksCapture = "marks.capture";
    public const string MarksViewOwn = "marks.view_own";
    public const string MarksViewChild = "marks.view_child";
    public const string MarksViewSubject = "marks.view_subject";
    public const string MarksViewGrade = "marks.view_grade";
    public const string MarksViewPhase = "marks.view_phase";
    public const string MarksViewAll = "marks.view_all";
    public const string MarksViewClass = "marks.view_class";
    public const string AssessmentCreate = "assessment.create";
    public const string AssessmentApprovePlan = "assessment.approve_plan";
    public const string AttendanceCapture = "attendance.capture";
    public const string AttendanceViewOwn = "attendance.view_own";
    public const string AttendanceViewChild = "attendance.view_child";
    public const string AttendanceViewClass = "attendance.view_class";
    public const string AttendanceViewGrade = "attendance.view_grade";
    public const string AssignmentsViewAssigned = "assignments.view_assigned";
    public const string AssignmentsSubmit = "assignments.submit";
    public const string ReportDraft = "report.draft";
    public const string ReportApprove = "report.approve";

    // Courses (LMS)
    public const string CoursesManage = "courses.manage";

    // Pathways
    public const string PathwaysViewOwn = "pathways.view_own";
    public const string PathwaysViewChild = "pathways.view_child";
    public const string PathwaysAdvise = "pathways.advise";
    public const string PathwaysCohortView = "pathways.cohort_view";

    // Discipline
    public const string DisciplineLog = "discipline.log";
    public const string DisciplineEscalate = "discipline.escalate";
    public const string DisciplineResolve = "discipline.resolve";

    // Finance
    public const string FinanceViewOwn = "finance.view_own";
    public const string FinancePay = "finance.pay";
    public const string FinanceViewAll = "finance.view_all";
    public const string FinanceCreateInvoice = "finance.create_invoice";
    public const string FinanceCapturePayment = "finance.capture_payment";
    public const string FinanceRefund = "finance.refund";
    public const string FinanceExemptInitiate = "finance.exempt_initiate";
    public const string FinanceExemptApprove = "finance.exempt_approve";
    public const string FinanceReports = "finance.reports";
    public const string FinanceYearEnd = "finance.year_end";
    public const string FinanceAuditPack = "finance.audit_pack";

    // Communication
    public const string CommunicationsMessageClass = "communications.message_class";
    public const string CommunicationsMessageGrade = "communications.message_grade";
    public const string CommunicationsMessageAll = "communications.message_all";
    public const string CommunicationsWhatsAppTrigger = "communications.whatsapp_trigger";
    public const string CommunicationsWhatsAppAdmin = "communications.whatsapp_admin";

    // System
    public const string SystemUsersManage = "system.users_manage";
    public const string SystemPositionsAssign = "system.positions_assign";
    public const string SystemIntegrations = "system.integrations";
    public const string SystemAuditLogView = "system.audit_log_view";
    public const string SystemBackup = "system.backup";
    public const string SystemFeatureFlags = "system.feature_flags";
    public const string SystemDataExport = "system.data_export";
    public const string SystemPopiaAdmin = "system.popia_admin";
    public const string SystemRefreshViews = "system.refresh_views";

    // Analytics
    public const string AnalyticsViewSchool = "analytics.view_school";

    // Reporting
    public const string ReportingView = "reporting.view";
    public const string ReportingPrincipalSummary = "reporting.principal_summary";

    // Communication & social (Step 6 comms cluster)
    public const string AnnouncementsPublish = "announcements.publish";
    public const string CalendarManage = "calendar.manage";
    public const string TimetableManage = "timetable.manage";
    public const string ActivitiesManage = "activities.manage";
    public const string SkillsEndorse = "skills.endorse";

    // Admin / system (Step 6 admin cluster)
    public const string SchoolManage = "school.manage";
    public const string AcademicsManage = "academics.manage";
    // Sprint 1.5.1 Gap 3: read-only academic-configuration diagnostics (subject-match report).
    // Separate from academics.manage so ITAdministrator gets the diagnostic without structure writes.
    public const string AcademicsDiagnostics = "academics.diagnostics";
    public const string AiUse = "ai.use";
    // Sprint 1.5.2 Step 3 — ask the Matric Hub AI tutor. Learner identity-implicit (the
    // feature exists FOR learners); also granted to the marks.view_class teaching/oversight
    // cluster so staff can exercise what their learners see. Separate from ai.use (teacher
    // authoring tools) so learner tutor access can be revoked without touching staff AI.
    public const string AiTutor = "ai.tutor";

    // Platform — baseline "any authenticated user" permission (identity-implicit, all identities)
    public const string PlatformAccess = "platform.access";

    /// <summary>
    /// Permissions whose checks must NEVER trust the JWT position cache — the resolver
    /// re-reads positions from the database at call time (STEP 3 Section C). Finance
    /// writes, exports/bulk reads, and position/permission administration.
    /// finance.exempt_initiate is included although the spec's list omitted it: it is a
    /// finance write, and deny-by-default says when unsure, treat as sensitive.
    /// </summary>
    public static readonly IReadOnlySet<string> Sensitive = new HashSet<string>
    {
        FinanceCreateInvoice, FinanceCapturePayment, FinanceRefund,
        FinanceExemptInitiate, FinanceExemptApprove, FinanceYearEnd, FinanceAuditPack,
        // Bulk financial reads (all accounts / financial reports) → DB-resolve per request (FIN-3).
        FinanceViewAll, FinanceReports,
        SystemBackup, SystemPositionsAssign, SystemUsersManage,
        // Bulk PII export + POPIA administration: never trust the cached JWT set —
        // re-resolve from the DB per request.
        SystemDataExport, SystemPopiaAdmin,
        // School-wide analytics surfaces named at-risk learners + activity → DB-resolve per request.
        AnalyticsViewSchool,
        // Principal's end-of-term class summary (school-wide named data) → DB-resolve per request.
        ReportingPrincipalSummary,
        // Refreshing the materialized views recomputes over ALL school data → DB-resolve per request.
        SystemRefreshViews,
    };

    /// <summary>Identity-implicit permissions — granted by Layer-1 identity alone, no
    /// position required. WHICH child/record is a Layer-3 scope check, not handled here.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> IdentityImplicit =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["Learner"] = new HashSet<string>
            {
                PlatformAccess,
                MarksViewOwn, AttendanceViewOwn, PathwaysViewOwn,
                AssignmentsViewAssigned, AssignmentsSubmit, FinanceViewOwn,
                AiTutor, // Sprint 1.5.2: Matric AI tutor — rate-limited + cost-capped
            },
            ["Parent"] = new HashSet<string>
            {
                PlatformAccess,
                MarksViewChild, AttendanceViewChild, PathwaysViewChild,
                FinanceViewOwn, FinancePay,
            },
            // Staff, External, System hold no DOMAIN permissions implicitly — those come only via
            // positions — but every authenticated identity gets platform.access (D1 / Step 6): the
            // baseline "any logged-in user" permission for endpoints like /api/me.
            ["Staff"] = new HashSet<string> { PlatformAccess },
            ["External"] = new HashSet<string> { PlatformAccess },
            ["System"] = new HashSet<string> { PlatformAccess },
        };
}
