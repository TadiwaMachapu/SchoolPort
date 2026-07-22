---
date: 2026-07-22
status: pending
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
**Pending** — this is a *verify*, not a confirmed bug. Before the branding sprint, confirm end-to-end that a school saving a new primary colour (`school.manage` → SchoolsController theme write) actually takes effect across the now-tokenised UI, and resolve the documented `FontFamily` follow-up.

- [ ] Verify the branding write path end-to-end (save primary colour → `School.Theme` persisted → `--color-primary` applied across tokenised screens), and decide whether to remove the unconsumed `SchoolTheme.FontFamily`.

## Consequences
- If the write path is sound, this closes to `decided` with no code change beyond the optional `FontFamily` removal.
- If a saved brand does NOT propagate (e.g. a chart/colour-as-prop surface, or a persistence gap), it re-scopes into a real branding-sprint fix — overlaps the [[Sprint 1.6 — Design Foundation & Colour Rollout]] chart-theming / category-colour follow-ups.

## Related
- [[Sprint 1.6 — Design Foundation & Colour Rollout]]
- [[Greendale High School (Demo)]]
