# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Product Strategy

SchoolPort is a school management platform for South African high schools. The product is organised around **5 pillars**, each controlled by feature flags stored in `School.Features` (jsonb). Core LMS features (assignments, attendance, quizzes, gradebook view, announcements, SignalR notifications) are always-on and not flag-gated.

### 5 Pillars & Feature Flags

| Pillar | Feature flags | Description |
|---|---|---|
| **Classroom** | `gradebook`, `virtualClassroom` | CAPS-aligned gradebook, virtual classroom / video lessons |
| **Reports & Insights** | `smartReports`, `saSamsExport` | AI-generated progress reports, SA-SAMS data export |
| **Pathways** | `skillsProfile`, `pathways`, `matricHub` | Learner skills portfolio, subject pathway planner, Matric prep hub |
| **Life at School** | `sportsCulture`, `schoolPay` | Sports & cultural activities, school fee payments (ZAR) |
| **Connect** | `schoolChat`, `whatsApp`, `popiaCentre` | In-app chat, WhatsApp integration, POPIA compliance centre |

Flag names are camelCase strings, matched exactly in `School.Features` jsonb, `SchoolFeatures` C# class, `SchoolFeatures` TypeScript interface, and the `useFeature()` hook.

### Anti-Clutter Principles

- No UI surface without a corresponding task in the current phase.
- No config options added "in case we need them."
- No features outside phase scope — log them as Phase 4 backlog items in this file instead.
- Hidden-behind-flag is preferred over removed; removing a nav item makes it invisible to future phases.
- Three similar lines of code beats a premature abstraction.

---

## Phased Roadmap

### Phase 0 — Foundation (2 weeks) ✅ In progress
Lays groundwork. No new user-facing features shipped.

1. School onboarding wizard — guided setup (school details, branding, pillar toggles, CSV import).
2. Feature flag enforcement — `useFeature(name)` hook; sidebar + route audit per role.
3. Academic calendar data model — `AcademicYear` and `Term` EF entities; migration `AddAcademicCalendar`.
4. CAPS subject pre-seeding — Senior Phase (Gr 7–9) and FET (Gr 10–12) subject list with `CapsPhase`.
5. Sidebar audit — enforce role item caps (Teacher ≤ 6, Learner ≤ 7, Parent ≤ 7, Admin ≤ 7).
6. Service file split — `AdditionalServices.cs` → one file per service class.
7. Documentation pass — keep this file current as work completes.

### Phase 1 — Core Pillars v1
CAPS Gradebook, Smart Reports v1, Pathways v1, SchoolPay v1.

#### Phase 1.5 — Make It Real (sequential sprints)

| # | Sprint | Status |
|---|---|---|
| 1.5.1 | Pathways v1 | ✅ Complete (2026-06-02) |
| 1.5.2 | Matric Hub v1 + Grade 9 Subject Advisor | ✅ Complete (2026-06-02) |
| 1.5.3 | Smart Reports v1 | ✅ Complete (2026-06-02) |
| 1.5.4 | SchoolPay v1 (PayFast) | ⬜ Not started |
| 1.5.5 | WhatsApp v1 (Meta Cloud API) | ⬜ Not started |

### Phase 2 — Engagement
Virtual Classroom, Skills Profile, Sports & Culture, SchoolChat.

### Phase 3 — Compliance & Integration
WhatsApp integration, POPIA Centre, SA-SAMS export, Matric Hub.

### Phase 4 Backlog
_Log future ideas here rather than implementing out of scope._

### Known improvements (Step 8 follow-ups)
- **My Academics → My Marks subject selector:** switch the pill-tabs vs dropdown threshold from "≤4 → pills" to "≤5 → pills, >5 → dropdown" (`components/my-academics/MarksTab.tsx`).

### Materialized views & refresh (Sprint 1.5.0.5 → follow-ups)
- **`vw_subject_term_averages`, `vw_matric_aps_summary`, `vw_school_performance_summary`** — refreshed **manually only** via `POST /api/admin/refresh-views` (`system.refresh_views`, Sensitive). NOT refreshed on grade save.
- **Sprint 1.5.3 — debounced background refresh:** add a background job that refreshes the views at most once per N minutes *when grades have changed*, so Smart Reports reads stay reasonably fresh without thrashing under bulk mark capture. The proper pattern, deferred until Smart Reports needs live freshness.
- **Smart Reports at-risk FLAGGING threshold:** `vw_school_performance_summary` uses **40%** for its pass-rate / at-risk-count (CAPS minimum). Smart Reports (Sprint 1.5.3) intervention flagging should use a **separate, higher threshold (~50%)** — a learner above the pass line can still need intervention. Keep the two distinct; do not change the view's 40% pass calculation.
- **`vw_matric_aps_summary.projected_aps` is a PROJECTION**, not an official APS (flat sum of CAPS-code points from year-averages — no LO exclusion / best-6 / promotion-mark weighting). Sprint 1.5.1 adds proper weighting; nothing downstream may treat it as final.
- **APS read source is per-surface (Sprint 1.5.1 decision):** `PathwaysService.GetLearnerApsAsync` is a **live** calculation (correlated per-subject grade average) — parent/learner pathways dashboards need current marks, so they must NOT read `vw_matric_aps_summary` (manual-refresh only → can be stale). Sprint 1.5.1 should decide per surface: live calculation for dashboards needing current data; `vw_matric_aps_summary` for heavier aggregates/reporting where slight staleness is acceptable.

