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
        SystemBackup, SystemPositionsAssign, SystemUsersManage,
    };

    /// <summary>Identity-implicit permissions — granted by Layer-1 identity alone, no
    /// position required. WHICH child/record is a Layer-3 scope check, not handled here.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> IdentityImplicit =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["Learner"] = new HashSet<string>
            {
                MarksViewOwn, AttendanceViewOwn, PathwaysViewOwn,
                AssignmentsViewAssigned, AssignmentsSubmit, FinanceViewOwn,
            },
            ["Parent"] = new HashSet<string>
            {
                MarksViewChild, AttendanceViewChild, PathwaysViewChild,
                FinanceViewOwn, FinancePay,
            },
            // Staff, External, System: no implicit permissions — everything via positions.
        };
}
