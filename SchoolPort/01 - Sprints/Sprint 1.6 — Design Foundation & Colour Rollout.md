# Sprint 1.6 — Design Foundation & Colour Rollout

---
sprint: 1.6
status: SHIPPED — design foundation (PR #16) + hardcoded-colour rollout (PR #17) both MERGED to main
pr: 16, 17
shipped: 2026-07-21 (PR #16 design foundation), 2026-07-22 (PR #17 colour rollout)
gate: MERGED to main — PR #16 merge commit 8abaf969 (2026-07-21), PR #17 merge commit 74d14376 (2026-07-22); post-merge CI green all 4 jobs (Backend 302/302, Frontend, Migration replay, Architecture contract)
---

## Goal
Give SchoolPort a real visual identity and make per-school branding *possible* — a token-driven design system, then migrate every screen off hardcoded palette utilities so a school's primary colour actually propagates.

## Shipped — design foundation (PR #16, MERGED 2026-07-21 `8abaf969`)
The Sprint 1.6 visual overhaul. Establishes the token system that everything else hangs off.
- **Single source of truth:** the Tailwind v4 `@theme` block in `app/globals.css` — no hardcoded hex outside that file. Palette: `primary` (green 50–900), `secondary` (coral — decorative variety only, never status), semantic `danger`/`warning`/`success` (CAPS bands depend on these, kept distinct from brand), `surface`/`text`/`border` neutrals, radii, `--shadow-card`.
- **Typography:** Plus Jakarta Sans via `next/font/google` in `app/layout.tsx` (400/500/600/700), exposed `--font-jakarta` → `--font-sans`.
- **Brand vs craft split.** *Brand layer* (school-configurable at runtime): logo, primary colour, name — a school overrides `--color-primary` via the inline style in `app/(dashboard)/layout.tsx`, so `bg-primary`/`text-primary`/etc. resolve per-school without touching components (only the primary *hue*; the tint scale is craft). *Craft layer* (SchoolPort-owned, NOT configurable): typography, spacing, radii, component design. `SchoolTheme.FontFamily` deliberately UNCONSUMED (font loads globally; a school changing typeface would break design quality) — seeded on Greendale only for correctness.
- **Primitives + shell:** `stat-card`, `empty-state`, `avatar`, `modal`, `page-with-rail`; app shell (sidebar as light floating card, header, mobile-nav) — nav still derives from shared `deriveNav` (restyle only, gating never touched).
- **Three screens delivered:** Login, teacher Dashboard, Marks Capture grid (`CaptureGrid`/`CaptureTab`, restyle-only, zero behaviour change) — all verified LIVE against the real .NET API (real data + runtime theme override). Plus neutral-tile + login-pill fixes and CORS allowlist tightening. Greendale demo theme → `#4A8C2A`.

## Shipped — hardcoded-colour rollout (PR #17, MERGED 2026-07-22 `74d14376`)
The branding prerequisite. Migrates ~1,997 hardcoded palette utilities across 58 `.tsx` files onto the design tokens, so a school's primary colour actually propagates instead of leaving every element blue/grey.
- **Frontend only — colour-token swap.** 58 files (all `.tsx`/globals). **No `.cs`, `.csproj`, or migration files touched.** Backend behaviour unchanged; **302/302 backend tests confirmed green in CI** (verified from the job log, not assumed — this was the first full-suite run on the branch).
- Delivered as 8 sequential `color-rollout N/8` passes: (1) gradebook, (2) reports, (3) matric hub, (4) pathways, (5) academics/learner, (6) admin/staff-ops, (7) comms/finance/compliance, (8) shared globals.
- **Verification discipline:** each pass used a handler-token grep of the diff to guarantee only `className`/style strings changed — handler/hook/ref/state text byte-identical on `-`/`+` sides.
- Clean post-merge reconciliation: local main = origin/main = `74d14376`, working tree clean, remote branch pruned.

## Key decisions made
- **Semantic scales are NOT brand.** Multi-level SEMANTIC colour scales (7 CAPS levels; 3 risk tiers where AtRisk-orange and Watch-amber both collapse to `warning`) are deliberately left hardcoded during the mechanical rollout — they're fixed regardless of a school's colour, so they do NOT block branding, and the `danger/warning/success` tokens can't represent them 1:1. Unifying them is its own pass (below).
- **Value-conditional one-off status IS migrated inline** (e.g. `avg<40 ? danger`); only fixed per-level LOOKUP MAPS are deferred.
- **stat-card `color` prop map is now token-backed** — blue→primary, green→success, orange→warning, red→danger; purple→secondary and teal→primary intentionally collapse (no violet/teal token family).

## Branding-sprint backlog (carried forward)
The seven follow-ups that gate/accompany the branding sprint. Each is its own work item with its own verification — none are inline to the rollout.
- [ ] **Semantic colour-scale unification** — audit every surface rendering a CAPS level (L1–L7) or risk band (`Priority`/`AtRisk`/`Watch`, `BAND_STYLE`, `FLAG_LABELS`, `RiskDot`, any per-level lookup map), decide whether to extend the token set with the needed semantic hues, and unify to one shared band-colour helper so a level reads the same colour everywhere (gradebook overview, reports, matric, learner card…).
- [ ] **Info-tone token gap** — the palette has no neutral-informational tone, so states like "graded"/"submitted"/"draft" collapse to green-on-green or stay un-migrated (symptom: `AssignmentsTab.StatusBadge` graded=blue left deferred). Add an `info-100/500/700` family (blue-based, distinct from primary and status hues) and migrate the deferred state badges. Resolve alongside the semantic-scale pass. NOT a semantic scale — a token-set gap.
- [ ] **Gradient design pass** — the design brief bans gradients; `courses.GRADIENTS` (6 rotating course-thumbnail gradients) and any other decorative gradients can't be mechanically swapped (removing one needs a replacement decision: flat tint / subject-coded colour / icon treatment). Small design pass to decide + apply.
- [ ] **Chart-theming pass (colour-as-prop, NOT colour-as-class)** — chart libs take colour as literal props, structurally invisible to any utility-based migration (never caught by a `bg-*`/`text-*` grep). Found: `analytics/page.tsx` recharts (`GRADE_COLORS` hex array + `<Pie>`/`<Bar>` `fill="#3b82f6"`). Consequence: a school changing its colour sees the whole app follow EXCEPT the charts. recharts accepts CSS vars → feed chart props from token vars. Audit EVERY chart, not just the incidental find.
- [ ] **Category-colour overflow** — colour-as-prop / fixed-hue category maps that overflow when categories exceed the available distinct hues (same colour-as-prop blind spot as charts — inline `style`, canvas/SVG, subject/category badge cycles). Sweep for colour-as-prop generally, not only recharts.
- [ ] **Branding write-path bug** — the school branding/primary-colour persistence path (`school.manage` → SchoolsController theme write) needs review so a saved brand actually takes effect end-to-end. Includes the unconsumed `SchoolTheme.FontFamily` (consider removing — nothing reads it). *(Carried per branding backlog; confirm exact repro against the design-tokens section of CLAUDE.md when the sprint starts.)*
- [ ] **At-risk query performance** — the At-Risk tab hit its loading spinner noticeably even at Greendale's 3 learners during the rollout live-render pass. If slow at 3, unusable at 800. Likely: `AtRiskService.EvaluateAsync` query shape, missing index on the captured-marks path (`grade → submission → assignment → class_subject`, no direct `(school_id, student_id, term_id)` index), or per-learner N+1. **Must be profiled at realistic scale (500–1200 learners), not the 3-learner demo.** Not yet investigated.

## Also deferred (documented rollout exceptions, not the branding backlog)
- **Modal-primitive adoption** — the ~8 inline modal copies were retokenized in place (converting mid-rollout would break the insertion/deletion-symmetry guarantee). Separate pass migrates them to the `Modal` primitive.
- `(dashboard)/layout.tsx` API-unreachable error screen (6 utilities) left un-migrated — acceptable edge state.
- Teacher dashboard "at-risk alerts" in the rail: true school-wide at-risk needs `analytics.view_school` (teachers don't hold it) → teacher rail shows teacher-scoped signals; real at-risk belongs on Principal/HOD dashboards.

## Test count
Backend before: 302 | after: 302 (untouched — colour-only rollout, confirmed in CI).

## Related
- [[Sprint 1.5.3 — Smart Reports]]
- [[Henco Interview — Design Teacher]]
