# SchoolPort — Security Model & Controls

_Last updated: 2026-06-19 (Sprint 1.5.0 closeout). Audience: school IT staff, pilot administrators, and auditors. This document describes how SchoolPort protects each school's data — especially minors' academic, attendance, and financial records — and is written to be honest about both what is enforced and what is not yet._

SchoolPort is a multi-tenant platform: many schools share one database, and **a school must never see another school's data**. Authorization is enforced in the application (ASP.NET Core 8 Web API). The source of truth for the permission catalogue is code (`SchoolPortal.Server/Seeds/PositionsSeedData.cs` and `Authorization/PermissionKeys.cs`), synced to the database on startup; this document summarizes and points to it rather than duplicating the grant matrix (which would drift).

---

## 1. The three-layer authorization model

Every request is authorized by three independent layers. All three must pass.

1. **Identity (Layer 1) — _who are you?_** Every user has exactly one of five identities baked into their signed JWT: `Learner`, `Parent`, `Staff`, `External`, `System`. Some permissions are *identity-implicit* (granted by identity alone, no appointment needed) — e.g. a Learner may always read their **own** marks (`marks.view_own`); a Parent may read **their child's** marks (`marks.view_child`). Staff/External/System hold only the baseline `platform.access` implicitly; all their domain powers come from positions. (`Authorization/PermissionKeys.cs` → `IdentityImplicit`.)

2. **Positions (Layer 2) — _what is your appointment?_** Staff hold one or more **positions** (Principal, DeputyPrincipal, HOD, SubjectTeacher, FinanceManager, BursarDebtorsClerk, ITAdministrator, etc.). Each position maps to a set of **permissions**. Positions can have effective-from/to dates; an expired appointment grants nothing.

3. **Permissions (Layer 3 enforcement) — _is this action allowed, on this resource?_** Endpoints declare the permission they require with `[RequirePermission("...")]`. Beyond *holding* a permission, **scope** is enforced: a SubjectTeacher may hold `marks.view_class` but may only read **their own** classes — `IScopeService` filters queries and rejects cross-resource access (IDOR) with `403`.

**Deny-by-default.** The global fallback policy requires an authenticated user, and a governance test (below) fails the build if any endpoint omits an explicit decision. There is no "allow by default" path.

### Token issuance & lifetime
Access tokens are issued by the real login path (`AuthService`), signed, and carry identity + positions + the resolved permission set. Lifetimes are **tiered by sensitivity** (`AuthService`): **8 hours** baseline, **1 hour** if the user holds a Finance or External position, **30 minutes** for System identities — and a token **never outlives** a time-limited External/System appointment. Refresh tokens are stored only as SHA-256 hashes, are single-use (rotated on refresh), and re-read positions from the database so a revoked appointment propagates promptly.

### Sensitive permissions are re-checked against the database
Routine permissions resolve from the JWT (fast, zero DB hits). A **Sensitive set** (`PermissionKeys.Sensitive`) is always re-resolved from the database per request, ignoring the cached token — so revoking one of these takes effect immediately, not when the token expires. It covers Finance writes and bulk reads (`finance.create_invoice`, `capture_payment`, `refund`, `exempt_initiate`, `exempt_approve`, `year_end`, `audit_pack`, `view_all`, `reports`), privileged System actions (`backup`, `positions_assign`, `users_manage`, `data_export`, `popia_admin`, `refresh_views`), school-wide `analytics.view_school`, and `reporting.principal_summary`.

---

## 2. The permission catalogue

The catalogue is defined and grant-mapped in **`SchoolPortal.Server/Seeds/PositionsSeedData.cs`** (positions → permissions) and keyed in **`Authorization/PermissionKeys.cs`**, synced to the live database on server start. It is **the single source of truth** — treat any list elsewhere (including this file) as explanatory.

Permissions are grouped by cluster:
- **Teaching** — `marks.view_class/capture/view_own/view_child`, `attendance.*`, `assessment.create`, `assignments.submit/view_assigned`, `courses.manage`, `reporting.view`.
- **Comms / social** — `announcements.publish`, `calendar.manage`, `timetable.manage`, `activities.manage`, `skills.endorse`, `communications.message_class/whatsapp_admin/whatsapp_trigger`.
- **Admin / system** — `school.manage`, `academics.manage`, `system.users_manage/positions_assign/integrations/feature_flags/data_export/popia_admin/refresh_views`, `ai.use`.
- **Finance** — `finance.view_all/view_own/create_invoice/capture_payment/refund/exempt_initiate/exempt_approve/reports/pay`.
- **Analytics / reporting** — `analytics.view_school`, `reporting.view`, `reporting.principal_summary`.
- **Baseline** — `platform.access` (every authenticated identity; backs "any logged-in user" endpoints such as `/api/me`).

