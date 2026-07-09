# Sprint 1.5.2.5 — Marks Capture & Approval

---
sprint: 1.5.2.5
status: planned
gate: Need HOD interview response + Henco's markbook absorbed
---

## Goal
The most important sprint in Phase 1.5. Every teacher uses this every week. Getting it right determines whether teachers adopt the platform.

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
