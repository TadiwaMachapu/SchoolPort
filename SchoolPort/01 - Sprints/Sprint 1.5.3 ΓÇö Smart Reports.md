# Sprint 1.5.3 — Smart Reports v1

---
sprint: 1.5.3
status: planned
gate: After Sprint 1.5.2.5 (needs marks data)
---

## Goal
Henco's most-wanted feature: "A dashboard that identifies at-risk learners and outstanding tasks automatically."

## At-risk thresholds
- **50%** — intervention threshold (NOT 40%). The 40% threshold in matviews is the CAPS pass line. A learner at 42% is technically passing but urgently needs help.
- **Watch:** below 50% in 1 subject
- **At Risk:** below 50% in 2 subjects
- **Priority:** below 50% in 3+ subjects OR declining >10% term-over-term

## At-risk signals
- Below 50% in a subject
- Declining trend (>10% drop term-over-term)
- Missing 2+ assessments
- Attendance below 80%
- Combination: declining + absent = Priority regardless of mark

## Views by role
- **Teacher** — their classes only (scoped), at-risk learners in their subject
- **Grade Head** — all learners in grade, cross-subject risk, priority learners first
- **HOD** — all learners in subject, teacher comparison, missing submissions
- **Principal** — school-wide risk summary, per-grade, per-subject breakdown

## AI-assisted report comments
- Teacher selects learner → sees performance data → AI suggests comment → teacher edits/accepts
- Prompt: "Write a CAPS-aligned school report comment for a [grade] [subject] learner..." (see CLAUDE.md)
- Major time-saver during report season (Henco Q35: "high-pressure, time-sensitive")

## Debounced background view refresh
- Finally built here (deferred from Sprint 1.5.0.5)
- Refresh at most once per 5 minutes per school after marks are captured
- NOT per-save (bulk capture would trigger 30 refreshes)

## New permissions needed
- `reports.generate_comment` → SubjectTeacher, ClassTeacher, LOTeacher, HOD, GradeHead, Principal, Deputy

## Related
- [[Sprint 1.5.2.5 — Marks Capture]]
- [[Henco Interview — Design Teacher]]
