using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolPortal.Server.Authorization;
using Xunit;

namespace SchoolPortal.Tests.Security.PermissionContract;

/// <summary>
/// Step 10 Inventory A (part 1) — permission contract governance. Asserts every controller endpoint
/// carries the permission decision it SHOULD, catching drift (an attribute changed to the wrong key,
/// or a new endpoint with no/incorrect key).
///
/// INDEPENDENCE: <see cref="Expected"/> is a hand-declared map of the INTENDED decision per endpoint,
/// derived from the CLAUDE.md permission catalogue + the Step 6/Sprint-1.5.0 cluster decisions — it is
/// NOT generated from the attributes under test. The test reads the actual attributes via reflection
/// and compares to this independent declaration, so a wrong-key change diverges and fails. A new
/// endpoint missing from the map fails; a stale map entry fails. (Deny-by-default — an endpoint with
/// NO decision — is enforced separately by EndpointAuthorizationContractTests.)
/// </summary>
public class PermissionContractReflectionTests
{
    private const string Anon = "[anonymous]";
    private const string Super = "[superadmin]";

    private static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>
    {
        // Admin
        ["AdminController.RefreshViews"] = "system.refresh_views",
        // Auth / Sso / DevSeed / Billing webhook — anonymous (justified)
        ["AuthController.Login"] = Anon, ["AuthController.Refresh"] = Anon, ["AuthController.Test"] = Anon,
        ["SsoController.GoogleLogin"] = Anon, ["SsoController.GoogleCallback"] = Anon,
        ["SsoController.MicrosoftLogin"] = Anon, ["SsoController.MicrosoftCallback"] = Anon,
        ["DevSeedController.Seed"] = Anon,
        // Sprint 1.5.2: Matric Hub demo data for Greendale (dev-only, IsDevelopment-guarded).
        ["DevSeedController.SeedMatricDemo"] = Anon,
        ["BillingController.Webhook"] = Anon,
        ["PluginsController.Register"] = Anon,
        // Attendance
        ["AttendanceController.GetAttendance"] = "attendance.view_class",
        ["AttendanceController.GetMyAttendance"] = "attendance.view_own",
        ["AttendanceController.BulkUpsertAttendance"] = "attendance.capture",
        // Assignments
        ["AssignmentsController.GetAssignments"] = "platform.access",
        ["AssignmentsController.GetAssignment"] = "platform.access",
        ["AssignmentsController.CreateAssignment"] = "assessment.create",
        ["AssignmentsController.UpdateAssignment"] = "assessment.create",
        // Classes
        ["ClassesController.GetClasses"] = "platform.access",
        ["ClassesController.GetClass"] = "platform.access",
        ["ClassesController.CreateClass"] = "academics.manage",
        ["ClassesController.UpdateClass"] = "academics.manage",
        ["ClassesController.DeleteClass"] = "academics.manage",
        ["ClassesController.GetStudents"] = "marks.view_class",
        ["ClassesController.GetSubjects"] = "platform.access",
        // ClassSubjects (class-level academics.manage)
        ["ClassSubjectsController.BulkAssign"] = "academics.manage",
        ["ClassSubjectsController.GetTeachers"] = "academics.manage",
        // Activities
        ["ActivitiesController.GetAll"] = "activities.manage",
        ["ActivitiesController.GetMine"] = "platform.access",
        ["ActivitiesController.Create"] = "activities.manage",
        ["ActivitiesController.Update"] = "activities.manage",
        ["ActivitiesController.Delete"] = "activities.manage",
        ["ActivitiesController.GetParticipants"] = "activities.manage",
        ["ActivitiesController.AddParticipant"] = "activities.manage",
        ["ActivitiesController.RemoveParticipant"] = "activities.manage",
        // Calendar
        ["CalendarController.GetEvents"] = "platform.access",
        ["CalendarController.CreateEvent"] = "calendar.manage",
        ["CalendarController.DeleteEvent"] = "calendar.manage",
        ["CalendarController.GetTimetable"] = "platform.access",
        ["CalendarController.AddTimetableSlot"] = "timetable.manage",
        // Analytics (class-level analytics.view_school)
        ["AnalyticsController.GetOverview"] = "analytics.view_school",
        ["AnalyticsController.GetGradeDistribution"] = "analytics.view_school",
        ["AnalyticsController.GetAttendanceTrend"] = "analytics.view_school",
        ["AnalyticsController.GetAtRiskStudents"] = "analytics.view_school",
        ["AnalyticsController.GetClassPerformance"] = "analytics.view_school",
        ["AnalyticsController.GetRecentActivity"] = "analytics.view_school",
        // WhatsApp
        ["WhatsAppController.GetSettings"] = "communications.whatsapp_admin",
        ["WhatsAppController.UpdateSettings"] = "communications.whatsapp_admin",
        ["WhatsAppController.GetLog"] = "communications.whatsapp_admin",
        ["WhatsAppController.Compose"] = "communications.whatsapp_admin",
        ["WhatsAppController.SendTest"] = "communications.whatsapp_admin",
        ["WhatsAppController.SendAbsenceReminders"] = "communications.whatsapp_trigger",
        // Announcements
        ["AnnouncementsController.GetAnnouncements"] = "platform.access",
        ["AnnouncementsController.CreateAnnouncement"] = "announcements.publish",
        ["AnnouncementsController.UpdateAnnouncement"] = "announcements.publish",
        ["AnnouncementsController.DeleteAnnouncement"] = "announcements.publish",
        // Billing
        ["BillingController.GetSubscription"] = "platform.access",
        ["BillingController.CreateCheckoutSession"] = "school.manage",
        ["BillingController.CreatePortalSession"] = "school.manage",
        // Ai (class-level ai.use)
        ["AiController.SuggestGrade"] = "ai.use",
        ["AiController.GenerateQuestions"] = "ai.use",
        ["AiController.CheckPlagiarism"] = "ai.use",
        // Gradebook
        ["GradebookController.GetGradebook"] = "marks.view_class",
        ["GradebookController.GetMyGrades"] = "marks.view_own",
        ["GradebookController.GetMyAcademics"] = "marks.view_own",
        ["GradebookController.GetCategories"] = "marks.view_class",
        ["GradebookController.SetCategories"] = "assessment.create",
        // Sprint 1.5.2.5 — Marks Capture: reads follow the class-gradebook key; writes are
        // marks.capture (TC-1: capture stays with teaching roles, oversight cannot capture).
        ["GradebookController.GetCaptureTasks"] = "marks.view_class",
        ["GradebookController.GetTaskMarks"] = "marks.view_class",
        ["GradebookController.BulkCapture"] = "marks.capture",
        ["GradebookController.CreateTask"] = "marks.capture",
        ["GradebookController.UpdateTask"] = "marks.capture",
        // Fees
        ["FeesController.GetFees"] = "finance.view_all",
        ["FeesController.CreateFee"] = "finance.create_invoice",
        ["FeesController.GetFee"] = "finance.view_all",
        ["FeesController.UpdateFee"] = "finance.create_invoice",
        ["FeesController.DeleteFee"] = "finance.create_invoice",
        ["FeesController.GetPayments"] = "finance.view_all",
        ["FeesController.RecordPayment"] = "finance.capture_payment",
        ["FeesController.GetMyStatement"] = "finance.view_own",
        // Enrolments (class-level system.users_manage)
        ["EnrolmentsController.BulkEnroll"] = "system.users_manage",
        // Me
        ["MeController.GetMe"] = "platform.access",
        // Grades (class-level marks.capture)
        ["GradesController.CreateGrade"] = "marks.capture",
        ["GradesController.BulkGrade"] = "marks.capture",
        // Courses
        ["CoursesController.GetCourses"] = "platform.access",
        ["CoursesController.GetCourse"] = "platform.access",
        ["CoursesController.CreateCourse"] = "courses.manage",
        ["CoursesController.PublishCourse"] = "courses.manage",
        ["CoursesController.DeleteCourse"] = "courses.manage",
        ["CoursesController.AddModule"] = "courses.manage",
        ["CoursesController.DeleteModule"] = "courses.manage",
        ["CoursesController.ReorderModules"] = "courses.manage",
        ["CoursesController.AddLesson"] = "courses.manage",
        ["CoursesController.UpdateLesson"] = "courses.manage",
        ["CoursesController.DeleteLesson"] = "courses.manage",
        ["CoursesController.ReorderLessons"] = "courses.manage",
        // Pathways
        ["PathwaysController.GetLearnerSubjects"] = "platform.access",
        ["PathwaysController.GetMySubjects"] = "pathways.view_own",
        ["PathwaysController.GetClassMatrix"] = "marks.view_class",
        ["PathwaysController.Enrol"] = "academics.manage",
        ["PathwaysController.Withdraw"] = "academics.manage",
        ["PathwaysController.GetUniversities"] = "platform.access",
        ["PathwaysController.GetUniversityCourses"] = "platform.access",
        ["PathwaysController.GetCareers"] = "platform.access",
        ["PathwaysController.GetMyAps"] = "pathways.view_own",
        ["PathwaysController.GetMyGoals"] = "pathways.view_own",
        ["PathwaysController.AddGoal"] = "pathways.view_own",
        ["PathwaysController.DeleteGoal"] = "pathways.view_own",
        ["PathwaysController.GetGoalTracking"] = "pathways.view_own",
        ["PathwaysController.GetGapAnalysis"] = "pathways.view_own",
        ["PathwaysController.GetGr9Profile"] = "pathways.view_own",
        ["PathwaysController.GetGr9Advice"] = "pathways.view_own",
        // Sprint 1.5.1 Gap 3: subject-name diagnostics — Principal/Deputy/HOD + ITAdministrator.
        ["PathwaysController.GetSubjectMatchReport"] = "academics.diagnostics",
        // Matric
        ["MatricController.GetDashboard"] = "marks.view_class",
        // Sprint 1.5.2 Week 2: staff risk views — class-scoped via IScopeService.
        ["MatricController.GetRiskDashboard"] = "marks.view_class",
        ["MatricController.GetGradeOverview"] = "marks.view_class",
        ["MatricController.GetMine"] = "marks.view_own",
        ["MatricController.GetSubjects"] = "platform.access",
        ["MatricController.GetPastPapers"] = "platform.access",
        // Sprint 1.5.2 Step 2: static NSC-requirements catalogue (national policy, no PII).
        ["MatricController.GetNscRequirements"] = "platform.access",
        // Sprint 1.5.2 Step 4: study planner reads the caller's OWN averages → marks.view_own.
        ["MatricController.GetStudyPlan"] = "marks.view_own",
        ["MatricController.GetQuiz"] = "platform.access",
        // Sprint 1.5.2 Step 3: tutor v2 — ai.tutor (Learner identity-implicit + the
        // marks.view_class staff cluster), no longer bare platform.access.
        ["MatricController.AskTutor"] = "ai.tutor",
        // Schools
        ["SchoolsController.GetCurrentSchool"] = "platform.access",
        ["SchoolsController.UpdateInfo"] = "school.manage",
        ["SchoolsController.UpdateTheme"] = "school.manage",
        ["SchoolsController.UpdateFeatures"] = "system.feature_flags",
        ["SchoolsController.GetSettings"] = "school.manage",
        ["SchoolsController.UpdateSettings"] = "school.manage",
        ["SchoolsController.ApplySizePreset"] = "school.manage",
        ["SchoolsController.SeedCapsSubjects"] = "school.manage",
        // Users
        ["UsersController.GetUsers"] = "system.users_manage",
        ["UsersController.GetDirectory"] = "platform.access",
        ["UsersController.GetImportCsvTemplate"] = "system.users_manage",
        ["UsersController.GetStaffImportTemplate"] = "system.users_manage",
        ["UsersController.ImportStaffCsv"] = "system.users_manage",
        ["UsersController.ImportCsv"] = "system.users_manage",
        ["UsersController.CreateUser"] = "system.users_manage",
        ["UsersController.UpdateUser"] = "system.users_manage",
        ["UsersController.DeleteUser"] = "system.users_manage",
        // Parent
        ["ParentController.GetChildren"] = "platform.access",
        ["ParentController.GetChildGrades"] = "marks.view_child",
        ["ParentController.GetChildAttendance"] = "attendance.view_child",
        ["ParentController.GetChildAssignments"] = "platform.access",
        ["ParentController.GetSchoolAnnouncements"] = "platform.access",
        ["ParentController.GetPathways"] = "pathways.view_child",
        // Popia
        ["PopiaController.GetMyConsents"] = "platform.access",
        ["PopiaController.UpdateConsents"] = "platform.access",
        ["PopiaController.GetMyRequests"] = "platform.access",
        ["PopiaController.SubmitRequest"] = "platform.access",
        ["PopiaController.AdminGetConsents"] = "system.popia_admin",
        ["PopiaController.AdminGetRequests"] = "system.popia_admin",
        ["PopiaController.AdminUpdateRequest"] = "system.popia_admin",
        // Quizzes
        ["QuizzesController.GetQuizzes"] = "platform.access",
        ["QuizzesController.GetQuiz"] = "platform.access",
        ["QuizzesController.CreateQuiz"] = "assessment.create",
        ["QuizzesController.PublishQuiz"] = "assessment.create",
        ["QuizzesController.DeleteQuiz"] = "assessment.create",
        ["QuizzesController.StartAttempt"] = "assignments.submit",
        ["QuizzesController.SubmitAttempt"] = "assignments.submit",
        ["QuizzesController.GetMyAttempts"] = "assignments.view_assigned",
        ["QuizzesController.GetAllAttempts"] = "marks.view_class",
        // Progress
        ["ProgressController.CompleteLesson"] = "platform.access",
        ["ProgressController.GetCourseProgress"] = "platform.access",
        ["ProgressController.GetAllStudentsProgress"] = "marks.view_class",
        ["ProgressController.GetLearningPaths"] = "platform.access",
        // Messages
        ["MessagesController.GetThreads"] = "platform.access",
        ["MessagesController.GetMessages"] = "platform.access",
        ["MessagesController.SendMessage"] = "platform.access",
        ["MessagesController.CreateDirectThread"] = "platform.access",
        ["MessagesController.CreateClassDiscussion"] = "communications.message_class",
        // Skills
        ["SkillsController.GetMine"] = "platform.access",
        ["SkillsController.GetLearnerSkills"] = "skills.endorse",
        ["SkillsController.Create"] = "platform.access",
        ["SkillsController.Delete"] = "platform.access",
        ["SkillsController.Endorse"] = "skills.endorse",
        // Notifications (class-level platform.access)
        ["NotificationsController.GetNotifications"] = "platform.access",
        ["NotificationsController.MarkRead"] = "platform.access",
        ["NotificationsController.MarkAllRead"] = "platform.access",
        // Terms (class-level platform.access)
        ["TermsController.GetTerms"] = "platform.access",
        ["TermsController.GetCurrentTerm"] = "platform.access",
        // Subjects
        ["SubjectsController.GetSubjects"] = "platform.access",
        ["SubjectsController.GetSubject"] = "platform.access",
        ["SubjectsController.CreateSubject"] = "academics.manage",
        ["SubjectsController.UpdateSubject"] = "academics.manage",
        ["SubjectsController.DeleteSubject"] = "academics.manage",
        // Positions (class-level system.positions_assign)
        ["PositionsController.GetCatalogue"] = "system.positions_assign",
        ["PositionsController.GetOverview"] = "system.positions_assign",
        ["PositionsController.GetUserAssignments"] = "system.positions_assign",
        ["PositionsController.Assign"] = "system.positions_assign",
        ["PositionsController.Update"] = "system.positions_assign",
        ["PositionsController.Revoke"] = "system.positions_assign",
        // Reports (class-level reporting.view; principal-summary ANDs a method-level key)
        ["ReportsController.GetTermReport"] = "reporting.view",
        ["ReportsController.GetAtRisk"] = "reporting.view",
        ["ReportsController.GetReportComment"] = "reporting.view",
        ["ReportsController.GetPrincipalSummary"] = "reporting.principal_summary",
        ["ReportsController.GetAttendanceSummary"] = "reporting.view",
        ["ReportsController.GetGradebookSimple"] = "reporting.view",
        // Sprint 1.5.3 Smart Reports v1 role views (on the shared at-risk primitive). Grade Head /
        // HOD views are scoped class reads → marks.view_class; the Principal school-wide overview
        // is analytics.view_school (Sensitive).
        ["SmartReportsController.GetGradeView"] = "marks.view_class",
        ["SmartReportsController.GetSubjectView"] = "marks.view_class",
        ["SmartReportsController.GetSchoolOverview"] = "analytics.view_school",
        // SaSams (class-level system.data_export)
        ["SaSamsController.ExportLearners"] = "system.data_export",
        ["SaSamsController.ExportAttendance"] = "system.data_export",
        ["SaSamsController.ExportResults"] = "system.data_export",
        // Plugins
        ["PluginsController.GetMarketplace"] = "platform.access",
        ["PluginsController.GetInstalled"] = "system.integrations",
        ["PluginsController.Install"] = "system.integrations",
        ["PluginsController.Uninstall"] = "system.integrations",
        ["PluginsController.Dispatch"] = "system.integrations",
        // SuperAdmin (separate platform scheme)
        ["SuperAdminController.Login"] = Anon,
        ["SuperAdminController.GetStats"] = Super,
        ["SuperAdminController.GetSchools"] = Super,
        ["SuperAdminController.CreateSchool"] = Super,
        ["SuperAdminController.UpdateFeatures"] = Super,
        ["SuperAdminController.SetStatus"] = Super,
        ["SuperAdminController.GetAuditLog"] = Super,
        // Submissions
        ["SubmissionsController.CreateSubmission"] = "assignments.submit",
        ["SubmissionsController.GetSubmissionsByAssignment"] = "marks.view_class",
        ["SubmissionsController.GetPending"] = "marks.view_class",
        ["SubmissionsController.GetMySubmission"] = "assignments.view_assigned",
    };

