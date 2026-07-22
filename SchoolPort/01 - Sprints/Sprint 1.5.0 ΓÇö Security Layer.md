---
sprint: "1.5.0"
status: shipped
completed: 2026-06-16
---

# Sprint 1.5.0 — Identity, Positions, Permissions Security Layer

## Goal
Install a three-layer security model across the entire platform: Identity → Positions → Permissions. Deny-by-default, tenant-isolated, audit-logged.

## What was built

### The three-layer model
- **Identity** — one per user: Staff, Learner, Parent, External, System
- **Positions** — many per user, scoped (SubjectTeacher, HOD, GradeHead, PhaseHead, Principal, DeputyPrincipal, FinanceManager, BursarDebtorsClerk, Cashier, ITAdministrator, etc.)
- **Permissions** — atomic verbs attached to positions, deny-by-default

### Steps completed
- **Step 1-2:** Data model (Position, UserPosition, Permission, AuditLog) + migrations + backfill of 14 users
- **Step 3:** PermissionResolver, ICurrentUserService, tiered JWT TTLs (8h staff / 1h Finance / 30min System)
- **Step 4:** [RequirePermission] attribute, deny-by-default FallbackPolicy, governance scanner
- **Step 5:** JWT identity+positions, TenantMiddleware, RefreshToken entity
- **Step 6:** All 35 controllers migrated, Finance SoD corrections (FIN-1/2/3)
- **Step 7:** Service-layer scope filtering, IDOR protection, IScopeService
- **Step 8:** Frontend permission-gated UI, AuthProvider hooks, sidebar rebuild, My Academics page
- **Step 9:** Onboarding presets, staff CSV import, position management UI
- **Step 9.5:** Teacher scope enforcement, H1/H2 cross-tenant fixes
- **Step 10:** 170-test security suite, [CrossTenantGuard] scanner, 19 endpoints hardened
- **Step 11:** SECURITY.md, CLAUDE.md cleanup, migration baseline, Data-API lockdown

## Key decisions
- [[Finance SoD Controls]] — FinanceManager lost exempt_approve; Bursar lost create_invoice
- [[Nav Rules by Role]] — sidebar derives from positions, not role strings
- [[RLS — App Layer Only]] — deliberate decision, documented in SECURITY.md
- [[HTTP QUERY Method]] — adopted in .NET 10, MapQuery extension built

## Critical findings during this sprint
- **19 endpoints hardened** — all had the same root cause: foreign ID in request body, no SchoolId validation before write. See [[Cross-Tenant Write Rule]]
- **Data-API leak closed** — vw_matric_aps_summary was readable with the public anon key. Migration 006 locked it down.
- **Sarah bug** — react-query cache not cleared on account switch. Fixed with queryClient.clear() on login/logout.

## Test count
Before: 32 | After: 170 (zero exclusions)

## Related
- [[Three-Layer Security Model]]
- [[Permission Catalogue]]
- [[Cross-Tenant Write Rule]]
- [[Sprint 1.5.0.5 — Performance]]
