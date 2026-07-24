---
date: 2026-07-22
status: resolved
sprint: "[[Sprint 1.6 — Design Foundation & Colour Rollout]]"
affects: SchoolTheme, per-school branding, SchoolsController
---

# Decision: Branding Write-Path Bug

## Context
Carried from the [[Sprint 1.6 — Design Foundation & Colour Rollout]] branding-sprint backlog as "branding write-path bug". **Honesty note:** CLAUDE.md does **not** document a confirmed persistence/write-path bug — it describes the primary-colour override as *working*, and records two real branding follow-ups. This note quotes CLAUDE.md's actual wording rather than the chat-reconstructed framing, so the stub tracks what is genuinely documented.

CLAUDE.md, **Brand layer** (documented as working):

> **Brand layer — school-configurable at runtime:** logo, primary colour, name. A school overrides `--color-primary` via the inline style injected in `app/(dashboard)/layout.tsx` (`style={{ "--color-primary": theme.primaryColor }}`). `bg-primary`/`text-primary`/`border-primary`/`ring-primary` all resolve to that var, so per-school branding works without touching components. Only the primary *hue* is overridden; the tint scale (`primary-100…900`) is craft.

CLAUDE.md, **Craft layer** (documented follow-up):

> **`SchoolTheme.FontFamily` is deliberately UNCONSUMED by the frontend** — the font loads globally via `next/font`; a school changing the typeface would break the design quality. It is seeded (`Plus Jakarta Sans` on Greendale) only for correctness. **Follow-up (branding sprint): consider removing `FontFamily` from `SchoolTheme`** — nothing reads it.

CLAUDE.md, **hardcoded-colour blocker** (the real propagation gap, closed by [[Sprint 1.6 — Design Foundation & Colour Rollout]]'s colour rollout but worth confirming end-to-end):

> **⚠️ Hardcoded colour utilities BLOCK per-school branding.** … Migrating them to `bg-primary`/`text-primary`/token utilities is a **prerequisite for the branding sprint**.

## Decision
**Resolved (2026-07-24)** — the verify surfaced a **real persistence gap**, exactly the "or a persistence gap" branch anticipated under Consequences.

**Root cause confirmed:** the per-school jsonb POCO columns on `School` (`Theme`, `Features`, `Settings`) are mapped via Npgsql `EnableDynamicJson()` with **no `ValueComparer`**, so EF Core used **reference equality** for change tracking and could not detect in-place mutations. `SchoolService.UpdateThemeAsync` mutates `school.Theme.PrimaryColor = …` on the same instance → `SaveChanges` returned 200 and bumped `updated_at` but **silently never wrote the `theme` column**. (Same defect class hit `UpdateFeaturesAsync` and the settings write paths.)

**Fixed in** commit `cece4c7c` (PR #18 `fix/jsonb-write-path`) — added a deep JSON `ValueComparer` (structural equality + serialize→deserialize snapshot) to all three jsonb properties in `SchoolPortalDbContext`; no migration.

**Verified by direct DB query:** saving `PrimaryColor` `#4A8C2A → #123456` via `PUT /api/schools/theme` (Greendale admin) then reading the row directly showed the `theme` jsonb **persisted the change** (`FontFamily` preserved); a features toggle likewise persisted with siblings unchanged. Full suite 302/302 green. (Demo values restored afterward.)

**Re-confirmed LIVE in production (2026-07-24, [[Sprint 1.6.1 — SuperAdmin Audit Trail]]):** the fix is deployed on `main` and exercised against the live DB, not just theoretically. A real feature toggle through the SuperAdmin console persisted correctly AND its audit row landed atomically in `super_admin_audit_logs` (`{"virtualClassroom": false}` → `{"virtualClassroom": true}` for Greendale) — verified by direct query. Because the flag write and the audit write share one `SaveChanges`, a landed audit row is proof the jsonb write path itself works in production. 312/312 tests green; the `ValueComparer` is covered by the DbContext-level suite. **This decision is closed — fix is live, tested, and verified in production.**

- [x] Verify the branding write path end-to-end (save primary colour → `School.Theme` persisted). Persistence gap found and fixed.
- [ ] Still open (separate, non-blocking): decide whether to remove the unconsumed `SchoolTheme.FontFamily`.

## Consequences
- If the write path is sound, this closes to `decided` with no code change beyond the optional `FontFamily` removal.
- If a saved brand does NOT propagate (e.g. a chart/colour-as-prop surface, or a persistence gap), it re-scopes into a real branding-sprint fix — overlaps the [[Sprint 1.6 — Design Foundation & Colour Rollout]] chart-theming / category-colour follow-ups.

## Related
- [[Sprint 1.6.1 — SuperAdmin Audit Trail]] — the sprint whose jsonb `ValueComparer` fix closed this note; re-verified live
- [[Sprint 1.6 — Design Foundation & Colour Rollout]]
- [[Greendale High School (Demo)]]