### Deferred indexes — Sprint 1.5.4
Requested in the Sprint 1.5.0.5 indexing sprint but **not creatable against the current normalized schema** — add when the finance/marks model lands in Sprint 1.5.4:
- **`grades` (school_id, class_id, term_id)**, **(school_id, student_id, term_id)**, **(school_id, subject_id, term_id)** — `grades` has none of `class_id`/`term_id`/`student_id`/`subject_id`; marks are reached via `grade → submission → assignment → class_subject → class/subject` and term via `assignment.due_at`. (The hot path is instead served by `ix_assignments_school_class_subject_due` from 1.5.0.5.) If marks queries become a bottleneck, consider a denormalized marks projection or a covering index on the join path.
- **`submissions` (school_id, student_id, status)** — `submissions` has no `status` column (submitted/graded is derived from grade presence). Add if/when a materialized submission status is introduced.
- **`fees`/invoices (school_id, student_id, status)** — `fees` are school-level templates (no per-student row, no status). Needs the Sprint 1.5.4 per-student invoice model first.

---

## South African Context

- **Currency:** ZAR (R) — never USD.
- **Spelling:** South African English — `organisation`, `colour`, `behaviour`, `centre`, `learner` (not "student" in user-facing copy), `programme`.
- **Date format in UI:** dd/mm/yyyy — database stores ISO 8601.
- **Grading:** CAPS percentage scale (0–100). Senior Phase uses levels 1–7; FET uses percentage with distinction at 80%+.
- **Academic year:** Four terms (Term 1–4). Only one `Term.IsCurrent = true` per school at a time.
- **CAPS phases:** `SeniorPhase` (Gr 7–9), `FET` (Gr 10–12).

---

## Per-Role Navigation Contracts

These caps are hard limits enforced in the sidebar. Anything beyond the cap must be feature-flagged off or omitted.

| Role | Max nav items | Core items (always visible) |
|---|---|---|
| **Admin** | 7 | Dashboard, Classes, Users, Announcements, Settings, Setup Wizard + 1 flagged |
| **Teacher** | 6 | Dashboard, Classes, Assignments, Attendance, Announcements + 1 flagged |
| **Learner** | 7 | Dashboard, Classes, Assignments, Quizzes, Announcements, Calendar + 1 flagged |
| **Parent** | 7 | Dashboard, Announcements, Calendar + up to 4 flagged |

---

## Phase 0 Constraints Introduced

| Constraint | Detail |
|---|---|
| `useFeature(name)` hook | Lives in `schoolportal-web/lib/use-feature.ts`. Reads `school.features` from the dashboard layout context. Returns `boolean`. |
| Migration name | `AddAcademicCalendar` — covers `AcademicYear`, `Term` tables and `Subject.CapsPhase` column. |
| `CapsPhase` values | String column, values: `"SeniorPhase"` or `"FET"`. Nullable — null means applies to both or phase not applicable. |
| CAPS seeding | Subjects seeded at school-creation time via `SchoolsController` / `SuperAdminController`. Per-school — schools can edit their subject list freely. |
| Service file locations | One file per service class under `SchoolPortal.Server/Services/`. Interface and implementation in same file, matching the existing pattern. |

---

## Repository Layout

```
SchoolPort/
├── SchoolPortal.Server/     ASP.NET Core 8 Web API
├── SchoolPortal.Data/       EF Core entities, DbContext, migrations
├── SchoolPortal.Shared/     DTOs shared between server and (Blazor) client
├── SchoolPortal.Client/     Blazor WebAssembly (legacy, largely unused)
├── SchoolPortal.Tests/      xUnit tests (unit + integration)
├── schoolportal-web/        Next.js 16 + Tailwind primary frontend
└── PostgresSetup.sql        Supabase Postgres schema + seed data
```

## Backend Commands (run from repo root)

```bash
# Build entire solution
dotnet build SchoolPortal.sln

# Run the API (listens on http://localhost:5128)
cd SchoolPortal.Server && dotnet run

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~AssignmentServiceTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~AssignmentServiceTests.CreateAssignment_ShouldReturnDto"

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project SchoolPortal.Data --startup-project SchoolPortal.Server

# Apply migrations
dotnet ef database update --project SchoolPortal.Data --startup-project SchoolPortal.Server
```

