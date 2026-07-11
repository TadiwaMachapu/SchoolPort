# Sprint 1.5.2 — Matric Hub v1

---
sprint: 1.5.2
status: shipped
merged: 2026-07-10
pr: 11
---

## Goal
Build out the Matric Hub shell into a real Grade 12 support tool. The highest-stakes cohort — November NSC exams, university applications, life decisions.

## Shipped (PR #11, merged to main 2026-07-10)
Single clean commit `f62c8df9` (history rebuilt off main — the working branch was polluted by obsidian-git vault-backup commits). All 4 CI jobs green: backend build + tests (253/253, zero exclusions), frontend build (+13 vitest), migration replay on fresh DB, architecture contract.

### Learner features
- **Past papers library** — 159 seeded: 142 NSC November (2019–2024) + 17 2014 NSC Exemplars. Every URL verified against official DBE pages (128 direct + 14 index fallback); idempotent startup sync heals legacy broken-URL rows and deactivates phantom 2019 Accounting/Business Studies P2 rows. Year/type filters, enrolled-subjects-first ordering, exemplar explainer.
- **AI tutor v2** — `ai.tutor` permission, 20 questions/day per learner (`School.Settings.MatricTutorDailyLimit`), 30-day answer cache (cache hits free, checked before the rate limit), quota refunded on API error. Live-verified Socratic behaviour: explains, warns about exam traps, ends with practice questions.
- **Study planner** — countdown to November exams + weakest-first weekly session goals (`marks.view_own`).
- **NSC subject requirements** — static catalogue endpoint (`platform.access`).

### Teacher/HOD features
- **Grade 12 risk dashboard** (`marks.view_class` + scope) — green/amber/red bands with ±5% term-over-term trend; missing work scoped via LearnerSubjects (FET subject choice).
- **Grade Head matric overview** — cross-subject risk + priority flags, tab gated on GradeHead position.

### AI provider switch: Anthropic → Google Gemini (free tier)
- All four AI services (tutor, gap analysis, smart reports, Gr9 advisor) route through one `IGeminiService` (plain HttpClient, no SDK).
- **Model is config-driven** (`Gemini:Model`, currently `gemini-3.1-flash-lite`): Google churned three model names in a week — 1.5 retired, 2.5-flash blocked for new users (despite appearing in ListModels), 3.5-flash 503-overloaded. Flip config when 3.5-flash stabilises.
- Anthropic monthly cost cap removed from the tutor (free tier — rate limit only); usage rows log at cost 0.
- Fixed latent DateTime Kind=Unspecified → Npgsql timestamptz crash in all four AI services (regression test added).

## System prompt for AI tutor (as shipped, verbatim)
"You are an expert NSC matric tutor for South African Grade 12 learners. Answer in a teaching style — explain concepts, give examples, test understanding. Reference CAPS curriculum where relevant. Never just give the answer — guide the learner to it. End each response with either a follow-up question to check understanding, or a practice suggestion."

## Known follow-ups
- Tutor answers contain markdown/LaTeX but `MatricTutorCard` renders plain text — learners see raw `$$`/`###`; needs markdown + KaTeX rendering.
- Legacy `AiService`/`AiController` (pre-sprint) still point at Anthropic; `Anthropic.SDK` package reference unused — drop both when convenient.
- DevSeed demo-data gaps documented in the sprint spot-check (Term/UserPositions quirks on fresh seed).

## Related
- [[Pathways Feature]]
- [[Sprint 1.5.2.5 — Marks Capture]]
