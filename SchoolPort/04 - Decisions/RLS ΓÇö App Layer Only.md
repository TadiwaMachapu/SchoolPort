---
date: 2026-06-19
status: decided
sprint: "[[Sprint 1.5.0 — Security Layer]] Step 11"
---

# Decision: RLS — Application Layer Only (Pre-Scale)

## Context
During the Step 11 RLS audit, we found all 68 application tables have RLS enabled but zero policies and not forced. The app connects as `postgres` (rolbypassrls=true), so RLS is architecturally inert for the application.

## The decision
**Accept application-layer-only tenant isolation for the pilot.** Log database-layer RLS as a pre-scale hardening item.

## Rationale
- The app connects as `postgres` which bypasses RLS regardless — policies would need a connection-role change to take effect
- Adding RLS requires: non-owner DB role + schoolId threaded as JWT/GUC claim + policies for all 68 tables + testing it can't lock out the app
- The application layer is proven: 188 tests, [CrossTenantGuard] scanner, SECURITY.md documents it
- RLS is the right eventual backstop, but the connection-role rework is a meaningful project that shouldn't gate the pilot

## What we did fix (pre-pilot)
The Data API (`/rest/v1/`) WAS leaking cross-school data — aggregate views were readable with the public anon key. Migration 006 locked this down (revoked anon/authenticated from public schema).

## The pre-scale plan
Before scaling beyond ~5 schools, implement:
1. Create a non-owner DB role for the app
2. Thread schoolId as a GUC claim per request
3. Write RLS policies for all application tables
4. Test thoroughly before enabling

## Related
- [[Three-Layer Security Model]]
- [[Sprint 1.5.0 — Security Layer]]