## Frontend Commands (run from `schoolportal-web/`)

```bash
yarn dev          # dev server on http://localhost:3000
yarn build        # production build
yarn lint         # ESLint
```

The dev server must be started via a **detached process** (e.g. `Start-Process cmd /c "yarn dev"`) because `yarn dev` in a background shell exits immediately while Node continues running. Port 3000 is the target; if it reports in-use, find the stale PID with `netstat -ano | findstr :3000` and kill it first.

## Secrets / Configuration

Real credentials are **never** in `appsettings.json` — they carry `CHANGE_ME_USE_USER_SECRETS` placeholders. Supply via:

```bash
cd SchoolPortal.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"
dotnet user-secrets set "Anthropic:ApiKey" "<key>"
```

Or set env var `CONNECTIONSTRINGS__DEFAULTCONNECTION` at runtime.

## Backend Architecture

### Multi-tenancy
Every authenticated request carries a `schoolId` JWT claim. `TenantMiddleware` (runs after auth, before authorization) reads this claim and stores it in `HttpContext.Items["SchoolId"]`. All services use `ICurrentUserService` to get `SchoolId`, `UserId`, and `Role` — never pass tenant IDs through request parameters.

### Service pattern
Controllers are thin. Business logic lives in services injected by interface. Services are in `SchoolPortal.Server/Services/`:
- `AdditionalServices.cs` — hosts multiple smaller service classes in one file: School, Class, Subject, Grade, Submission, Announcement, etc.
- Separate files for larger services: Auth, User, Assignment, Attendance, Course, Quiz, AI, Storage, Notification.

**Critical pitfall with `AdditionalServices.cs`:** When adding a method to an existing service class (e.g. `SubmissionService`), the method must be placed *inside* that class's closing `}`. The file contains many back-to-back classes; accidentally placing a method after a closing brace compiles as a top-level function and causes `CS1519`. Always verify the surrounding class context before editing.

### Authorization model
- `[Authorize]` — any authenticated user
- `[Authorize(Roles = "Admin")]` — school admin only
- `[Authorize(Roles = "Admin,Teacher")]` — admin or teacher
- `[Authorize(Roles = "Student")]` — student only
- `[Authorize(Roles = "Parent")]` — parent only

The JWT token contains `schoolId`, `email`, `role` claims. `schoolId` is a `Guid`.