Deliberate scope decisions worth noting for an auditor:
- **Oversight ≠ authoring.** Principals/Deputies hold oversight reads but **not** `marks.capture` / `attendance.capture` / `assessment.create` — an admin who must capture marks needs an explicit teaching appointment.
- **Rank-and-file teachers** do not get school-wide `analytics.view_school` — that is SMT/academic-oversight only.

---

## 3. Cross-tenant & IDOR write hardening (the 19 endpoints)

The most dangerous bug class in a shared database: a write that accepts an **id from the request** (a class, student, teacher, term, fee…) and links it without checking it belongs to the caller's school — because the database foreign key resolves across tenants regardless. The Step 10 audit found and fixed **19 such endpoints** (commit `aed1068a` + follow-ups): **18 cross-tenant** (a foreign route/body id) **+ 1 cross-user** (`SubmitAttempt`).

| # | Endpoint | Validated id | Type |
|---|----------|--------------|------|
| 1 | `CalendarController.CreateEvent` | ClassId | cross-tenant |
| 2 | `CalendarController.AddTimetableSlot` | ClassSubjectId | cross-tenant |
| 3 | `FeesController.CreateFee` | TermId | cross-tenant |
| 4 | `FeesController.UpdateFee` | TermId | cross-tenant |
| 5 | `FeesController.RecordPayment` | StudentId (money across tenants) | cross-tenant |
| 6 | `MessagesController.CreateDirectThread` | RecipientUserId | cross-tenant |
| 7 | `MessagesController.CreateClassDiscussion` | ClassId | cross-tenant |
| 8 | `PathwaysController.Enrol` | StudentId + SubjectId | cross-tenant |
| 9 | `ProgressController.CompleteLesson` | LessonId | cross-tenant |
| 10 | `AttendanceController.BulkUpsertAttendance` | StudentIds | cross-tenant |
| 11 | `ClassesController.CreateClass` | TeacherId | cross-tenant |
| 12 | `ClassesController.UpdateClass` | TeacherId | cross-tenant |
| 13 | `EnrolmentsController.BulkEnroll` | ClassIds + StudentIds | cross-tenant |
| 14 | `CoursesController.CreateCourse` | ClassSubjectId | cross-tenant |
| 15 | `CoursesController.ReorderModules` | CourseId | cross-tenant (IDOR) |
| 16 | `CoursesController.ReorderLessons` | ModuleId | cross-tenant (IDOR) |
| 17 | `QuizzesController.CreateQuiz` | ClassSubjectId | cross-tenant |
| 18 | `SubmissionsController.CreateSubmission` | AssignmentId | cross-tenant |
| 19 | `QuizzesController.SubmitAttempt` | AttemptId → caller's own UserId | **cross-user** |

### The rule (binding requirement for all future endpoints)
> **Any mutating endpoint that accepts a tenant-owned id in its body or route MUST validate that the id belongs to the caller's school (and, where applicable, the caller) before performing the write.** A foreign id must yield `404`/`403` with no row mutated. The database foreign key does **not** enforce this — it resolves across tenants.

### Regression prevention — the scanner
`CrossTenantGuardScannerTests` (in `SchoolPortal.Tests`) reflects over **every** id-bearing mutating endpoint and **fails CI** if one lacks a cross-tenant guard test (`EveryIdBearingMutatingEndpoint_HasACrossTenantGuardTest`). A second governance test, `EndpointAuthorizationContractTests`, fails the build if any endpoint lacks an explicit `[RequirePermission]` / justified `[AllowAnonymous]` / `[RequireSuperAdmin]` decision. A reflection test (`PermissionContractReflectionTests`) asserts each endpoint carries its expected permission. The full suite (170 tests, run against real PostgreSQL in CI with **zero exclusions**) must be green to merge.

---

## 4. Segregation of Duties (Finance)

The Step 6 finance audit corrected segregation-of-duties violations in the original seed (applied to the live database 2026-06-14):
- **FinanceManager** lost `exempt_approve` (could both initiate and approve exemptions).
- **BursarDebtorsClerk** lost `create_invoice` and `exempt_initiate` (a capture-and-chase role, not fee creation).
- After: FinanceManager/Bursar **initiate** exemptions; SMT (Principal + DeputyPrincipal) **approve**; only FinanceManager **creates** fees. Principal/Deputy retain finance oversight but **lose operational fee writes** (`create_invoice`, `capture_payment`).