    private static readonly Type[] Verbs =
    {
        typeof(HttpGetAttribute), typeof(HttpPostAttribute), typeof(HttpPutAttribute),
        typeof(HttpPatchAttribute), typeof(HttpDeleteAttribute),
    };

    [Fact]
    public void EveryEndpoint_CarriesItsExpectedPermissionDecision()
    {
        var actual = new Dictionary<string, string>();
        foreach (var controller in typeof(Program).Assembly.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            foreach (var m in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!m.GetCustomAttributes().Any(a => Verbs.Contains(a.GetType()))) continue;
                actual[$"{controller.Name}.{m.Name}"] = ResolveDecision(controller, m);
            }
        }

        var problems = new List<string>();
        foreach (var (key, act) in actual.OrderBy(k => k.Key))
        {
            if (!Expected.TryGetValue(key, out var exp))
                problems.Add($"{key}: endpoint NOT in expected map (new/unreviewed) — actual decision '{act}'");
            else if (exp != act)
                problems.Add($"{key}: DRIFT — expected '{exp}', actual '{act}'");
        }
        foreach (var key in Expected.Keys)
            if (!actual.ContainsKey(key))
                problems.Add($"{key}: in expected map but no such endpoint (stale entry)");

        Assert.True(problems.Count == 0, $"{problems.Count} permission-contract problem(s):\n  " + string.Join("\n  ", problems));
    }

    // Resolves an endpoint's primary authorization decision: method-level wins over class-level.
    private static string ResolveDecision(Type controller, MethodInfo m)
    {
        var mPerm = m.GetCustomAttribute<RequirePermissionAttribute>();
        if (mPerm is not null) return mPerm.PermissionKey;
        if (m.GetCustomAttribute<RequireSuperAdminAttribute>() is not null) return Super;
        if (m.GetCustomAttribute<AllowAnonymousAttribute>() is not null) return Anon;

        var cPerm = controller.GetCustomAttribute<RequirePermissionAttribute>();
        if (cPerm is not null) return cPerm.PermissionKey;
        if (controller.GetCustomAttribute<RequireSuperAdminAttribute>() is not null) return Super;
        if (controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null) return Anon;

        return "[none]";
    }
}