#### Permission catalogue widenings (Sprint 1.5.0 Step 6)
Deliberate, reviewed additions to the position→permission map in `PositionsSeedData` (applied to live via sync-by-key on next server start). Recorded here so the catalogue's evolution is auditable:
- `platform.access` — new baseline permission, **identity-implicit for all five identities** (Staff/Learner/Parent/External/System), attached to no position. Backs "any authenticated user" endpoints (e.g. `/api/me`) under the permission model (D1).
- `system.integrations` — added to **Principal** and **DeputyPrincipal** (previously ITAdministrator only). Rationale: Principals/Deputies *authorise* which integrations a school uses; IT Administrators configure them technically — both legitimately hold it.
- `communications.whatsapp_admin` — added to **ITAdministrator** (previously Principal/DeputyPrincipal only).
- `communications.whatsapp_trigger` — was held by **no** position; now granted to **Principal, DeputyPrincipal, ClassTeacher, GradeHead, FinanceManager, SportCultureMIC**. Preserves the admin-vs-trigger distinction (administer the integration vs. send a message).
- `system.data_export` — **new permission, in the Sensitive set** (DB-resolved per request — these are bulk PII exports that must not trust cached JWT claims). Granted to **Principal, DeputyPrincipal, ITAdministrator, Auditor, DistrictOfficial**. Backs SaSamsController (#27) SA-SAMS compliance exports; deliberately **not** granted to Finance or teaching positions.
- `system.popia_admin` — **new permission, in the Sensitive set** (bulk PII reads of consents / data-subject requests). Granted to **Principal, DeputyPrincipal** only (POPIA Information Officer = head of organisation, delegable to a deputy; not IT/external/teaching). Backs PopiaController (#23) admin endpoints; the self-service consent/DSR endpoints there use `platform.access`.
- `analytics.view_school` — **new permission, in the Sensitive set** (school-wide dashboards surface named at-risk learners + recent activity; DB-resolved per request). Granted to **Principal, DeputyPrincipal, HOD, PhaseHead, GradeHead** (SMT + academic oversight). **Intentional tightening from legacy `[Authorize(Roles="Admin,Teacher")]`**: rank-and-file Subject/Class/LO/SportCulture teachers no longer see school-wide named at-risk lists or all-class performance comparisons — those are oversight functions; scoped teacher analytics come from the Gradebook/Attendance controllers instead. Backs AnalyticsController (#3).
- `reporting.view` — **new permission (NOT Sensitive — high-frequency teacher workflow)**. View/generate class term reports, at-risk lists, AI report comments, and the `vw_attendance_summary`/`vw_gradebook_simple` views. Granted to **SubjectTeacher, ClassTeacher, LOTeacher, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal**. Backs ReportsController (#26). **D-R2 distinction:** `report.draft` is reserved for comment *submission*; *generating* an AI report comment is `reporting.view` — so HODs/oversight (who hold `reporting.view`, not `report.draft`) can trigger comment generation for classes in their scope. (Data boundary — a teacher seeing only their own classes — is enforced by Step 7 scope filtering, not by this permission.)
- `reporting.principal_summary` — **new permission, in the Sensitive set** (end-of-term, school-wide named data; DB-resolved per request). Granted to **Principal, DeputyPrincipal** only. Backs ReportsController `principal-summary`. Applied as a method-level `[RequirePermission]` on top of the class-level `reporting.view`; ASP.NET ANDs them, and since the grantees are a subset of `reporting.view`, the effective access is exactly Principal + DeputyPrincipal.
- `marks.view_class` — **new permission (not Sensitive)**. View a class's gradebook matrix, submission lists, and quiz attempts. Granted to **SubjectTeacher, ClassTeacher, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal**. Backs GradebookController, SubmissionsController (teacher views), QuizzesController attempts (teacher). Teaching-cluster decision TC-3.
- `courses.manage` — **new permission (not Sensitive)**. Create/manage LMS course content (courses/modules/lessons). Granted to **SubjectTeacher, ClassTeacher, LOTeacher, HOD, Principal, DeputyPrincipal**. Backs CoursesController writes (views use `platform.access`). TC-4.
- `attendance.view_class` — **grant set widened** (existing permission): added **PhaseHead, GradeHead, Principal, DeputyPrincipal** (was SubjectTeacher, ClassTeacher, HOD) to match `marks.view_class` so oversight can view a class's attendance. Semantic separation kept — attendance endpoints never use `marks.view_class`.
- `assessment.create` — **grant set widened** (existing permission): added **LOTeacher** (was SubjectTeacher only). TC-2. ClassTeacher/HOD deliberately excluded.
- **Teaching-cluster TC-1 (intentional tightening):** `marks.capture`, `attendance.capture`, and `assessment.create` were **not** widened to Principal/DeputyPrincipal. Pure oversight roles do not author assessments or capture marks/attendance — an admin who needs to must also hold a teaching position. This is deny-by-default by design; school "Admins" (backfilled to Principal) lose direct grade/attendance/assignment authoring.
- `announcements.publish` — **new permission (not Sensitive)**. Create/edit/delete announcements. Granted to **SubjectTeacher, ClassTeacher, LOTeacher, SportCultureMIC, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal**. Backs AnnouncementsController writes (reads → `platform.access`). CS-1.
- `calendar.manage` — **new permission (not Sensitive)**. Create/delete calendar events. Granted to **SubjectTeacher, ClassTeacher, LOTeacher, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal**. Backs CalendarController event writes (reads → `platform.access`). CS-3.
- `timetable.manage` — **new permission (not Sensitive)**. Manage the school timetable. Granted to **Principal, DeputyPrincipal** only (kept separate from `calendar.manage` to preserve the legacy Admin-only timetable restriction). Backs CalendarController `POST /timetable`. CS-4.
- `activities.manage` — **new permission (not Sensitive)**. Manage Sports & Culture activities + participants. Granted to **SportCultureMIC, HOD, GradeHead, Principal, DeputyPrincipal**. MIC → own activity/team scope is enforced in Step 7. Backs ActivitiesController. CS-5.
- `skills.endorse` — **new permission (not Sensitive)**. View and endorse learner skill entries (staff trust action — deliberately **not** `platform.access`). Granted to **SubjectTeacher, ClassTeacher, LOTeacher, SportCultureMIC, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal**. Backs SkillsController staff endpoints (learner self-service → `platform.access`). CS-6.
- **Comms-cluster CS-2 (intentional tightening):** `communications.message_class` was **not** widened. Creating a class discussion thread (MessagesController `POST /threads/class/{classId}`) is a class-teacher function; Principal/DeputyPrincipal (`message_all`) and PhaseHead/GradeHead (`message_grade`) hold their own higher tiers and deliberately do not hold `message_class`.
- **Comms-cluster CS-5 (intentional tightening):** ActivitiesController staff *view* endpoints (`GET /`, `GET /{id}/participants`) move from legacy `Admin,Teacher` to `activities.manage` (MIC + HOD/GradeHead + SMT). Rank-and-file subject teachers no longer see activity rosters — activities are run by MICs with academic-oversight visibility.
- `school.manage` — **new permission (not Sensitive)**. Manage school profile, branding, settings, and CAPS-subject seeding. Granted to **Principal, DeputyPrincipal** only (not ITAdministrator). Backs SchoolsController writes except feature flags. AS-1.
- `academics.manage` — **new permission (not Sensitive)**. Manage academic structure (classes, subjects, class-subject assignments). Granted to **Principal, DeputyPrincipal, HOD**. Backs ClassesController/SubjectsController/ClassSubjectsController writes. AS-3.
- `ai.use` — **new permission (not Sensitive)**. Use AI-assisted teacher tools (grade suggestion, question generation, plagiarism check). Granted to **SubjectTeacher, ClassTeacher, LOTeacher, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal, ITAdministrator** (IT for diagnostics). Backs AiController. **Cost note:** holders can incur Anthropic spend — backstopped by `School.Settings.AiMonthlyCostCapZar`; a dedicated permission allows revoking AI access without touching teaching perms. AS-5.
- `system.feature_flags` — **grant set widened** (existing permission): added **Principal, DeputyPrincipal** (was ITAdministrator only) so school admins can toggle pillar/feature flags (SchoolsController `PUT /features`). AS-2.
- `system.refresh_views` — **new permission, in the Sensitive set** (Sprint 1.5.0.5; recomputes the materialized analytics/reporting views over ALL school data → DB-resolved per request). Granted to **Principal, DeputyPrincipal, ITAdministrator**. Backs AdminController `POST /api/admin/refresh-views`. Refresh is **manual-only** by design — never on grade save (bulk mark capture would thrash a full refresh per row).
- **Admin-cluster AS-3 (intentional tightening):** ClassSubjectsController `POST /bulk` (subject-teacher assignment) moves from legacy `Admin,Teacher` to `academics.manage` (Principal/Deputy/HOD). Rank-and-file teachers no longer restructure subject-teacher assignments — that's an academic-management function.
- **Admin-cluster AS-7 (intentional tightening):** PluginsController `POST /dispatch` (fan webhook events to plugins — an internal integration action) moves from legacy `Admin,Teacher` to `system.integrations` (Principal/Deputy/ITAdministrator). Rank-and-file teachers no longer dispatch plugin events.
- `finance.view_all`, `finance.reports` — **added to the Sensitive set** (FIN-3): bulk financial reads (all fee accounts / financial reports) → DB-resolved per request. `finance.reports` has no endpoint yet; marking it Sensitive now means Sprint 1.5.4 finance-reporting endpoints inherit the protection automatically.
- **SuperAdminController (D3 exemption):** SuperAdmin is a platform-level role across ALL schools, outside the per-school identity/permission model. Its endpoints use `[RequireSuperAdmin]` (a `AuthorizeAttribute` enforcing the `SuperAdmin` role) + a class-level `[AnonymousJustification]` — **not** `[RequirePermission]`. The governance test (`EndpointAuthorizationContractTests`) accepts `[RequireSuperAdmin]` + non-empty justification as a compliant decision. `/api/super` is also skipped by `TenantMiddleware`.
- **Step 6 COMPLETE: `LegacyAuthorizeControllers` ratchet is EMPTY.** Every controller endpoint now makes an explicit decision — `[RequirePermission]`, justified `[AllowAnonymous]`, or `[RequireSuperAdmin]`. The governance test enforces this for all current and future controllers; do not re-introduce bare `[Authorize]`.
- **Finance SoD revocations (Step 6 Finance audit, applied 2026-06-14) — most significant intentional tightening of the migration.** The Step 6 audit found segregation-of-duties violations in the original seed. **What was revoked:** `finance.exempt_approve` from **FinanceManager**; `finance.create_invoice` + `finance.exempt_initiate` from **BursarDebtorsClerk**. **Why:** FinanceManager could both create invoices and approve exemptions, and both initiate and approve exemptions (violations); Bursar is a capture-and-chase role, not fee-creation. After the fix: FM/Bursar **initiate** exemptions, SMT (Principal + DeputyPrincipal) **approve** them; only FinanceManager **creates** fees. `finance.refund` stays with FinanceManager (latent capture+refund overlap — no refund endpoint exists yet; revisit in Sprint 1.5.4). **Mechanism:** because catalogue sync is additive-only, the revocations are applied by an explicit **idempotent delete-if-present revocation block** in `PositionsSeedData` (runs after the additive sync; removes the pairs from already-seeded DBs incl. live, no-op on a fresh DB) — a deliberate, documented exception to the never-delete invariant, so the live DB and a fresh install end up with identical security properties. **Broader principle (FIN-5):** every financial operation is now traceable to a finance position, not to seniority — Principal/Deputy retain finance oversight (`view_all`, `reports`, `exempt_approve`) but **lose operational fee writes** (`create_invoice`, `capture_payment`); those require an explicit FinanceManager/Bursar/Cashier appointment.

### Notable non-obvious endpoints
| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /api/users/directory?q=` | Any | Lightweight name/email search for all users in school — for message recipient lookup |
| `POST /api/users/import-csv` | Admin | Bulk-creates users from a CSV upload; returns `{ created, failed: [{row, reason}] }` |
| `GET /api/users/import-csv` | Admin | Returns a CSV template file download |
| `GET /api/submissions/pending?limit=` | Admin, Teacher | Ungraded submissions — teachers see only their own assignments' submissions |
| `GET /api/classes?mine=true` | Any | When role is Teacher, filters to classes where the current user is the assigned teacher |
| `GET /api/gradebook/{classId}` | Admin, Teacher | Full grade matrix (students × assignments) for a class |
| `GET /api/gradebook/my-grades` | Student | Student's own grade history |

### Database
Supabase Postgres. EF Core 8 with Npgsql. All table/column names use `snake_case` (applied globally in `SchoolPortalDbContext.OnModelCreating`). Twelve EF migrations, `InitialCreate` through `AddIdentityPositionsPermissions` — see Migration chain notes below. Two Postgres views mapped as keyless entities: `AttendanceSummaryView`, `GradebookSimpleView`.

### Migration chain notes (Sprint 1.5.0 findings — read before adding migrations)
- **Migrations were gitignored from the initial commit (2025-10-07) until Sprint 1.5.0** (`**/Migrations/` in `.gitignore`). No migration was ever in version control before commit `1b2e2cc7` (12/06/2026). Do not re-ignore them.
- **The live DB history contains two orphan rows whose files are lost** (never tracked, deleted from the dev machine): `20260521000000_Phase2Features` (an earlier Phase2Features applied before the surviving `20260521130950` one) and `20260523120000_Phase4SuperAdminSettings` (created `super_admins`). Verified 12/06/2026: a full table-level diff of the live DB against the current EF model shows **no drift** — every effect of the lost migrations is present in the current model/snapshot.
- **The chain cannot replay from scratch**: nothing surviving creates `super_admins`, yet `AddAcademicCalendar` alters its PK. `InitialCreate`'s invalid `audit_logs` default (`bigint DEFAULT gen_random_uuid()`) was repaired in-place during Sprint 1.5.0. Fresh environments need `EnsureCreated` (what integration tests use) or a future baseline migration — candidate fix logged for Sprint 1.5.0 Step 11: reconstruct a stub `20260523120000_Phase4SuperAdminSettings` with guarded `CREATE TABLE IF NOT EXISTS super_admins` (it would never run on the live DB, whose history already records that id, but repairs fresh replays).
- **Integration tests run on real Postgres via the Docker CLI** (`docker run … postgres:16-alpine`, see `PostgresFixture`), schema built by `EnsureCreated`. Testcontainers' .NET package is NOT used — Windows Smart App Control blocks its unsigned assembly. The legacy in-memory tests fail on the jsonb POCO columns (pre-existing; conversion scheduled in Sprint 1.5.0 Step 10).
- `AddIdentityPositionsPermissions` (applied to live 12/06/2026) changed `audit_logs.audit_log_id` from `bigint` to `uuid` and added the identity/positions/permissions tables.

Key relationships:
- `Student.ParentUserId` → links a parent `User` to their child `Student`
- `ClassSubject` joins `Class` ↔ `Subject` and carries its own `TeacherId`
- `School.Theme` and `School.Features` are `jsonb` columns deserialized via `.EnableDynamicJson()` on the Npgsql data source

`RowVersion` is `long` everywhere — Postgres doesn't support SQL Server's `byte[]` rowversion.

### Test infrastructure notes

- **Port 5432 vs 5433.** A native Postgres instance occupies the host's `5432`. Integration-test Docker containers must publish on **5433** (`docker run -p 5433:5432 …`) and the test run must point at it via `TEST_PG_CONNECTION=…Port=5433…`. Using 5432 collides with the native server and the tests bind to the wrong database.
- **`BackfillTests.Apply` parallel-load flake.** This test has a known Npgsql timeout flake when run under parallel load while the machine sleeps — it is a machine/timing artefact, **not** a logic bug. It passes reliably in isolation (~4m51s). Run it alone if it flakes; do not "fix" it by changing backfill logic.
- **Three legacy in-memory tests are baseline-red.** `AssignmentServiceTests`, `AttendanceServiceTests`, and `AssignmentEndpointTests` use the EF in-memory provider, which cannot map the `jsonb` POCO columns (`School.Theme`/`School.Features`). They are **pre-existing baseline failures**, deferred to **Step 10** (Sprint 1.5.0), which converts them to the real-Postgres fixture. Do not treat their red as a regression.
- **CI excludes those three** (`.github/workflows/ci.yml`) via a filter carrying a comment that points to Step 10. When Step 10 converts them, remove the exclusion.
- **Docker Desktop checkpoint-failure on this machine.** A plain `docker run … postgres:16-alpine` here exits (code 1) during the entrypoint's init→restart because the shutdown checkpoint fails on Docker Desktop's disk. Run the test container with its data dir on tmpfs and fsync off — fine for a throwaway DB and also fast:
  `docker run -d --name schoolport-test-pg -p 5433:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=schoolport_test --tmpfs /var/lib/postgresql/data:rw postgres:16-alpine -c fsync=off -c full_page_writes=off -c synchronous_commit=off`
  Then `TEST_PG_CONNECTION=Host=localhost;Port=5433;Database=schoolport_test;Username=postgres;Password=postgres`. With tmpfs the Backfill flake did not reproduce (full integration set ~14s).

### SignalR
`NotificationHub` at `/hubs/notifications`. On connect, users join three groups: `school:{schoolId}`, `user:{userId}`, `school:{schoolId}:role:{Role}`. JWT is extracted from the `access_token` query param (browsers can't set headers on WebSocket upgrades) — configured in `Program.cs` via `OnMessageReceived`.

## Frontend Architecture (schoolportal-web)

### Routing & Auth
`proxy.ts` (Next.js 16's name for what was `middleware.ts`) handles:
1. Redirect unauthenticated users → `/login`
2. Redirect authenticated users away from `/login` → `/dashboard`
3. Inject `x-pathname` header so the server-layout breadcrumb resolves the page title

**Never create `middleware.ts`** — Next.js 16 uses `proxy.ts`. Both files existing simultaneously crashes the dev server with an unrecoverable error.

Three cookies set on login (8hr TTL): `sp_token` (JWT), `sp_role`, `sp_userid`. Read client-side via `getClientRole()` / `getClientUserId()` in `lib/utils.ts` — avoids extra `/api/me` network calls for role-gated UI rendering.

### Layout
`app/(dashboard)/layout.tsx` is a **server component**. It fetches `/api/me` and `/api/schools/current` on every navigation, renders `<Sidebar>` and a sticky header with breadcrumb + notification bell. If the API is unreachable it renders a helpful error screen instead of crashing.

### API layer
All calls go through `lib/api.ts`, a typed fetch wrapper. `request<T>()` reads `sp_token` from cookies and sets `Authorization: Bearer` automatically. Base URL: `NEXT_PUBLIC_API_URL` (default `http://localhost:5128`).

Multipart uploads (file uploads, CSV import) bypass `request<T>()` and call `fetch()` directly with manual auth header injection — see `api.users.importCsv` and `api.submissions.submit` as reference patterns.

### Role-gated UI
```ts
const [role, setRole] = useState("");
useEffect(() => { setRole(getClientRole()); }, []);
const isAdmin = role === "Admin";
const canEdit  = role === "Admin" || role === "Teacher";
```
The `useEffect` is required because `getClientRole()` reads `document.cookie` and can only run client-side. Pages that gate UI on role always follow this pattern.

### UI component conventions

**Icons:** All icons are `lucide-react` — no emoji in any UI content.

**Component palette:**
- `components/ui/stat-card.tsx` — KPI metric card with colored icon box, large value, uppercase label, optional trend line. Props: `icon`, `label`, `value`, `color` (`"blue"|"green"|"purple"|"orange"|"red"|"teal"`), `trend?`.
- `components/ui/skeleton.tsx` — exports `Skeleton`, `SkeletonTable`, `SkeletonCards`, `SkeletonKPIs`.
- `components/ui/badge.tsx` — `rounded-md` (not `rounded-full`), ring-based borders. Variants: `default`, `success`, `warning`, `destructive`, `outline`.
- `components/ui/button.tsx` — variants: `default`, `secondary`, `outline`, `ghost`, `destructive`.
- `components/ui/card.tsx` — exports `Card`, `CardHeader`, `CardTitle`, `CardDescription`, `CardContent`.

**Patterns:**
- Empty states: centered `<Icon className="h-10 w-10 text-gray-300" />` + title + description + optional CTA
- Modals: `fixed inset-0 z-50 bg-black/40 backdrop-blur-sm` overlay, `rounded-2xl bg-white shadow-2xl` panel, close button top-right
- Page titles: `text-2xl font-semibold text-gray-900 tracking-tight`
- Table headers: `text-xs font-semibold text-gray-500 uppercase tracking-wider`
- Page padding: `p-6 lg:p-8`
- Cards: `rounded-xl border border-gray-100 shadow-sm ring-1 ring-gray-100/50`

### Sidebar navigation
`components/sidebar.tsx` uses lucide-react icons, grouped nav sections (main / learn / tools / admin), dark navy (`#0f172a`) background via `--sidebar-bg` CSS variable, and active items styled with the school's primary color. Nav items are defined as `NavItem[]` with `group` and `feature` fields — `feature` is matched against `school.features` (jsonb) to show/hide feature-flagged items.

## Sprint 1.5.1 — Pathways v1 (Complete)

### New Entities (global reference data — no SchoolId)
| Entity | Table | Purpose |
|---|---|---|
| `University` | `universities` | SA universities (10 seeded) |
| `Career` | `careers` | Career archetypes (20 seeded) |
| `UniversityCourse` | `university_courses` | Course with `MinimumAps`, `Faculty`, `ApsNotes` |
| `CourseSubjectRequirement` | `course_subject_requirements` | `SubjectName` string (CAPS standard name), `MinimumPercent?`, `IsRequired` |
| `SeniorPhaseRequirement` | `senior_phase_requirements` | FET → Senior Phase prerequisite map (6 seeded) |

### Per-learner Entities
| Entity | Table | Purpose |
|---|---|---|
| `LearnerCareerGoal` | `learner_career_goals` | Student saves a `UniversityCourse` as a goal; unique index on `(StudentId, UniversityCourseId)` |
| `AiGapAnalysisCache` | `ai_gap_analysis_cache` | SHA-256 fingerprint cache, 7-day TTL |
| `AiUsageLog` | `ai_usage_logs` | Per-call cost log in ZAR; drives monthly cap enforcement |

### APS Calculation
SA National Senior Certificate Admission Point Score (7-point scale):
- 80%+ → 7, 70%+ → 6, 60%+ → 5, 50%+ → 4, 40%+ → 3, 30%+ → 2, <30% → 1
- **Standard APS:** best 6 subjects, Life Orientation excluded
- **Life Orientation** capped at 4 APS points; included in some university totals but never in the standard APS used for admission comparisons

### AI Gap Analysis
- Model: `claude-sonnet-4-6`, cost R3/MTok input + R15/MTok output (at R18.50/USD)
- Cache: SHA-256 fingerprint of `{courseId}:{standardAps}|{subj}={avg%}|...` sorted by subject name; expires after 7 days
- Monthly cap: `School.Settings.AiMonthlyCostCapZar` (default R100); sum of `AiUsageLog.EstimatedCostZar WHERE CreatedAt >= month start AND Success = true`
- Returns `{ available: false }` (never throws) when: no API key, cap exceeded, API error, parse failure

### Tracking Status Logic (`PathwaysService`)
- **Red:** APS gap > 3 OR any required subject > 10% below minimum
- **Amber:** APS gap 1–3 OR any required subject 0–10% below minimum
- **Green:** APS met and all required subjects met

### New Endpoints
| Method | Path | Role | Purpose |
|---|---|---|---|
| `GET` | `/api/pathways/universities` | Any | List all universities |
| `GET` | `/api/pathways/universities/{id}/courses` | Any | Courses for a university |
| `GET` | `/api/pathways/careers` | Any | All career archetypes |
| `GET` | `/api/pathways/aps` | Student | Current learner's APS breakdown |
| `GET` | `/api/pathways/goals` | Student | Learner's saved goals with tracking |
| `POST` | `/api/pathways/goals` | Student | Save a goal `{ universityCourseId }` |
| `DELETE` | `/api/pathways/goals/{goalId}` | Student | Remove a goal (also clears AI cache) |
| `GET` | `/api/pathways/goals/{goalId}/tracking` | Student | Detailed gap analysis for one goal |
| `POST` | `/api/pathways/goals/{goalId}/gap-analysis` | Student | Trigger AI gap analysis (`?forceRefresh=true` bypasses cache) |
| `GET` | `/api/parent/pathways` | Parent | Child's goals + APS for parent dashboard |

### Frontend
- `pathways/page.tsx` — Students: two tabs ("Career Goals", "Subject Enrolment"). Staff: MatrixView only.
- `components/pathways/GapAnalysisCard.tsx` — Lazy-loaded AI widget (idle → loading → result/unavailable/error).
- `parent/page.tsx` — `PathwaysWidget` shows child's goals as coloured status rows.

### Migration
`AddPathwaysV1` — adds all 8 tables above.

### Settings Added
`SchoolSettings.AiMonthlyCostCapZar` — `decimal`, default `100.00m`, stored in `School.Settings` jsonb.

---

## Key Constraints

- `RowVersion` is `long`, not `byte[]` — Postgres has no `rowversion` type.
- `School.Theme` / `School.Features` are `jsonb` — requires `.EnableDynamicJson()` on Npgsql data source (already configured in `Program.cs`).
- Attendance bulk upsert uses `ON CONFLICT DO UPDATE` — the SQL Server TVP approach was removed during the Supabase migration.
- `AdditionalServices.cs` contains many classes back-to-back — always confirm you're inside the correct class before adding methods.
- Building while the server is running fails with MSB3027 DLL-lock errors — kill PID first.