Two properties are verified by tests (`SeedSyncTests`): the SoD revocations are **idempotently enforced** on every seed sync (delete-if-present, so the live DB and a fresh install converge), and the behavioral matrix proves a Bursar is **denied** `finance.create_invoice` (`403`).

---

## 5. Database-layer posture (RLS) — honest statement

**Tenant isolation is enforced entirely in the application layer, by deliberate decision for the pilot.** The application connects to PostgreSQL as a role (`postgres`) that **bypasses** row-level security; therefore RLS policies cannot take effect for the app without a non-owner connection role plus a `schoolId` claim threaded through every query — a real architecture change. RLS is *enabled* on all tables but carries no policies, which serves only as a default-deny backstop for non-app roles.

We accept application-layer-only isolation for the pilot because it is the **tested** control: the 19 guards above, `IScopeService` IDOR enforcement, the scanner, and 170 green tests. **Database-layer RLS as defense-in-depth is logged as a pre-scale hardening item**, not a pre-pilot blocker. (See _Known limitations_.)

### Supabase Data API exposure — found and fixed before pilot
The hosting provider (Supabase) exposes an optional auto-generated REST API (`/rest/v1/`) reachable with a public "anon" key. An audit on 2026-06-19 found that five aggregate views/materialized views (`vw_matric_aps_summary`, `vw_school_performance_summary`, `vw_subject_term_averages`, `vw_attendance_summary`, `vw_gradebook_simple`) were readable by the anon role — leaking **cross-school learner APS and performance aggregates**, bypassing the entire permission model. **This was remediated before pilot** (`migrations/006_restrict_data_api.sql`): the API roles were stripped of all access to the application schema, the two SECURITY DEFINER views were converted to honor the caller, and a privileged helper function was locked down. The application is unaffected (it does not use the Data API). 

**Evidence:** before the fix, `GET /rest/v1/vw_matric_aps_summary` with the anon key returned **HTTP 200 with real cross-school student rows**; after the fix it returns **HTTP 401 — permission denied**. Supabase's security advisors confirm the related findings cleared.

---

## 6. Audit & accountability

Writes record to an append-only `audit_logs` table including the acting user, the authorizing position, and the permission used. SuperAdmin (platform operator, across all schools) is outside the per-school model and uses a separate authentication scheme (`[RequireSuperAdmin]`); its endpoints are skipped by tenant middleware and are not reachable with a normal school token (verified by test).

---

## 7. Known limitations & deferred hardening

Honest list of current edges. None is a silent gap; each is tracked.

- **Database-layer RLS is not in force (pre-scale).** Tenant isolation is application-layer only (Section 5). Adding RLS defense-in-depth requires a non-owner DB connection role + `schoolId` as a request claim every policy keys on. Deferred to a pre-scale hardening sprint.
- **`submissions` storage bucket is public (pre-pilot follow-up).** Student work files are served from a **public** bucket, so a file is readable by anyone with its exact (GUID-based) URL — security-by-obscurity, which is not POPIA-clean for minors' submissions. The bulk-listing exposure was removed in `006`, but the proper fix (private bucket + short-lived signed URLs, requiring a `StorageService` change) is a tracked, separately-scoped follow-up.
- **Class-metadata read is coarse.** `GET /api/classes/{id}` is gated by `platform.access` + SchoolId only (not per-teacher scope) — low severity (school-scoped, non-sensitive metadata); logged, not fixed.
- **Assignment _creation_ is not scope-checked beyond the tenant (intra-tenant write-scope gap).** `AssignmentService.CreateAssignmentAsync` validates the `ClassSubjectId` belongs to the caller's school but does **not** check the teacher actually teaches it — so a holder of `assessment.create` can create an assignment against **any class-subject in their own school**, not only their own. No cross-school leak and no data exposure, but it is a write a teacher can perform outside their taught class-subjects. Deferred to a scope-refinement sprint (would add an `IScopeService` class-subject check on create, mirroring the grade-write gating).
- **Oversight read of a non-existent class returns `200 []` rather than `404`.** No data leak (empty result); a cosmetic inconsistency on the optimized roster path.

---

## 8. For developers

- Add `[RequirePermission("...")]` to every new endpoint; the governance test enforces it.
- For any mutating endpoint taking a tenant-owned id, **validate ownership before writing** (Section 3 rule) and add a `[CrossTenantGuard]` test — the scanner enforces this.
- The permission catalogue lives in `PositionsSeedData.cs` / `PermissionKeys.cs`; changes sync to the database on startup. Do not hand-edit grants in the database.
- Run the full test suite (real PostgreSQL, zero exclusions) before merging.
