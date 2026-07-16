# Sprint 1.5.3 — Smart Reports v1

---
sprint: 1.5.3
status: v1 shipped — role views + oversight scope MERGED (PR #15); gap-closing was PR #14
pr: 14, 15
shipped: 2026-07-12 (PR #14 gap-closing), 2026-07-16 (PR #15 role views)
gate: MERGED to main 2026-07-16 (merge commit 4b96e1b6); post-merge CI green all 4 jobs (Backend 302/302, Frontend, Migration replay, Architecture contract)
---

## Goal
Henco's most-wanted feature: "A dashboard that identifies at-risk learners and outstanding tasks automatically."

## Shipped — gap-closing (PR #14, MERGED 2026-07-14 `f2182561`)
The Matric at-risk dashboard (`MatricHubService`) rendered but had correctness bugs. Fixed:
- **Reads real captured marks.** Was reading via the submission join → saw zero Sprint 1.5.2.5 capture-grid marks. Now all marks flow through one seam, `GetCapturedGradesQuery(schoolId)` (Grade path; not absent, score present). Absent excluded (≠ 0); "missing" = past-due assignment with no grade record.
- **50% intervention band** (Fix 1): learner-level Watch (1) / At Risk (2) / Priority (3+ or declining >10% term-over-term), counting only captured subjects. Per-subject CAPS bands (red <40 / amber <50 / green ≥50) kept distinct. Response carries the "2 of 3 captured" honesty fraction.
- **Refinements:** single-term → declining clause can't fire, absolute count still applies; band counts only captured subjects (not enrolled-unmarked). Each with a named test.
- **Report comments** confirmed already on Gemini + `DateTimeKind.Utc` (one of the original four AI services); removed the vestigial cost cap.
- 279 backend tests (+2) + 13 vitest green. Live spot-check passed (Lethabo → Priority, "3 of 5 captured", averages match DB, trends correct).
- **Week 3 seam:** approval gate (`approval_status = Approved`) goes at `GetCapturedGradesQuery` when HOD approval ships — one line, one place.

## Shipped — role views + oversight scope (PR #15, MERGED 2026-07-16 `4b96e1b6`)
Completes v1's role-specific scope on top of the corrected at-risk dashboard.
- **Oversight scope primitive.** `GetOversightClassIdsAsync` / `CanAccessGradeAsync` resolve which classes/grades a position may see. Grade Head / HOD / Principal endpoints are position-gated, with matching frontend tabs — each role sees only its scope.
- **At-risk judgment is term-scoped.** `AtRiskService` computes average / risk / below-50 / band from the **selected term's** marks; the previous term feeds the trend only. No marks in the selected term → `no_data`, not a false 0%. Named tests: `AtRisk_OverallAverage_And_BelowFifty_AreTermScoped`, `AtRisk_NoMarksInSelectedTerm_IsNoData_NotZero`, `AtRisk_SingleTerm_DecliningRuleDoesNotFire_AbsoluteThresholdStillApplies`.
- **Overall average unified across surfaces.** Term Report, learner card, and at-risk now report an identical avg-of-subject-averages (rounding aligned). Guarded by `OverallAverage_ConsistentAcrossSurfaces_TermScoped`.
- **Attendance false-flag fixed.** `AttendanceSignal`: Late counts as attended; sparse data → null (no signal) rather than a false at-risk flag.
- **Term Report migrated to the captured-marks path**; **CAPS levels enabled for FET**.
- DevSeed terms made relative-to-now so the term window is always live. **302 backend tests green in CI** (the term-scoping + consistency tests were the Postgres-gated ones confirmed by name on the runner). Merged to main; post-merge CI green on all 4 jobs.

## Still planned (remaining after v1)
- **Debounced background view refresh** (still deferred from 1.5.0.5; matviews remain manual-refresh and still read the submission join — they don't yet see capture marks).
- **`AnalyticsController` third at-risk surface** — the separate at-risk calc still to be unified onto the term-scoped `AtRiskService` seam (the others now flow through one path; this one is the last holdout).
- **Legacy `AiService` → Anthropic swap** — retire the vestigial legacy AI service path in favour of the current provider config.
- **`fetch-in-effect` lint cleanup** — frontend effect/data-fetching lint pass.

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
