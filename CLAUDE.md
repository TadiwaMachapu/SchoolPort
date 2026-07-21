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

### Phase 0 — Foundation — ✅ foundational items shipped (across Sprints 1.5.0–1.5.0.5)
Lays groundwork. Onboarding wizard, feature-flag enforcement, academic calendar, CAPS subject seeding, and the sidebar audit are done. **Item 6 (service-file split) is partial** — `AdditionalServices.cs` still hosts multiple service classes (see the pitfall note under Service pattern).

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
- **Admin subjects page: show a banner when subject-match-report returns `healthy: false`, linking to the mismatch report. Implement when admin subjects UI is built.** (Sprint 1.5.1 Gap 3 follow-up — the backend endpoint `GET /api/pathways/subject-match-report` and structured ops warnings shipped; the admin-facing banner is deferred until that UI surface exists.)
- **LO-exclusion matches on subject name only — fix requires coordinated change to both CapsSubjects.IsLifeOrientation and the matview SQL to use the LO code as primary identifier. Deferred — no school currently uses "LO" as official subject name.** (Sprint 1.5.1 Gap 3 flagged edge: a school renaming Life Orientation to "LO" would match requirements via the code tier but slip past the best-6 LO exclusion in `PathwaysService.GetLearnerApsAsync` / `vw_matric_aps_summary`.)
- **Migrate fetch-in-effect to a shared data hook across the reports/matric feature area (Sprint 1.5.3 lint cleanup).** ESLint `react-hooks/set-state-in-effect` (React 19 plugin) fires on the codebase's established `useEffect(() => { setLoading(true); api.x().then(setData)...})` pattern — pre-existing in `components/matric/RiskDashboardTab.tsx` and matched (deliberately, for consistency) by the new Sprint 1.5.3 views (`GradeHeadView`, `HodSubjectView`, `SchoolOverviewView`). Fix as ONE dedicated pass: extract a small `useApiResource(fetcher, deps)` hook (or adopt the already-installed `@tanstack/react-query`) and migrate the whole feature area together, so the pattern stays uniform rather than three files diverging. Not build-blocking (`next dev` skips ESLint); TypeScript is clean.

