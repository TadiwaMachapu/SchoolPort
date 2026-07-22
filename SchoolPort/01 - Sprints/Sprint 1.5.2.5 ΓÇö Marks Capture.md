---
sprint: "1.5.2.5"
status: in-progress
completed: 
---

# Sprint 1.5.2.5 — Marks Capture & Approval

## Goal
The most important sprint in Phase 1.5. Every teacher uses this every week. Getting it right determines whether teachers adopt the platform.

## Shipped — Weeks 1-2 (PR #12, CI green 2026-07-11; not yet merged)
Single clean commit off main. All CI green: backend build + tests (277, zero exclusions), frontend build, migration replay on fresh DB, architecture contract. Live spot-check passed (James Dlamini, Grade 12A): rubric entry + auto total, CAPS badges, absent-not-zero, live class average + distribution, autosave, unsaved indicator, rubric-builder modal.

### Week 1 — data model (migration `AddMarksCaptureSchema`, applied to live)
- **Grade decoupled from Submission**: StudentId + AssignmentId authoritative (unique pair, backfilled); SubmissionId nullable (null = capture-grid mark); Score nullable (absent ⇒ null, service-enforced — absents fall out of SQL AVG).
- New: AssessmentCriteria, CriteriaScore (score null = pending ≠ 0; unique (GradeId,CriteriaId)), ApprovalRecord (per-task, history rows, partial-unique open-record index), MarkCaptureAuditLog (append-only, Grade FK RESTRICT).
- Assignment +HasRubric/+SbaWeight/+TermNumber; TaskType += PAT (string column, no pg enum — no ALTER TYPE).

### Week 2 — API + capture grid
- MarkCaptureService + 5 GradebookController endpoints (reads marks.view_class, writes marks.capture); my-grades extended with rubric breakdown.
- Bulk-capture: 4-query cross-tenant validation + HashSet batch student check (no N+1) via new IScopeService.GetEnrolledStudentIdsAsync/IsStudentInClassAsync; rubric totals server-derived; **audit on corrections only** (filling a pending null = initial capture, no log).
- Grid: keyboard-first (Tab/Enter, skips absent rows), ABS toggle, auto CAPS badges, live stats + L1-7 distribution, 60s autosave, unsaved indicator, mobile one-learner view.
- **Submit for Review button built but DISABLED** ("HOD review — coming soon") — Week 3.

## Week 3 — PAUSED, pending HOD interview
The approval workflow below (HOD moderation view comparing distributions across teachers, approve/flag/request-correction, publish marks to learner+parent) is NOT built. Starts when HOD interview responses arrive.

## Critical context from Henco's markbook (Design, Randfontein HS, 2025)

### 1. Rubric-based entry is required
Tasks have multiple criteria scored separately. Design theory task: 5 criteria each /10:
- Expression of intention and rationale
- Evidence of research and experimentation
- Evidence of detailed planning and drawing
- Evidence of final drawing/collage/maquette
- Research: Design in a business context
Total auto-calculates. Some subjects use rubrics; Mathematics probably doesn't. Both modes required.

### 2. Absent ≠ Zero
When a learner was absent: NOT zero, explicitly absent (`IsAbsent = true`). Zero means present, scored nothing. Affects SBA calculations.

### 3. PAT is a real task type
Practical Assessment Task — year-long cumulative CAPS project (Design, Art, Drama). Special weighting rules. Add to task type enum.

### 4. Transparency is non-negotiable (Henco's requirement)
"How is this calculated?" must have a visible, honest answer. Show the formula.

### 5. HOD moderation ≠ just approval
HODs compare distributions across teachers. This is formal CAPS moderation.

### 6. Audit trail is non-negotiable (Henco's requirement)
Every mark entry, change, approval logged. Who did what when. Always visible.

## The approval workflow
```
Teacher captures → saves draft
Teacher submits to HOD
HOD sees:
  - This teacher's marks + distribution
  - Other teachers' same subject (MODERATION)
  - Missing submissions
HOD: approve | flag concern | request correction
After approval → learner + parent see marks with context
```

## Data model additions needed
- `AssessmentCriteria` entity (criteria-level entry)
- `CriteriaScore` entity
- `IsAbsent` boolean on Grade entity
- `PAT` added to TaskType enum
- `ApprovalRecord` entity (Draft→Submitted→Approved→Rejected)
- `MarkCaptureAuditLog` entity

## Term structure (CAPS)
- Term 1: Tests + SBAs
- Term 2: June Exams + Tests
- Term 3: SBAs + Tests
- Term 4: November Exams + SBAs

## Gates
- Must have HOD interview response
- Henco's Excel markbook absorbed (done — see [[Henco Markbook Analysis]])
- Starts after Sprint 1.5.2

## Related
- [[Henco Interview — Design Teacher]]
- [[Henco Markbook Analysis]]
- [[Marks Capture Feature]]
