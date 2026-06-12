using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Seeds;

/// <summary>
/// Sprint 1.5.0 — seeds the global Permission and Position catalogues and the in-code
/// position→permission map (STEP 1 §5). This is the authoritative mapping; schools do not
/// define custom permissions. Idempotent: runs once when the catalogue is empty.
///
/// Note: identity-implicit permissions (marks.view_own, marks.view_child, pathways.view_own,
/// pathways.view_child, finance.view_own) are resolved in code from a user's Identity, not
/// attached to a position — they exist in the catalogue but appear in no position map.
/// </summary>
public static class PositionsSeedData
{
    public static async Task SeedAsync(SchoolPortalDbContext db, ILogger logger)
    {
        if (await db.Positions.AnyAsync()) return;

        logger.LogInformation("Seeding positions/permissions catalogue…");

        // ── Permissions ────────────────────────────────────────────────────────────
        Permission Perm(string key, string category, string desc) =>
            new() { Key = key, Category = category, Description = desc };

        var permissions = new[]
        {
            // Academic
            Perm("marks.capture",          "Academic", "Capture marks for in-scope classes"),
            Perm("marks.view_own",         "Academic", "View own marks (Learner, identity-implicit)"),
            Perm("marks.view_child",       "Academic", "View linked child's marks (Parent, identity-implicit)"),
            Perm("marks.view_subject",     "Academic", "View marks for an in-scope subject"),
            Perm("marks.view_grade",       "Academic", "View marks for an in-scope grade"),
            Perm("marks.view_phase",       "Academic", "View marks for an in-scope phase"),
            Perm("marks.view_all",         "Academic", "View all marks school-wide"),
            Perm("assessment.create",      "Academic", "Create assessments"),
            Perm("assessment.approve_plan","Academic", "Approve assessment plans"),
            Perm("attendance.capture",     "Academic", "Capture attendance for in-scope classes"),
            Perm("attendance.view_class",  "Academic", "View attendance for an in-scope class"),
            Perm("attendance.view_grade",  "Academic", "View attendance for an in-scope grade"),
            Perm("report.draft",           "Academic", "Draft report comments"),
            Perm("report.approve",         "Academic", "Approve reports"),
            // Pathways
            Perm("pathways.view_own",      "Pathways", "View own pathways (Learner, identity-implicit)"),
            Perm("pathways.view_child",    "Pathways", "View linked child's pathways (Parent, identity-implicit)"),
            Perm("pathways.advise",        "Pathways", "Provide pathways advice"),
            Perm("pathways.cohort_view",   "Pathways", "View pathways across an in-scope cohort"),
            // Discipline
            Perm("discipline.log",         "Discipline", "Log a discipline incident"),
            Perm("discipline.escalate",    "Discipline", "Escalate a discipline incident"),
            Perm("discipline.resolve",     "Discipline", "Resolve a discipline incident"),
            // Finance
            Perm("finance.view_own",       "Finance", "View own fee statement (Learner/Parent, identity-implicit)"),
            Perm("finance.view_all",       "Finance", "View all finance records school-wide"),
            Perm("finance.create_invoice", "Finance", "Create invoices/fees"),
            Perm("finance.capture_payment","Finance", "Capture a payment"),
            Perm("finance.refund",         "Finance", "Issue a refund"),
            Perm("finance.exempt_initiate","Finance", "Initiate a fee exemption"),
            Perm("finance.exempt_approve", "Finance", "Approve a fee exemption"),
            Perm("finance.reports",        "Finance", "Run finance reports"),
            Perm("finance.year_end",       "Finance", "Perform finance year-end"),
            Perm("finance.audit_pack",     "Finance", "Generate the finance audit pack"),
            // Communication
            Perm("communications.message_class", "Communication", "Message an in-scope class"),
            Perm("communications.message_grade", "Communication", "Message an in-scope grade"),
            Perm("communications.message_all",   "Communication", "Message school-wide"),
            Perm("communications.whatsapp_trigger","Communication", "Trigger a WhatsApp message"),
            Perm("communications.whatsapp_admin",  "Communication", "Administer WhatsApp integration"),
            // System
            Perm("system.users_manage",    "System", "Create/manage users"),
            Perm("system.positions_assign","System", "Assign/revoke positions"),
            Perm("system.integrations",    "System", "Manage integrations"),
            Perm("system.audit_log_view",  "System", "View the audit log"),
            Perm("system.backup",          "System", "Manage backups"),
            Perm("system.feature_flags",   "System", "Manage feature flags"),
        };
        var permByKey = permissions.ToDictionary(p => p.Key);
        db.Permissions.AddRange(permissions);

        // ── Positions ──────────────────────────────────────────────────────────────
        Position Pos(string key, string name, string category, ScopeType scope,
                     bool external = false, bool system = false, bool timeLimit = false,
                     bool consent = false, int? durationHours = null) =>
            new()
            {
                Key = key, DisplayName = name, Category = category, ScopeType = scope,
                IsExternal = external, IsSystem = system, RequiresTimeLimit = timeLimit,
                RequiresConsent = consent, DefaultDurationHours = durationHours
            };

        var positions = new[]
        {
            // SMT
            Pos("Principal",        "Principal",            "SMT", ScopeType.None),
            Pos("DeputyPrincipal",  "Deputy Principal",     "SMT", ScopeType.None),
            Pos("HOD",              "Head of Department",   "SMT", ScopeType.Subject),
            Pos("PhaseHead",        "Phase Head",           "SMT", ScopeType.Phase),
            Pos("GradeHead",        "Grade Head",           "SMT", ScopeType.Grade),
            // Teaching
            Pos("SubjectTeacher",   "Subject Teacher",      "Teaching", ScopeType.Class),
            Pos("ClassTeacher",     "Class Teacher",        "Teaching", ScopeType.Class),
            Pos("LOTeacher",        "LO Teacher",           "Teaching", ScopeType.Class),
            Pos("SportCultureMIC",  "Sport/Culture MIC",    "Teaching", ScopeType.Activity),
            // Finance
            Pos("FinanceManager",   "Finance Manager",      "Finance", ScopeType.None),
            Pos("BursarDebtorsClerk","Bursar/Debtors Clerk","Finance", ScopeType.None),
            Pos("Cashier",          "Cashier",              "Finance", ScopeType.None),
            // Operational
            Pos("ITAdministrator",  "IT Administrator",     "Operational", ScopeType.None),
            // External (read-only, time-limited)
            Pos("Auditor",          "Auditor",              "External", ScopeType.None, external: true, timeLimit: true),
            Pos("DistrictOfficial", "District Official",    "External", ScopeType.None, external: true, timeLimit: true),
            // System (consent-gated, time-limited, all actions logged)
            Pos("SystemSupport",    "System Support",       "System", ScopeType.None, system: true, timeLimit: true, consent: true, durationHours: 24),
        };
        var posByKey = positions.ToDictionary(p => p.Key);
        db.Positions.AddRange(positions);

        // ── Position → Permission map (STEP 1 §5) ────────────────────────────────────
        void Map(string positionKey, params string[] permissionKeys)
        {
            foreach (var pk in permissionKeys)
                db.PositionPermissions.Add(new PositionPermission
                {
                    Position = posByKey[positionKey],
                    Permission = permByKey[pk]
                });
        }

        Map("Principal",
            "marks.view_all", "attendance.view_grade", "report.approve", "assessment.approve_plan",
            "pathways.cohort_view", "discipline.escalate", "discipline.resolve",
            "communications.message_all", "communications.whatsapp_admin",
            "finance.view_all", "finance.reports", "finance.exempt_approve",
            "system.users_manage", "system.positions_assign", "system.audit_log_view");

        Map("DeputyPrincipal",
            "marks.view_all", "attendance.view_grade", "report.approve", "assessment.approve_plan",
            "pathways.cohort_view", "discipline.escalate", "discipline.resolve",
            "communications.message_all", "communications.whatsapp_admin",
            "finance.view_all", "finance.reports", "system.audit_log_view");

        Map("HOD",
            "marks.view_subject", "assessment.approve_plan", "report.approve", "attendance.view_class",
            "communications.message_class", "discipline.log", "discipline.escalate");

        Map("PhaseHead",
            "marks.view_phase", "attendance.view_grade", "report.approve",
            "discipline.escalate", "discipline.resolve", "communications.message_grade");

        Map("GradeHead",
            "marks.view_grade", "attendance.view_grade", "discipline.log", "discipline.escalate",
            "discipline.resolve", "communications.message_grade", "pathways.cohort_view");

        Map("SubjectTeacher",
            "marks.capture", "marks.view_subject", "assessment.create", "attendance.capture",
            "attendance.view_class", "report.draft", "communications.message_class", "discipline.log");

        Map("ClassTeacher",
            "attendance.capture", "attendance.view_class", "report.draft",
            "discipline.log", "discipline.escalate", "communications.message_class");

        Map("LOTeacher",
            "marks.capture", "attendance.capture", "pathways.advise", "pathways.cohort_view",
            "communications.message_class", "discipline.log");

        Map("SportCultureMIC",
            "attendance.capture", "communications.message_class", "discipline.log");

        Map("FinanceManager",
            "finance.view_all", "finance.create_invoice", "finance.capture_payment", "finance.refund",
            "finance.exempt_initiate", "finance.exempt_approve", "finance.reports",
            "finance.year_end", "finance.audit_pack");

        Map("BursarDebtorsClerk",
            "finance.view_all", "finance.create_invoice", "finance.capture_payment",
            "finance.exempt_initiate", "finance.reports");

        Map("Cashier",
            "finance.view_all", "finance.capture_payment");

        Map("ITAdministrator",
            "system.users_manage", "system.positions_assign", "system.integrations",
            "system.audit_log_view", "system.backup", "system.feature_flags");

        Map("Auditor",
            "finance.view_all", "finance.reports", "finance.audit_pack",
            "system.audit_log_view", "marks.view_all");

        Map("DistrictOfficial",
            "marks.view_all", "attendance.view_grade", "finance.reports");

        Map("SystemSupport",
            "system.audit_log_view");

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Perms} permissions, {Pos} positions, {Map} position-permission mappings.",
            permissions.Length, positions.Length, await db.PositionPermissions.CountAsync());
    }
}