### Step 9.5 follow-ups (deferred refinements)
- **Assignment scope is class-level, not class-subject-level (Fix #2 — deferred).** A SubjectTeacher of one subject in 12A can see *other* subjects' assignments in that class, because assignment scope is keyed on `Class`, not `ClassSubject`. **Low-Med, not a sensitive-data leak** (same-class academic context, not PII) — deferred to a later refinement sprint as it needs a deeper scope-model change (scope assignments through `ClassSubject.TeacherId`, not `Class`). **Also logged here:** `GET /api/classes/{id}` and `GET /api/classes/{id}/subjects` are `platform.access` and filter only by `SchoolId`, not by scope — a learner could read another class's metadata/subject list by id. Class metadata, **low severity — log, don't fix** (unlike the list endpoint, which Step 9.5 Fix #1 tightened).
- **Unassign teacher from a class-subject (Build #6b follow-up).** `SubjectService.BulkAssignClassSubjectsAsync` is deliberately non-destructive — it only *sets* `TeacherId` (`if (item.TeacherId.HasValue)`) and never clears it. So the `/classes/[id]` "Assign teacher" control can assign/reassign but **cannot unassign**. If unassign is needed, extend the bulk endpoint with an explicit clear signal (a flag/sentinel) rather than overloading `null` — keep the current null-ignoring semantics intact for the common upsert path.
- **Frontend component-test infra (deferred to Step 10/11).** Vitest currently runs `node`-env over `lib/**/*.test.ts` only — there is no React Testing Library / jsdom, so React components (e.g. the `TeacherCell` assign→persist→update flow on `/classes/[id]`) have no automated render/interaction test. Add `@testing-library/react` + `jsdom` and a jsdom vitest project when broadening the frontend suite, then backfill the `TeacherCell` test. (Step 9.5 covered the assignment via backend tests + manual spot-check.)
- **L1 — oversight roster read of a non-existent class returns `200 []` instead of 404 (deferred, Low).** Found in the Step 9.5 live spot-check: `GET /api/classes/{id}/students` as oversight (`marks.view_all`) → `CanAccessClassAsync` returns true for *any* id (null scope contains everything), so a bogus/non-existent class id falls through to an empty roster (`200 []`) rather than `NotFound`. **No data leak** (empty, same-tenant) — it's a contract inconsistency vs. the non-oversight path (which 404s). Deliberately NOT fixed: the fix adds an in-school existence query to the roster read path that Sprint 1.5.0.5 just optimised, not worth it for a Low contract nit. If fixed later, add an existence check ahead of the scope branch so unknown ids 404 for all identities.
- **Seed a second teacher into the demo school (test-data follow-up).** The demo school (`admin@demo.schoolportal.com`) has only one Teacher, so a live spot-check cannot exercise a *true* teacher reassignment (A→B→A) on `POST /api/class-subjects/bulk` — inventing a teacherId correctly 404s via the H1 guard. Value-change persistence IS covered by integration tests (`ClassSubjectAssignmentTests`); this is purely for live-verification convenience. Add a 2nd Teacher (+User) to the demo seed.

### Materialized views & refresh (Sprint 1.5.0.5 → follow-ups)
- **`vw_subject_term_averages`, `vw_matric_aps_summary`, `vw_school_performance_summary`** — refreshed **manually only** via `POST /api/admin/refresh-views` (`system.refresh_views`, Sensitive). NOT refreshed on grade save.
- **Sprint 1.5.3 — debounced background refresh:** add a background job that refreshes the views at most once per N minutes *when grades have changed*, so Smart Reports reads stay reasonably fresh without thrashing under bulk mark capture. The proper pattern, deferred until Smart Reports needs live freshness.
- **Smart Reports at-risk FLAGGING threshold:** `vw_school_performance_summary` uses **40%** for its pass-rate / at-risk-count (CAPS minimum). Smart Reports (Sprint 1.5.3) intervention flagging should use a **separate, higher threshold (~50%)** — a learner above the pass line can still need intervention. Keep the two distinct; do not change the view's 40% pass calculation.
- **Overall average — ONE method, ONE window everywhere (Sprint 1.5.3).** Every surface (Term Report, learner card, at-risk tab, Matric dashboard) computes overall = **avg-of-subject-averages** (each subject weighted once, report-card style) over the **captured-marks path** (`AtRiskMarks.CapturedPredicate`; Term Report was migrated off the legacy Submission-join path — it couldn't see submission-less bulk-captured marks — and its per-mark rounding was dropped so it matches `AtRiskService` to the decimal). The window is the **SELECTED TERM**: `AtRiskService` term-scopes the per-subject average, red/amber/green risk, the below-50 count and the band to marks in that term (previous term only for the trend arrow). A learner strong last term but failing now is judged on now — all-time scoring hid exactly the learner needing intervention. No marks in the term → `no_data` / null average, never 0% (no-data ≠ zero, like attendance). Guarded by `OverallAverage_ConsistentAcrossSurfaces` + `AtRisk_OverallAverage_And_BelowFifty_AreTermScoped` + `AtRisk_NoMarksInSelectedTerm_IsNoData_NotZero`.
- **`vw_matric_aps_summary` semantics (fixed Sprint 1.5.1 Gap 1, migration `FixMatricApsWeighting` + `migrations/008_fix_matric_aps_view.sql`):** `projected_aps` = **STANDARD APS — best 6 subjects excluding Life Orientation** (the number used for university admission comparisons and goal tracking); `total_aps` = **all subjects with each LO subject capped at 4 points** (the broader picture). Both match `PathwaysService.GetLearnerApsAsync` / `CalculateApsPoints` exactly — the two surfaces may never disagree again. Still a projection from year-averages (no promotion-mark weighting) and only as fresh as the last manual refresh.
- **Per-surface APS read source (Sprint 1.5.1 Gap 5 — DECIDED, do not revisit per surface):**
  - **Learner/parent dashboards → LIVE calculation** (`PathwaysService.GetLearnerApsAsync`): current accuracy is required; these surfaces must NOT read the matview (manual-refresh only → stale).
  - **Aggregate views → `vw_matric_aps_summary`**: principal dashboard, Smart Reports cohort view, Pathways cohort distribution — heavier reads where slight staleness is acceptable. Use `projected_aps` for admission-comparable numbers, `total_aps` for the broader picture.
- **Live APS is CURRENT-academic-year scoped (Sprint 1.5.1 Gap 2):** `GetLearnerApsAsync` resolves the current year (the `IsCurrent` term's year; fallback latest by `Year`), filters enrolments to that year's `LearnerSubjects` rows, and averages only grades whose assignment `DueAt` falls inside the year window. No academic year configured → fail-closed empty result. Prior-year grades and duplicate cross-year subject rows are excluded (pinned by `PathwaysServiceTests`).

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
├── SchoolPortal.Server/     ASP.NET Core 10 Web API
├── SchoolPortal.Data/       EF Core entities, DbContext, migrations
├── SchoolPortal.Shared/     DTOs shared between server and (Blazor) client
├── SchoolPortal.Client/     Blazor WebAssembly (legacy, largely unused)
├── SchoolPortal.Tests/      xUnit tests (unit + integration)
├── schoolportal-web/        Next.js 16 + Tailwind primary frontend
└── PostgresSetup.sql        Supabase Postgres schema + seed data
```

## Runtime & Framework — .NET 10 (LTS)

Upgraded from .NET 8 pre-Sprint-1.5.1 (branch `chore/dotnet10-upgrade`). All solution projects target `net10.0`; CI pins `dotnet-version: 10.0.x` and `dotnet-ef 10.0.9`. Key package lines: ASP.NET Core / EF Core packages at 10.0.x, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2 (must track the EF major), Swashbuckle 10.x, Serilog.AspNetCore 10.x. netstandard2.0 packages (BCrypt.Net-Next, Stripe.net, Supabase, Moq, Blazored.LocalStorage, AspNetCore.HealthChecks.Npgsql 9.0.0 — no 10.x line) are unchanged and fine on .NET 10.

**HTTP QUERY method:** complex filter endpoints (gradebook filters, at-risk queries, Pathways cohort views — anywhere the filter is too complex for a query string) should use the **`MapQuery` extension** (`SchoolPortal.Server/Extensions/EndpointRouteBuilderExtensions.cs`) — QUERY is safe/idempotent like GET but carries a request body. .NET 10 ships only the primitives (no built-in MapQuery/[HttpQuery]); the extension wraps `MapMethods`. A Testing-env-only smoke endpoint (`/api/_smoke/query`) + `QueryMethodSmokeTests` prove the method end-to-end through the auth pipeline.

**Future improvements unlocked by .NET 10 (logged, deliberately NOT done in the upgrade):**
- **EF Core 10 JSON complex types** — `School.Features`/`School.Theme`/`School.Settings` are jsonb POCOs mapped via `.EnableDynamicJson()`; EF 10's first-class JSON complex-type mapping would give change-tracked, queryable JSON columns without the dynamic-JSON opt-in. Migration-affecting; needs its own sprint.
- Re-audit hot LINQ queries (gradebook matrix, roster reads) for EF 10's improved translations before hand-optimising anything new.

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
dotnet user-secrets set "Gemini:ApiKey" "<key>"
```

Or set env var `CONNECTIONSTRINGS__DEFAULTCONNECTION` at runtime.

**⚠️ Deployment prerequisite — CORS origins.** `CorsOrigins` in `appsettings.json` is **localhost-only**; the CORS policy (`Program.cs`, `AllowSPA`) is an authoritative allowlist (no wildcard/`SetIsOriginAllowed`). **Production deploy MUST add the real SPA origin to `CorsOrigins`** (via `appsettings.Production.json` or the `CorsOrigins__N` env vars) **or SignalR will fail in prod** (the notification hub negotiate is a credentialed cross-origin request — it is rejected if the origin isn't allowlisted).

## Backend Architecture

### Multi-tenancy
Every authenticated request carries a `schoolId` JWT claim. `TenantMiddleware` (runs after auth, before authorization) reads this claim and stores it in `HttpContext.Items["SchoolId"]`. All services use `ICurrentUserService` to get `SchoolId`, `UserId`, and identity/permissions (note: `ICurrentUserService.Role` was **removed** in Step 7 — use identity/`HasPermission`) — never pass tenant IDs through request parameters.

**Tenant isolation is application-layer only (deliberate, pilot decision — Option A).** The app connects to Postgres as `postgres` (bypasses RLS), so DB-layer RLS is not in force; the tested controls are the 19 cross-tenant/IDOR guards, `IScopeService`, the `CrossTenantGuardScannerTests` scanner, and 170 tests. Full RLS defense-in-depth is a **pre-scale** item. See `SECURITY.md` §5. **Mutating-endpoint rule:** any write taking a tenant-owned id (body or route) MUST validate it belongs to the caller's school before writing (the FK resolves cross-tenant) — the scanner enforces a guard test for every such endpoint.

**Supabase Data API (`/rest/v1/`) is locked down** (`migrations/006_restrict_data_api.sql`, applied live 2026-06-19): anon/authenticated have no access to the `public` schema. This closed a cross-school leak where aggregate views/matviews were readable with the public anon key. Nothing in the app uses the Data API (frontend has no supabase-js; backend uses Npgsql/EF + the Storage API).

**Submissions bucket is PRIVATE (Sprint 1.5.0.6, POPIA — closed the last pre-pilot storage follow-up).** `migrations/007_private_submissions.sql` (applied live 2026-07-05 as Supabase migration `private_submissions_bucket`) set `storage.buckets.public = false` for `submissions` and dropped the public read policy. Unsigned `/object/public/...` URLs now return `400 Bucket not found` (verified live — the private bucket is invisible to the public endpoint). Reads go through `IStorageService.GetSignedUrlAsync` / `GetSignedUrlsAsync` (Supabase Storage REST `POST /storage/v1/object/sign/...`, service role key, **1-hour expiry**, bulk variant = one round-trip per class list), minted at DTO-build time in `SubmissionService` (`GetSubmissionsByAssignmentAsync`, `GetMySubmissionAsync`). **Storage convention:** `Submission.FileUrl` stores the **bucket-relative object path** for new rows; legacy rows hold full public URLs — `StorageService.ExtractObjectPath` normalises both (unknown shapes / signing failures → `null` in the DTO, never a raw path, never a 500). Uploads are unchanged (service role key bypasses storage RLS).

### Service pattern
Controllers are thin. Business logic lives in services injected by interface. Services are in `SchoolPortal.Server/Services/`:
- `AdditionalServices.cs` — hosts multiple smaller service classes in one file: School, Class, Subject, Grade, Submission, Announcement, etc.
- Separate files for larger services: Auth, User, Assignment, Attendance, Course, Quiz, AI, Storage, Notification.

**Critical pitfall with `AdditionalServices.cs`:** When adding a method to an existing service class (e.g. `SubmissionService`), the method must be placed *inside* that class's closing `}`. The file contains many back-to-back classes; accidentally placing a method after a closing brace compiles as a top-level function and causes `CS1519`. Always verify the surrounding class context before editing.

### Authorization model
**Current model (Sprint 1.5.0, Step 6 onward): three layers — Identity → Positions → Permissions.** See `SECURITY.md` for the full model and `Authorization/PermissionKeys.cs` + `Seeds/PositionsSeedData.cs` for the catalogue (source of truth, synced on startup). In short:
- Endpoints declare `[RequirePermission("perm.key")]`; genuinely-anonymous ones carry `[AllowAnonymous]` + `[AnonymousJustification]`; SuperAdmin uses `[RequireSuperAdmin]`. The `EndpointAuthorizationContractTests` governance test **fails the build** if any endpoint omits an explicit decision.
- Deny-by-default: `FallbackPolicy = RequireAuthenticatedUser`.
- Resource scope (which class/student/child) is enforced by `IScopeService` (IDOR → 403), not by the permission alone.
- Sensitive permissions (`PermissionKeys.Sensitive`) re-resolve from the DB per request (ignore the cached JWT set).
- The JWT carries `schoolId` (Guid), `email`, identity, and position claims.

**Legacy note:** bare `[Authorize]` / `[Authorize(Roles = "Admin,Teacher")]` etc. are **removed** and **banned** — the governance test's legacy allowlist is empty. Do not reintroduce them.

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
- `academics.diagnostics` — **new permission (not Sensitive — read-only configuration metadata, no PII)** (Sprint 1.5.1 Gap 3). View academic-configuration diagnostics, currently `GET /api/pathways/subject-match-report`. Granted to **Principal, DeputyPrincipal, HOD** (the `academics.manage` holders) **+ ITAdministrator** (they configure subject names during onboarding and need the diagnostic without gaining structure writes — deliberately a separate key, not a widening of `academics.manage`).
- `system.refresh_views` — **new permission, in the Sensitive set** (Sprint 1.5.0.5; recomputes the materialized analytics/reporting views over ALL school data → DB-resolved per request). Granted to **Principal, DeputyPrincipal, ITAdministrator**. Backs AdminController `POST /api/admin/refresh-views`. Refresh is **manual-only** by design — never on grade save (bulk mark capture would thrash a full refresh per row).
- `ai.tutor` — **new permission (not Sensitive)** (Sprint 1.5.2 Step 3). Ask the Matric Hub AI tutor (`POST /api/matric/tutor`, was `platform.access` in v1). **Learner identity-implicit** (in `PermissionKeys.IdentityImplicit["Learner"]`, same pattern as `marks.view_own` — the feature exists for learners, no position required) **+ granted to the `marks.view_class` teaching/oversight cluster** (SubjectTeacher, ClassTeacher, HOD, PhaseHead, GradeHead, Principal, DeputyPrincipal) so staff can test what their Grade 12 learners see. Deliberately separate from `ai.use` (teacher authoring tools): learner tutor access can be revoked without touching staff AI. **Cost/abuse controls:** learners are day-capped at `School.Settings.MatricTutorDailyLimit` successful non-cached answers (default 20, ≤0 disables; failed calls and cache hits consume no quota); staff callers have no Student row and are bounded by `AiMonthlyCostCapZar` instead. Pinned by `MatricTutorServiceTests` (rate limits) + two `PermissionBehavioralTests` rows (learner-implicit and staff-position grant paths).
- **Admin-cluster AS-3 (intentional tightening):** ClassSubjectsController `POST /bulk` (subject-teacher assignment) moves from legacy `Admin,Teacher` to `academics.manage` (Principal/Deputy/HOD). Rank-and-file teachers no longer restructure subject-teacher assignments — that's an academic-management function.
- **Admin-cluster AS-7 (intentional tightening):** PluginsController `POST /dispatch` (fan webhook events to plugins — an internal integration action) moves from legacy `Admin,Teacher` to `system.integrations` (Principal/Deputy/ITAdministrator). Rank-and-file teachers no longer dispatch plugin events.
- `finance.view_all`, `finance.reports` — **added to the Sensitive set** (FIN-3): bulk financial reads (all fee accounts / financial reports) → DB-resolved per request. `finance.reports` has no endpoint yet; marking it Sensitive now means Sprint 1.5.4 finance-reporting endpoints inherit the protection automatically.
- **SuperAdminController (D3 exemption):** SuperAdmin is a platform-level role across ALL schools, outside the per-school identity/permission model. Its endpoints use `[RequireSuperAdmin]` (a `AuthorizeAttribute` enforcing the `SuperAdmin` role) + a class-level `[AnonymousJustification]` — **not** `[RequirePermission]`. The governance test (`EndpointAuthorizationContractTests`) accepts `[RequireSuperAdmin]` + non-empty justification as a compliant decision. `/api/super` is also skipped by `TenantMiddleware`.
- **Step 6 COMPLETE: `LegacyAuthorizeControllers` ratchet is EMPTY.** Every controller endpoint now makes an explicit decision — `[RequirePermission]`, justified `[AllowAnonymous]`, or `[RequireSuperAdmin]`. The governance test enforces this for all current and future controllers; do not re-introduce bare `[Authorize]`.
- **Finance SoD revocations (Step 6 Finance audit, applied 2026-06-14) — most significant intentional tightening of the migration.** The Step 6 audit found segregation-of-duties violations in the original seed. **What was revoked:** `finance.exempt_approve` from **FinanceManager**; `finance.create_invoice` + `finance.exempt_initiate` from **BursarDebtorsClerk**. **Why:** FinanceManager could both create invoices and approve exemptions, and both initiate and approve exemptions (violations); Bursar is a capture-and-chase role, not fee-creation. After the fix: FM/Bursar **initiate** exemptions, SMT (Principal + DeputyPrincipal) **approve** them; only FinanceManager **creates** fees. `finance.refund` stays with FinanceManager (latent capture+refund overlap — no refund endpoint exists yet; revisit in Sprint 1.5.4). **Mechanism:** because catalogue sync is additive-only, the revocations are applied by an explicit **idempotent delete-if-present revocation block** in `PositionsSeedData` (runs after the additive sync; removes the pairs from already-seeded DBs incl. live, no-op on a fresh DB) — a deliberate, documented exception to the never-delete invariant, so the live DB and a fresh install end up with identical security properties. **Broader principle (FIN-5):** every financial operation is now traceable to a finance position, not to seniority — Principal/Deputy retain finance oversight (`view_all`, `reports`, `exempt_approve`) but **lose operational fee writes** (`create_invoice`, `capture_payment`); those require an explicit FinanceManager/Bursar/Cashier appointment.

#### Cross-tenant / IDOR write hardening (Sprint 1.5.0 Step 10) — count for SECURITY.md
The Step 10 audit hardened **19 id-bearing write endpoints** (`fix` commit `aed1068a`): **18 cross-tenant** linkage/IDOR (a foreign route/body id silently linked across schools because the FK resolves regardless of `SchoolId`) **+ 1 cross-user** IDOR (`QuizzesController.SubmitAttempt` — an attempt was loaded by id alone, so a learner could score another student's attempt; now scoped to the caller's own `UserId`). The "18 endpoints" in the commit headline = the cross-tenant set; the cross-user fix is the **+1** called out separately in the body, so the reconciled total is **19**. Earlier tallies (16+1, 15) were pre-burn-down snapshots — the count *grew* as the Courses cluster added `ReorderModules`/`ReorderLessons`. The full enumerated 19-endpoint list (controller → guarded id) is produced and belongs in **SECURITY.md, written in Step 11**. The `[CrossTenantGuard]` test universe is larger because it also covers endpoints that already had tenant scoping from Steps 7/9.5 (e.g. `DeleteClass`, `UpdateSubject`, `Activities.*`, `Positions.Assign`).

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
Supabase Postgres. EF Core 10 with Npgsql. All table/column names use `snake_case` (applied globally in `SchoolPortalDbContext.OnModelCreating`). EF migrations `InitialCreate` through `FixMatricApsWeighting` (Sprint 1.5.1) — see Migration chain notes below. Matview changes use the **dual-vehicle convention**: the EF migration is the chain's source of truth AND an identical `migrations/NNN_*.sql` ships for immediate manual apply to live. Two Postgres views mapped as keyless entities: `AttendanceSummaryView`, `GradebookSimpleView`.

### Migration chain notes (Sprint 1.5.0 findings — read before adding migrations)
- **Migrations were gitignored from the initial commit (2025-10-07) until Sprint 1.5.0** (`**/Migrations/` in `.gitignore`). No migration was ever in version control before commit `1b2e2cc7` (12/06/2026). Do not re-ignore them.
- **The live DB history contains two orphan rows whose files are lost** (never tracked, deleted from the dev machine): `20260521000000_Phase2Features` (an earlier Phase2Features applied before the surviving `20260521130950` one) and `20260523120000_Phase4SuperAdminSettings` (created `super_admins`). Verified 12/06/2026: a full table-level diff of the live DB against the current EF model shows **no drift** — every effect of the lost migrations is present in the current model/snapshot.
- **The chain cannot replay from scratch**: nothing surviving creates `super_admins`, yet `AddAcademicCalendar` alters its PK. `InitialCreate`'s invalid `audit_logs` default (`bigint DEFAULT gen_random_uuid()`) was repaired in-place during Sprint 1.5.0. Fresh environments need `EnsureCreated` (what integration tests use) or a future baseline migration — candidate fix logged for Sprint 1.5.0 Step 11: reconstruct a stub `20260523120000_Phase4SuperAdminSettings` with guarded `CREATE TABLE IF NOT EXISTS super_admins` (it would never run on the live DB, whose history already records that id, but repairs fresh replays).
- **Integration tests run on real Postgres via the Docker CLI** (`docker run … postgres:16-alpine`, see `PostgresFixture`), schema built by `EnsureCreated`. Testcontainers' .NET package is NOT used — Windows Smart App Control blocks its unsigned assembly. The former in-memory tests were converted to this real-Postgres path in Sprint 1.5.0 Step 10 (Part 4); no test uses the EF in-memory provider anymore.
- `AddIdentityPositionsPermissions` (applied to live 12/06/2026) changed `audit_logs.audit_log_id` from `bigint` to `uuid` and added the identity/positions/permissions tables.

Key relationships:
- `Student.ParentUserId` → links a parent `User` to their child `Student`
- `ClassSubject` joins `Class` ↔ `Subject` and carries its own `TeacherId`
- `School.Theme` and `School.Features` are `jsonb` columns deserialized via `.EnableDynamicJson()` on the Npgsql data source

`RowVersion` is `long` everywhere — Postgres doesn't support SQL Server's `byte[]` rowversion.

### Test infrastructure notes

- **Port 5432 vs 5433.** A native Postgres instance occupies the host's `5432`. Integration-test Docker containers must publish on **5433** (`docker run -p 5433:5432 …`) and the test run must point at it via `TEST_PG_CONNECTION=…Port=5433…`. Using 5432 collides with the native server and the tests bind to the wrong database.
- **`BackfillTests.Apply` parallel-load flake.** This test has a known Npgsql timeout flake when run under parallel load while the machine sleeps — it is a machine/timing artefact, **not** a logic bug. It passes reliably in isolation (~4m51s). Run it alone if it flakes; do not "fix" it by changing backfill logic.
- **The three formerly-baseline-red tests are converted (Step 10 Part 4, 2026-06-18).** `AssignmentServiceTests` + `AttendanceServiceTests` now run on the shared `PostgresFixture` (isolated DB per test via `CreateIsolatedDatabaseAsync`, mocked unrestricted scope — service-layer intent unchanged); `AssignmentEndpointTests` was rebuilt on the `ApiFactory` harness (real Postgres + full auth pipeline) and seeds a Staff+SubjectTeacher caller because POST `/api/assignments` now requires `assessment.create` and GET requires `platform.access`. **The CI exclusion filter is REMOVED — `dotnet test` runs the whole suite with ZERO exclusions.** The EF in-memory provider is no longer used by any test (it cannot map the `jsonb` POCO columns `School.Theme`/`School.Features`).
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

**Component palette** (all token-backed after the Sprint 1.6 overhaul — see "Frontend design tokens"):
- `components/ui/stat-card.tsx` — KPI card. Props unchanged: `color` (`"blue"|"green"|"purple"|"orange"|"red"|"teal"`), but the map is now token-backed (blue→primary, green→success, orange→warning, red→danger; **purple→secondary and teal→primary intentionally collapse** — no violet/teal token family).
- `components/ui/skeleton.tsx` — `Skeleton`, `SkeletonTable`, `SkeletonCards`, `SkeletonKPIs`.
- `components/ui/badge.tsx` — **`rounded-pill`**, tinted bg + dark same-hue text (no ring). Variants: `default`, `success`, `warning`, `destructive`, `outline`.
- `components/ui/button.tsx` — **`rounded-pill`**; primary = `bg-primary` white text, secondary = `bg-primary-100 text-primary-800`, ghost = transparent. Variants: `default`, `secondary`, `outline`, `ghost`, `destructive`.
- `components/ui/card.tsx` — `Card`/`CardHeader`/`CardTitle`/`CardDescription`/`CardContent`; `rounded-lg bg-surface-card shadow-card`, **no border**.
- `components/ui/empty-state.tsx`, `avatar.tsx`, `modal.tsx`, `page-with-rail.tsx` — new (see design-tokens section).

**Patterns (target — new work should follow these; un-migrated pages still use the old ones):**
- Empty states: use the `EmptyState` primitive (56px lucide icon in a 96px tinted circle). GOOD-NEWS empties (zero at-risk) use `tone="positive"` + positive copy.
- Modals: use the `Modal` primitive (`bg-text-primary/40 backdrop-blur` overlay, `rounded-xl bg-surface-card shadow-card` panel).
- Page titles: `text-[20px] font-semibold text-text-primary tracking-tight`.
- Table headers: `text-[11px] font-semibold text-text-muted uppercase tracking-wider`; 0.5px `divide-border` rows, no vertical rules.
- Sentence case everywhere (page titles, section headings, **stat-card labels**, buttons); ALL CAPS only for table column labels.
- Cards: `rounded-lg bg-surface-card shadow-card`.
- **Status colour is NEVER decorative.** `danger`/`warning`/`success` (red/amber/green) carry meaning the CAPS bands and at-risk flags depend on — do not assign them to convey variety or to distinguish neutral counts. A KPI icon tile for a plain count ("20 assignments") uses **neutral** (see next rule), not a random status hue; colour a tile red/amber/green ONLY when the number reflects that real state (0 needs-grading = success; overdue = danger). For genuine decorative variety use the brand `secondary` (coral), never status colours — and never the primary green tint on a neutral (see next rule).
- **Neutral UI elements use `surface-subtle`, not `primary` tint.** Primary tint means "this is branded/primary", not "this is a container". Because primary IS green and success is green — indeed `primary-100` and `success-100` are the same `#EAF3DE` — tinting neutrals with primary makes the whole surface read green and makes success unreadable. A plain-count KPI tile, a neutral icon chip, a generic container → `bg-surface-subtle` with `text-text-secondary` icons (`StatCard color="neutral"`). Reserve `primary-100`/`bg-primary` for genuinely primary/branded elements (the active CTA, the brand mark, a selected state). This is the single biggest lever against a green-wash across the platform at rollout scale.
- **In-card empty states use `EmptyState size="compact"`** (small circle, tight padding); the large default is for full-page empties only.
- **One brand-coloured number per card, max — hierarchy, not decoration.** On any card/panel, the SINGLE headline metric (the point of the card) MAY be `text-primary`; every supporting number is neutral `text-text-primary`. If a card shows several equal-weight numbers, they ALL go neutral — colouring all of them is the same as colouring none. Only colour a number when it's genuinely the card's focus.
- **CAPS level labels are semantic state, not decoration — they band-colour wherever they appear.** An L1–L7 CAPS level must read the same colour on every surface (matching the capture grid's band colours); a teacher must never see L2 red on the capture grid but neutral grey in the gradebook overview. (During the colour rollout, un-unified CAPS labels may be left `text-secondary` as an interim token swap, but the end state is consistent band-colouring — see the CAPS-unification follow-up.)

### Sidebar navigation
`components/sidebar.tsx` uses lucide-react icons, grouped nav sections (main / learn / tools / admin), and derives its items from `deriveNav(identity, positions, permissions, features, context)` in `lib/nav.ts` (the shared source of truth for both desktop sidebar and `mobile-nav.tsx` — restyle only, never change gating). As of the Sprint 1.6 visual overhaul it is a **light floating card** (`bg-surface-card rounded-lg`, floated by the `p-3` wrapper in `app/(dashboard)/layout.tsx`), active items use `bg-primary-100 text-primary-800`, and a help card is pinned bottom. The logo/avatar still use the school's `theme.primaryColor` (brand layer). Nav items are `NavItem[]` with `group` and `feature` fields — `feature` is matched against `school.features` (jsonb) to show/hide feature-flagged items.

### Frontend design tokens (Sprint 1.6 visual foundation)
The visual system is token-driven. **Single source of truth: the Tailwind v4 `@theme` block in `app/globals.css`.** No hardcoded hex outside that file. Palette: `primary` (green, 50–900), `secondary` (coral — decorative variety only, never status), semantic `danger`/`warning`/`success` (CAPS bands depend on these — kept distinct from brand), `surface`/`text`/`border` neutrals, radii, and `--shadow-card` (cards separate by contrast, not shadow). Typography is **Plus Jakarta Sans** loaded via `next/font/google` in `app/layout.tsx` (weights 400/500/600/700), exposed as `--font-jakarta` → `--font-sans`.

**BRAND vs CRAFT split (the governing principle):**
- **Brand layer — school-configurable at runtime:** logo, primary colour, name. A school overrides `--color-primary` via the inline style injected in `app/(dashboard)/layout.tsx` (`style={{ "--color-primary": theme.primaryColor }}`). `bg-primary`/`text-primary`/`border-primary`/`ring-primary` all resolve to that var, so per-school branding works without touching components. Only the primary *hue* is overridden; the tint scale (`primary-100…900`) is craft.
- **Craft layer — SchoolPort-owned, NOT school-configurable:** typography, spacing, radii, component design. **`SchoolTheme.FontFamily` is deliberately UNCONSUMED by the frontend** — the font loads globally via `next/font`; a school changing the typeface would break the design quality. It is seeded (`Plus Jakarta Sans` on Greendale) only for correctness. **Follow-up (branding sprint): consider removing `FontFamily` from `SchoolTheme`** — nothing reads it.

**Shared primitives (`components/ui/`):** `Button` (pill), `Card`, `Badge` (pill), `Input` (borderless, subtle bg), `StatCard`, `Skeleton`, plus new ones added in this overhaul — `EmptyState` (icon-in-tinted-circle; has a `tone="positive"` GOOD-NEWS variant for empty states that are healthy, e.g. zero at-risk), `Avatar`, `Modal` (replaces the ~8 inline modal copies as pages are restyled), `PageWithRail` (opt-in right-rail layout slot — dashboards render it; data-dense screens omit it and get full width). `Select`/`Tabs`/`Table` primitives are still deferred (promote when a restyled screen needs them).

**⚠️ Hardcoded colour utilities BLOCK per-school branding.** As of the Sprint 1.6 foundation, **~1,997 hardcoded palette utilities (`bg-blue-*`, `text-gray-*`, etc.) remain across 58 `.tsx` files** (was 2,214 / 71 before the overhaul; the 3 migrated screens + shell + primitives are clean). **A school changing their primary colour will leave every one of these elements blue/grey** — they don't read `--color-primary`. Migrating them to `bg-primary`/`text-primary`/token utilities is a **prerequisite for the branding sprint**, not merely "31 pages of restyle remaining". **Scope it as its own sizeable work item** — ~1,997 utilities across 58 files is the actual blocker to per-school branding shipping, not a footnote to the rollout. Budget the branding sprint around this migration, not around adding new colour-picker UI. Heaviest remaining offenders: `pathways/page.tsx` (107), `matric/page.tsx` (95), `quizzes/page.tsx` (90), `onboarding/page.tsx` (81), `reports/page.tsx` (76), `users/page.tsx` (70), `parent/page.tsx` (69), `school-pay` (67), `attendance`/`gradebook` (65). The `(dashboard)/layout.tsx` API-unreachable error screen (6) is also un-migrated — an acceptable edge state, retoken during rollout.

**Restyle verification procedure (STANDARD for every remaining screen with interaction logic).** A visual-only restyle of an interactive component MUST be proven to change nothing but presentation, using both checks:
1. **Insertion/deletion symmetry** — `git diff --stat <file>` must show **equal insertions and deletions** (an N/N line swap). Any asymmetry means lines were added/removed = structural/logic change → investigate.
2. **Handler-token grep of the diff** — `git diff <file> | grep -E '^[+-]' | grep -vE '^(\+\+\+|---)' | grep -iE 'useState|useEffect|useCallback|useRef|useMemo|onChange|onClick|onKeyDown|=>|<handler names>'`. Every surviving line must differ **only** inside its `className`/style string; the handler/hook/ref/state text on the `-` and `+` sides must be byte-identical. If a match shows changed logic, STOP and report rather than working around it.
This is how `CaptureGrid.tsx` was verified (58/58 swap; only the `onBack` button's className differed). Apply it to every interactive screen in the page rollout.

**Colour-rollout follow-ups (dedicated passes AFTER the token rollout — not inline):**
- **Semantic colour-scale unification (CAPS bands + risk/intervention bands).** Multi-level SEMANTIC colour scales are DEFERRED from the mechanical colour rollout and left hardcoded as documented exceptions, because (a) they're semantic state, not brand — they stay fixed regardless of a school's primary colour, so they do NOT block per-school branding; and (b) the token palette (`danger`/`warning`/`success`) can't represent them 1:1 (7 CAPS levels; 3 risk tiers where `AtRisk`-orange and `Watch`-amber both collapse to `warning`) — forcing a collapse inline would pre-empt this pass and risk inconsistency. Scope: `CAPS_LEVEL_COLOURS` (L1–L7), intervention `BAND_STYLE`/inline band chips (`Priority`/`AtRisk`/`Watch`), `FLAG_LABELS`, `RiskDot`, and any per-level lookup map. The pass audits EVERY surface that renders a CAPS level or risk band (gradebook overview, reports, matric, learner card…), decides whether to extend the token set with the needed semantic hues, and unifies them to one shared band-colour helper so a level reads the same colour everywhere. Its own verification — not a colour rollout. (Value-conditional one-off status — e.g. `avg<40 ? danger`, `attendance<80 ? warning` — IS migrated inline during the rollout; only the fixed per-level LOOKUP MAPS are deferred.)
- **Modal-primitive adoption.** The ~8 inline modal copies are being retokenized in place during the rollout (converting them mid-rollout would be structural, break the insertion/deletion-symmetry guarantee, and change the behaviour surface). A separate pass should migrate them to the `Modal` primitive with its own verification.

**Done so far (Sprint 1.6 Step 6):** tokens + type + primitives + app shell (sidebar/header/mobile-nav) + three screens — **Login**, **teacher Dashboard** (welcome banner + CTA, saturated `My classes` cards, KPIs, grading queue, upcoming-assessments table, right rail = profile + mini-calendar + teacher-scoped "needs attention"), and the **Marks Capture grid** (`CaptureGrid.tsx`/`CaptureTab.tsx`, full-width, restyle-only — zero behaviour change). **Note:** the brief's "at-risk alerts" in the dashboard rail is school-wide (`analytics.view_school`) and teachers deliberately don't hold it — so the teacher rail shows teacher-scoped signals instead; true at-risk belongs on the Principal/HOD dashboards (follow-up).

## Sprint 1.5.1 — Pathways v1 (Complete)

### New Entities (global reference data — no SchoolId)
| Entity | Table | Purpose |
|---|---|---|
| `University` | `universities` | SA public universities (all 26 seeded — Sprint 1.5.1 Gap 4; each block in `PathwaysSeedData.SeedExpandedUniversitiesAsync` cites its official source URL; own-scale universities seed `MinimumAps = 0` + requirement in `ApsNotes`) |
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
- Model: Google Gemini free tier via `IGeminiService` (was `claude-sonnet-4-6` pre-1.5.2) — see "AI Tutor" section; per-call cost is 0
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

## AI Tutor (Sprint 1.5.2)

- **AI Provider:** Google Gemini (free tier)
- **Model:** `gemini-3.1-flash-lite` (config-driven via `Gemini:Model` in appsettings.json — update the value if a better free-tier model becomes available)
- **Key:** set via user-secrets as `Gemini:ApiKey`
- **Why Gemini:** Anthropic API costs not viable at pre-revenue pilot stage. Switch back to Claude when schools are paying. The system prompt and service interface are provider-agnostic.
- **Rate limit:** 20 questions/day per learner (configurable via `School.Settings.MatricTutorDailyLimit`)

All four AI services (Matric tutor, gap analysis, smart reports, Gr9 advisor) route through the single `IGeminiService`; usage rows log at cost 0 on the free tier.

---

## Marks Capture (Sprint 1.5.2.5)

CAPS mark capture for teachers — the highest-frequency teacher workflow. Weeks 1-2 (data model + capture grid) shipped; Week 3 (HOD approval/moderation) is paused pending the HOD interview (the "Submit for Review" button is built but disabled).

### Endpoints (all on `GradebookController`)
| Endpoint | Permission | Purpose |
|---|---|---|
| `GET /api/gradebook/{classSubjectId}/tasks` | `marks.view_class` | Task list for a class-subject (captured x/y counts, approval status) |
| `GET /api/gradebook/{classSubjectId}/task/{taskId}/marks` | `marks.view_class` | Full capture-grid payload: criteria + every enrolled learner with current mark, `isAbsent`, `criteriaScores` |
| `POST /api/gradebook/bulk-capture` | `marks.capture` | Save marks for many learners in one task (the security-critical write) |
| `POST /api/gradebook/tasks` | `marks.capture` | Create a task; accepts a criteria array when `HasRubric` |
| `PUT /api/gradebook/tasks/{taskId}` | `marks.capture` | Update task details (rubric criteria are immutable post-create) |
| `GET /api/gradebook/my-grades` | `marks.view_own` | Learner's own marks; includes per-criterion breakdown when the task has a rubric |

`marks.capture` is a **teaching-role** key (TC-1: oversight roles like HOD/Principal deliberately cannot capture — they can open the grid read-only via `marks.view_class`, but saves 403 server-side). All logic (validation, scope, audit) lives in `MarkCaptureService`; the controller is a thin passthrough.

### Absent ≠ zero
`Grade.IsAbsent = true` **requires `Grade.Score = null`** — enforced in `MarkCaptureService` before any DB write and again on the audit path. Absent is *not* a zero; a zero is present-scored-nothing. `Grade.Score` is nullable precisely so absent (and not-yet-captured) rows carry no number, and **every average/aggregation must exclude `IsAbsent` rows** — SQL `AVG(score)` ignores NULLs, so this is automatic for straight averages, but explicit filters are still required anywhere absents could otherwise be counted.

### Audit on change, not on initial capture
`MarkCaptureAuditLog` records **corrections, never first entries**. The rule in `BulkCaptureAsync`:
```csharp
var hadCapturedMark = grade.Score != null || grade.IsAbsent;
var differs = grade.Score != newScore || grade.IsAbsent != entry.IsAbsent;
if (hadCapturedMark && differs) { /* write audit row */ }
```
- New grade row → no log (initial capture).
- Existing row whose score was still `null` (pending — e.g. a partially-entered rubric) being filled in → no log (still initial capture).
- A previously-captured value/absent-state changing → log (PreviousScore/NewScore, PreviousIsAbsent/NewIsAbsent, ChangedByUserId, ChangedAt). The Grade FK is `RESTRICT` so audit rows survive.

### The 4-query cross-tenant validation pattern (ESTABLISHED — reuse it)
**Any endpoint that processes a list of student entries** (bulk-capture, and future submission / attendance bulk endpoints) MUST validate in a fixed number of queries, never N-per-entry:
1. Resolve `classSubjectId` scoped to `SchoolId` (404 if foreign).
2. `IScopeService.EnsureClassAsync` on its class (403 if in-school but out of the caller's scope).
3. Resolve `taskId` (or equivalent) scoped to both `SchoolId` and that class-subject (404 otherwise).
4. `IScopeService.GetEnrolledStudentIdsAsync(classSubjectId, schoolId)` — **one** query loads the active-enrolment student ids into a `HashSet`; each entry's `studentId` is then checked **in memory** (O(1)), never re-queried.

Result: 4 security queries regardless of class size, not 4 + N. `IScopeService` exposes `GetEnrolledStudentIdsAsync` (batch, active-only, school-pinned) and `IsStudentInClassAsync` (single-id convenience — **do not call it in a loop**). Pinned by `MarksCaptureCrossTenantWriteTests` (all three body-id directions) + `ScopeServiceEnrolmentTests`.

### Rubric vs simple entry
- `Assignment.HasRubric` (bool) picks the mode. Also on `Assignment`: `SbaWeight` (decimal(5,2)?, 0–100, service-validated), `TermNumber` (int?, 1–4, service-validated), and `TaskType += PAT` (Practical Assessment Task; `task_type` is a **string column, not a pg enum** — new values need no `ALTER TYPE`).
- **Simple:** one `Grade.Score` per learner, `0..MaxMarks`.
- **Rubric:** `AssessmentCriteria` rows (name + `MaxMark` + `DisplayOrder`) hang off the task; each learner gets a `CriteriaScore` per criterion (`Score` nullable — null = pending, distinct from 0; unique `(GradeId, CriteriaId)`). The task **total is server-derived** — `Grade.Score` is set to the sum of entered criteria; a client-sent total is ignored. Task `MaxMarks` for a rubric is the sum of criterion maxima, computed on create.
- **Grade is decoupled from Submission** (Sprint 1.5.2.5): `Grade.StudentId` + `Grade.AssignmentId` are authoritative (unique pair); `Grade.SubmissionId` is nullable and set only by the LMS submission-grading flow (capture-grid marks leave it null). Read paths must not assume a submission exists.

### At-risk dashboard: marks source + Week 3 approval seam
The at-risk judgment lives in **ONE place — `AtRiskService.EvaluateAsync` (Sprint 1.5.3)**, the shared primitive both the Matric risk dashboard (`MatricHubService`) and Smart Reports (`SmartReportsService`) route through; neither keeps its own at-risk logic. It **reads all captured marks** — deliberately not gated on approval status, because nothing else in the platform gates on approval yet (marks appear on My Academics the moment they're captured); a stricter dashboard would be inconsistent. All at-risk marks flow through **one seam — `AtRiskMarks.CapturedPredicate(schoolId)`** (not absent, score present). **When HOD approval (Marks Capture Week 3) ships, gate on `approval_status = Approved` at that single predicate** — append `&& g.Assignment.ApprovalRecords.OrderByDescending(r => r.ReviewedAt).First().Status == ApprovalStatus.Approved` there and nowhere else. Tracked as a Week 3 follow-up. The intervention band (Watch/At Risk/Priority) uses the **50% intervention line** counting only captured subjects, distinct from the per-subject CAPS bands (red <40 / amber <50 / green ≥50). `AtRisk_BothSurfaces_AgreeForSameLearner` guards against re-divergence.

**Known divergence risks (route through `AtRiskService` when touched):**
- **`AnalyticsController.GetAtRiskStudents`** uses its OWN at-risk calc (attendance-only, submission-join). Not migrated in 1.5.3 — should route through `AtRiskService` when next touched, or it will drift from the authoritative judgment.
- **`ReportsController.GetTermReport`** uses its own grade reads, but that's fine — it's a different feature (per-learner term reports, not the at-risk judgment), not a divergence risk.

---

## Key Constraints

- `RowVersion` is `long`, not `byte[]` — Postgres has no `rowversion` type.
- `School.Theme` / `School.Features` are `jsonb` — requires `.EnableDynamicJson()` on Npgsql data source (already configured in `Program.cs`).
- Attendance bulk upsert uses `ON CONFLICT DO UPDATE` — the SQL Server TVP approach was removed during the Supabase migration.
- `AdditionalServices.cs` contains many classes back-to-back — always confirm you're inside the correct class before adding methods.
- Building while the server is running fails with MSB3027 DLL-lock errors — kill PID first.
