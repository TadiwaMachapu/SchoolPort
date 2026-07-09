# Sprint 1.5.2 — Matric Hub v1

---
sprint: 1.5.2
status: planned
---

## Goal
Build out the Matric Hub shell into a real Grade 12 support tool. The highest-stakes cohort — November NSC exams, university applications, life decisions.

## Learner features
- Past papers library (subject + year + paper type: Paper 1/2, Prelim, November)
- AI tutor — Anthropic API, subject-specific Q&A in exam style. Rate-limited (default 20 questions/day per learner)
- Study planner — countdown to November exams, per-subject suggested weekly goals based on current performance
- NSC subject requirements — what counts toward the certificate per subject

## Teacher/HOD features
- Grade 12 risk dashboard (marks.view_class) — learners at risk per subject, traffic light
- Grade Head matric overview — all Grade 12 learners, cross-subject risk, priority flags

## System prompt for AI tutor
"You are an expert NSC matric tutor for South African Grade 12 learners. Answer in a teaching style — explain concepts, give examples, test understanding. Reference CAPS curriculum where relevant. Never just give the answer — guide the learner to it."

## Gates
- Starts after Sprint 1.5.1 is complete
- No gates on external approvals

## Related
- [[Pathways Feature]]
- [[Sprint 1.5.2.5 — Marks Capture]]
