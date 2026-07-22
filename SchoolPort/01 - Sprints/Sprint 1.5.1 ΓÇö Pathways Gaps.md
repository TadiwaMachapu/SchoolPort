---
sprint: "1.5.1"
status: shipped
completed: 2026-07-06
---

# Sprint 1.5.1 — Pathways v1 Completion (Gap Closing)

## Context
Pathways v1 was scaffolded and marked complete on 2026-06-02. On audit, the scaffold was functionally present but had five correctness gaps that make it not production-ready. This sprint closes them.

## What already existed (from the scaffold)
- PathwaysController with 15 endpoints — all [RequirePermission]-gated and IDOR-scoped
- PathwaysService.GetLearnerApsAsync — live APS calculation (7-point CAPS scale, best-6-excluding-LO)
- PathwaysService.GetMatchingProgrammesAsync — university matching
- PathwaysService.GetImprovementPlanAsync — what to improve for a target programme
- APS goal tracking with RAG status (Red/Amber/Green)
- AI gap analysis with SHA-256 cache + ZAR cost cap
- Grade 9 advisor (Gr9AdvisorService)
- SeniorPhaseRequirement prerequisite graph
- 10 SA universities seeded with faculty APS minimums
- 20 careers, ~30 courses
- Frontend Pathways pages exist

## The five gaps being closed

### Gap 1 — Matview weighting (DONE)
`vw_matric_aps_summary.projected_aps` was a flat sum with no best-6-excluding-LO logic. The live calculator and the matview disagreed.

**Fix:** Migration 008_fix_matric_aps_view — rewrote the view with:
- `projected_aps` = best 6 non-LO subjects (matches CalculateApsPoints)
- `total_aps` = all subjects, LO capped at 4 points (matches LearnerApsResult.TotalAps)
- `::int` cast (old view returned bigint, EF entity maps int)

**Column semantics (important):**
- `projected_aps` — use for university admission comparisons and goal tracking
- `total_aps` — use for the broader picture including all subjects

**Per-surface APS read source:**
- Learner/parent dashboards → live calculation (current accuracy required)
- Aggregate views (principal dashboard, Smart Reports cohort) → vw_matric_aps_summary

### Gap 2 — Year scoping
GetLearnerApsAsync averages all grades ever recorded, not just the current academic year. Wrong once a school accumulates multi-year data.

**Fix:** Filter grade averages to current AcademicYear in the live calculator.

### Gap 3 — Subject name normalisation  
CourseSubjectRequirement and prerequisite map join on SubjectName strings (OrdinalIgnoreCase). A school renaming "Mathematics" silently breaks prerequisite matching.

**Fix:** Normalise against CAPS standard names. Surface mismatches as a warning, not a silent miss.

### Gap 4 — University seed breadth
10 of 26 SA public universities seeded. Expand to all 26 with accurate 2025/2026 APS minimums.

### Gap 5 — Documentation
Per-surface APS source decision documented in CLAUDE.md (see above under Gap 1).

## Ethical guardrails (non-negotiable, already implemented)
- Never say a learner "cannot" do something
- Always frame as "you need to improve X by Y points"
- Show effort required, not capability ceiling
- LO teacher/counsellor review flag before results show for the first time

## Related
- [[Pathways Feature]]
- [[Sprint 1.5.0.5 — Performance]]
- [[Henco Interview — Design Teacher]]
