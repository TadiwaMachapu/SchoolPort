# SchoolPort — Master Index

> A South African high school management platform. Turns school data into educational intelligence.

## The north star
*"Are you building software that digitises school administration, or redesigning how information flows through a school?"* — Henco Janse van Rensburg, Design Teacher

## Current status
- **Main branch:** .NET 10, 302 tests, zero exclusions
- **Active sprint:** Branding sprint (next) — gated on the Sprint 1.6 backlog
- **Last completed:** [[Sprint 1.6 — Design Foundation & Colour Rollout]] (PR #16 + #17, merged 2026-07-22)

## Sprint pipeline
```dataview
TABLE status, completed
FROM "01 - Sprints"
SORT completed DESC
```

## Open follow-ups across the project
```dataview
TASK
FROM "01 - Sprints" OR "04 - Decisions" OR "05 - Architecture"
WHERE !completed
```

## Recent decisions
```dataview
TABLE date, status
FROM "04 - Decisions"
SORT date DESC
LIMIT 10
```

## Key architecture
- [[Three-Layer Security Model]]
- [[Permission Catalogue]]
- [[Cross-Tenant Write Rule]]
- [[Data Model Overview]]

## Research
- [[Henco Interview — Design Teacher]]
- [[Henco Markbook Analysis]]

## External applications to submit
- [ ] PayFast merchant account (payfast.co.za) — needed for Sprint 1.5.4
- [ ] WhatsApp Business API (Meta Business Manager) — needed for Sprint 1.5.5
