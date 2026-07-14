# Sprint 1.5.3 — Smart Reports v1

---
sprint: 1.5.3
status: gap-closing shipped (at-risk dashboard correctness) — full v1 scope partially remaining
pr: 14
shipped: 2026-07-12
gate: remaining scope (role views, debounced refresh, comment permission) not yet built
---

## Goal
Henco's most-wanted feature: "A dashboard that identifies at-risk learners and outstanding tasks automatically."

## Shipped — gap-closing (PR #14, CI green 2026-07-12; not yet merged)
The Matric at-risk dashboard (`MatricHubService`) rendered but had correctness bugs. Fixed:
- **Reads real captured marks.** Was reading via the submission join → saw zero Sprint 1.5.2.5 capture-grid marks. Now all marks flow through one seam, `GetCapturedGradesQuery(schoolId)` (Grade path; not absent, score present). Absent excluded (≠ 0); "missing" = past-due assignment with no grade record.
- **50% intervention band** (Fix 1): learner-level Watch (1) / At Risk (2) / Priority (3+ or declining >10% term-over-term), counting only captured subjects. Per-subject CAPS bands (red <40 / amber <50 / green ≥50) kept distinct. Response carries the "2 of 3 captured" honesty fraction.
- **Refinements:** single-term → declining clause can't fire, absolute count still applies; band counts only captured subjects (not enrolled-unmarked). Each with a named test.
- **Report comments** confirmed already on Gemini + `DateTimeKind.Utc` (one of the original four AI services); removed the vestigial cost cap.
- 279 backend tests (+2) + 13 vitest green. Live spot-check passed (Lethabo → Priority, "3 of 5 captured", averages match DB, trends correct).
- **Week 3 seam:** approval gate (`approval_status = Approved`) goes at `GetCapturedGradesQuery` when HOD approval ships — one line, one place.

## Still planned (NOT built in this gap-closing pass)
- Debounced background view refresh (still deferred; the matviews remain manual-refresh and, note, still read the submission join — they don't yet see capture marks).
- Role-specific views beyond the current teacher/GradeHead dashboard (HOD teacher-comparison, Principal school-wide summary).
- `reports.generate_comment` permission (comment generation currently under `reporting.view`).
- Attendance-below-80 as an at-risk signal in the intervention band (band is marks-only today).
- Unifying the second, separate at-risk calc in `SmartReportsService.GetAtRiskStudentsAsync` (still 40% subject / submission-join / no trend).

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
