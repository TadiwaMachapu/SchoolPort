---
sprint: "1.6.1"
status: shipped
completed: 2026-07-24
---

# Sprint 1.6.1 — SuperAdmin Audit Trail

## Goal
Close the platform's biggest accountability gap: the SuperAdmin console (`schoolportal-superadmin/`) is the only surface with **unscoped, cross-school** database access — it can activate/deactivate any school and edit any school's feature flags — yet **none of it left any record** (no who, no when, no before/after). Add an audit trail before anything else is built on this surface.

## Shipped (PR #19 `47e9787b`, merged 2026-07-24 → `f1c86640`)

### SuperAdminAuditLog
Append-only audit table, built to the **`MarkCaptureAuditLog` precedent** (deliberately NOT the generic `AuditLog`, which is dormant/unwired — see [[Branding Write-Path Bug]] sibling context and CLAUDE.md's "Audit logging" note):
- Typed columns, FKs `RESTRICT` (audit must survive), written **explicitly in the service inside the mutation's own `SaveChanges`** so the log row and its effect are one atomic transaction.
- Actor is a `SuperAdmin` (FK) — outside the school identity model; target is a nullable `School` (FK). Migration `AddSuperAdminAuditLog` (**applied to live** 2026-07-24; verified: table + 2 FK `RESTRICT` + 3 indexes + history row).

### Audited actions
`CreateSchool`, `UpdateFeatures`, `SetStatus` — every mutating SuperAdmin action logs before returning success (no-ops write nothing):
- **`UpdateFeatures` logs a compact per-flag diff** — only the flags that changed, e.g. `{"virtualClassroom": false}` → `{"virtualClassroom": true}`, never a 12-field blob.
- `SetStatus` logs old/new `isActive` + optional `reason`.
- `CreateSchool` newValue captures name/domain/features/first-admin.

### Read + frontend
- `GET /api/super/audit-log?schoolId=&actionType=&from=&to=&page=` `[RequireSuperAdmin]` — paginated, most-recent-first, actor name/email + school name joined.
- New `/audit-log` console page: table (timestamp / actor / action / target & change) with filters and an expandable row showing the full before/after JSON.

### Reusable helper
`AddAudit(...)` on `SuperAdminService` — adding a future mutating endpoint is one line, not a new pattern.

## Three bugs found & fixed along the way
This started as an audit feature; verifying it surfaced two real persistence/creation bugs, and building the frontend surfaced a third:

1. **jsonb write-path (Features / Theme / Settings)** — the per-school jsonb POCO columns were mapped via Npgsql `EnableDynamicJson()` with **no `ValueComparer`**, so EF used reference equality and **silently dropped in-place mutations** (`UpdateFeaturesAsync`, `UpdateThemeAsync`, settings). 200 returned, `updated_at` bumped, jsonb never written. Fixed with a deep JSON `ValueComparer` on all three properties (PR #18 `cece4c7c`). This is what closed [[Branding Write-Path Bug]].
2. **Orphan-school creation** — `CreateSchoolAsync` used two sequential `SaveChanges`; if the admin-user insert failed, the school was already committed as an orphan. Collapsed to a **single `SaveChanges`** (school + first user + audit atomic). Pinned by `CreateSchool_UserCreationFails_SchoolCreationRollsBackToo`.
3. **Missing postcss config** — `schoolportal-superadmin` had **no `postcss.config.mjs`**, so `@tailwindcss/postcss` never ran and **every page had rendered unstyled since the app's creation** (built CSS ~369 B → ~30 KB after). Added the standard config (matches `schoolportal-web`). Documented in CLAUDE.md ("if styling looks wrong in this app again, check this file first").

## Verification (live)
A real feature toggle through the console's endpoint produced the exact audit row in `super_admin_audit_logs` on the **live** DB — `previous_value {"virtualClassroom": false}` → `new_value {"virtualClassroom": true}`, actor `admin@schoolportal.dev`, target Greendale — confirmed by direct query. Greendale's demo flag restored afterward. Frontend re-screenshotted (audit-log + dashboard + schools) — the whole console now renders its intended dark-violet style.

## Tests
**312/312** green (302 baseline + 10 new): one row per real mutation with the correct diff; zero rows for no-ops/reads; audit+effect atomic rollback (incl. an audit-FK-failure case proving one transaction); pagination/filtering/joins. Green locally (tmpfs Postgres) and on CI (all 4 gating jobs, incl. Frontend build now emitting real CSS, and Migration replay on a fresh DB).

## Delivery
- Commit `47e9787b` on `feat/superadmin-audit-log`; **PR #19**; merged → `f1c86640`; post-merge CI green.

## Open follow-ups
- [ ] Consider removing the unconsumed `SchoolTheme.FontFamily` (carried from [[Branding Write-Path Bug]], non-blocking).
- [ ] Deciding whether to wire or retire the dormant generic `AuditLog` table is a separate, unmade decision.

## Related
- [[Branding Write-Path Bug]] — closed by this session's jsonb `ValueComparer` fix
- [[Sprint 1.6 — Design Foundation & Colour Rollout]]
- [[Greendale High School (Demo)]]
