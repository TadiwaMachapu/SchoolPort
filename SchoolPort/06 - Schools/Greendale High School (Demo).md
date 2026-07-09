# Greendale High School (Demo)

---
type: demo school
school_id: 4fb7c46d-...-2239
---

## Users
| Email | Role | Position | Notes |
|---|---|---|---|
| admin@greendale.edu | Staff | Principal | |
| james.dlamini@greendale.edu | Staff | SubjectTeacher | Grade 12A |
| priya.naidoo@greendale.edu | Staff | SubjectTeacher | Grade 9B |
| lethabo.sithole@greendale.edu | Learner | — | Grade 12, STU2026-1001 |
| bongani.mokoena@parent.edu | Parent | — | |
| nomsa.sithole@parent.edu | Parent | — | |

## Feature flags
All ON except VirtualClassroom (turned off — Phase 3 feature not built yet)

## Demo data
- 8 assignments (all currently overdue in demo data)
- Lethabo has: R5,500 total fees, R5,000 paid, R500 outstanding (Activity Levy)
- Subjects: Accounting, English Home Language, Life Sciences, Mathematics, Physical Sciences

## Credentials
Passwords are in the database / Supabase Auth. Use `dotnet user-secrets` or Supabase dashboard to check/reset.

## Notes
- Used for all spot-checks throughout Sprint 1.5.0
- VirtualClassroom feature disabled 2026-06-15 (Phase 3 decision)
- No Finance Manager user exists yet — need to assign one for SchoolPay spot-checks

## Related
- [[Sprint 1.5.0 — Security Layer]]
