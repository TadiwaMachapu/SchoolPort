# Sprint 1.5.0.5 — Performance: Indexes & Materialized Views

---
sprint: 1.5.0.5
status: complete
completed: 2026-06-15
commit: d2c709af
---

## Goal
Make the platform pilot-ready for performance before adding features. Index the hot query paths, add materialized views for aggregate data, fix N+1 queries.

## What was built

### Part 1 — AddPerformanceIndexes migration
9 indexes applied to live:
- `attendances (school_id, class_id, date)`
- `attendances (school_id, student_id, date)`
- `assignments (school_id, class_subject_id, due_at)`
- `assignments (school_id, created_by_user_id)`
- `audit_logs (school_id, timestamp DESC)`
- `audit_logs (school_id, user_id, timestamp DESC)`
- `fees (school_id, due_date)`
- `fee_payments (fee_id, created_at DESC)`
- `whatsapp_logs (school_id, status, created_at DESC)`

**Note:** 3 grades indexes were impossible — grades table is normalised, no direct class_id/term_id/student_id columns. Joins through submission → assignment → class_subject. Deferred indexes logged for Sprint 1.5.4.

### Part 2 — AddMaterializedViews migration
Three materialized views:
- **vw_subject_term_averages** — per-learner/subject/term averages. Powers My Academics Subjects tab.
- **vw_matric_aps_summary** — APS per Grade 12 learner per year. Powers Pathways APS calculator. ⚠️ Weighting bug found in Sprint 1.5.1 — see [[Sprint 1.5.1 — Pathways Gaps]]
- **vw_school_performance_summary** — school-wide stats per term. Powers principal dashboard.

Manual refresh only: `POST /api/admin/refresh-views` (system.refresh_views permission). Debounced background refresh deferred to Sprint 1.5.3.

### Part 3 — N+1 audit
- SubmissionService, SmartReportsService, GradebookController, /api/gradebook/my-academics — all clean
- **One fix:** PathwaysService.GetParentPathwaysAsync computed APS twice → deduped (2 queries → 1)

## Key decisions
- Manual-only view refresh (not per-grade-save) — bulk mark capture would trigger 30 refreshes for one operation
- Debounced background refresh deferred to Sprint 1.5.3

## Deferred indexes (Sprint 1.5.4)
- fees (school_id, student_id, status) — model doesn't exist yet
- fee_payments (school_id, student_id) — same

## Related
- [[Sprint 1.5.0 — Security Layer]]
- [[Sprint 1.5.1 — Pathways Gaps]]
