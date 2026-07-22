---
date: 2026-07-22
status: pending
sprint: "[[Sprint 1.6 — Design Foundation & Colour Rollout]]"
affects: AtRiskService, at-risk dashboard, pre-pilot performance
---

# Decision: At-Risk Query Performance

## Context
Logged during the [[Sprint 1.6 — Design Foundation & Colour Rollout]] live-render pass. CLAUDE.md records it verbatim under **Pre-pilot performance items**:

> **At-risk query is visibly slow on the Greendale demo (3 learners) — profile before pilot.** Observed in the Sprint 1.6 colour-rollout live-render pass: the At-Risk tab hit its loading spinner noticeably even at Greendale's 3 learners. If it's visibly slow at 3, it will be unusable at 800 — and at-risk is the feature teachers were most explicit about wanting. Likely candidates: `AtRiskService.EvaluateAsync` query shape, missing index on the captured-marks path (see the deferred-indexes note above — marks are reached via `grade → submission → assignment → class_subject`, no direct `(school_id, student_id, term_id)` index), or a per-learner N+1. **Must be profiled at realistic school scale (500–1200 learners), not on the 3-learner demo.** Not yet investigated — logged during the rollout, to be picked up after it.

## Decision
**Pending** — no fix decided yet. The finding is captured and owned here so it is tracked rather than buried in prose. The next step is to profile before committing to a remedy (query reshape vs. a covering/denormalised index on the join path vs. an N+1 fix), because the right fix depends on which of the three candidates dominates at scale.

- [ ] Profile `AtRiskService.EvaluateAsync` at realistic school scale (500–1200 learners), not the 3-learner Greendale demo, and decide the remedy.

## Consequences
- Blocks nothing structurally today (demo works), but is a **pre-pilot blocker**: at-risk is the most-requested teacher feature and cannot ship unusable at real scale.
- Related to the deferred marks-path indexes ([[Sprint 1.5.0.5 — Performance]] / Sprint 1.5.4): there is no direct `(school_id, student_id, term_id)` index on the captured-marks join path.

## Related
- [[Sprint 1.6 — Design Foundation & Colour Rollout]]
- [[Sprint 1.5.3 — Smart Reports]]
- [[Sprint 1.5.0.5 — Performance]]
- [[Greendale High School (Demo)]]
