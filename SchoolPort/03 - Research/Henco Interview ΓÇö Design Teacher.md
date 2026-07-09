# Henco Janse van Rensburg — Design Teacher Interview

---
date: 2026-07-06
interviewee: Henco Janse van Rensburg
role: Design Teacher (Grades 10-12) + Examiner, Moderator, Curriculum Specialist
school: Randfontein High School
experience: ~20 years
---

## The headline insight
> "Teachers do not need another system that records information. They need a system that helps them act on information."

This is the product's north star. Every feature decision filters through it.

## His framework for school software generations
- **Generation 1 (most platforms):** Records information (SA-SAMS, d6)
- **Generation 2 (what he wants now):** Identifies patterns → action
- **Generation 3 (the dream):** Predicts challenges before they occur

SchoolPort should be Generation 2, designed to enable Generation 3.

## What confirms existing decisions

**Re-typing problem is severe** (Q19)
Marks get typed 3-5 times across systems. One-entry-flows-everywhere is the primary value proposition.

**HOD approval is real** (Q23)
Subject HODs, examination departments, and management all check marks. The approval workflow is confirmed practice, not assumed.

**Audit trail is non-negotiable** (Q on trust)
"Lack of transparency, incorrect calculations or no audit trail showing changes." The audit log is a trust requirement, not just compliance.

**Parents shouldn't see marks immediately** (Q32)
"Not always. Context and teacher feedback should accompany results." Confirms the approval gate — marks should not auto-publish.

**At-risk dashboard is the most-wanted feature** (Q39)
"A dashboard that identifies at-risk learners and outstanding tasks automatically." Named unprompted.

## What's new / unexpected

**Moderation is a formal, structured process**
HODs don't just approve — they compare distributions across teachers. This is a CAPS requirement. The HOD view must show all teachers' marks side by side with distributions.

**CAPS mark structure is more complex than assumed**
SBA and exam weightings differ by grade AND subject. Design Grade 10 has different task structures from Design Grade 12.

**Repeated attendance AND marks** are the #1 dreaded admin tasks (Q5)

**Learner fragmentation is a real gap** (Q7-9)
Getting information about one learner takes "minutes to days" across SA-SAMS, admin, grade heads, and other teachers.

**Intervention tracking, not just reporting** (Q11, Q29)
"It is recorded, but meaningful intervention often happens much later." The at-risk dashboard must push information, not wait for teachers to look.

## His workarounds (the gold — Q4)
- Custom Excel mark sheets (with calculations, weightings, diagnostics, rankings)
- Assessment trackers
- Moderation systems
- Parent communication templates
- Learner progress tracking

"Every spreadsheet a teacher builds identifies a problem that existing software has failed to solve."

## Technology comfort
- Q42: Desktop for detailed work, phone for quick access
- Mobile-first design confirmed

## Key question for feature design
> "What information should a teacher see at the exact moment they need it?"

## Follow-up actions
- [x] Received his Excel markbook — see [[Henco Markbook Analysis]]
- [ ] Share pilot version when available
- [ ] He offered to try it and give honest feedback

## Related
- [[Henco Markbook Analysis]]
- [[Sprint 1.5.2.5 — Marks Capture]]
- [[Smart Reports Feature]]
