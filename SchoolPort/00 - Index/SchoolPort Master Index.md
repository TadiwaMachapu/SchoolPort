# SchoolPort — Master Index

> A South African high school management platform. Turns school data into educational intelligence.

## The north star
*"Are you building software that digitises school administration, or redesigning how information flows through a school?"* — Henco Janse van Rensburg, Design Teacher

## Current status
- **Main branch:** .NET 10, 302 tests, zero exclusions
- **Active sprint:** Branding sprint (next) — gated on the Sprint 1.6 backlog
- **Last completed:** [[Sprint 1.6 — Design Foundation & Colour Rollout]] (PR #16 + #17, merged 2026-07-22)

## Sprint pipeline
| Sprint | What | Status |
|---|---|---|
| [[Sprint 1.5.0 — Security Layer]] | Identity, Positions, Permissions | ✅ Complete |
| [[Sprint 1.5.0.5 — Performance]] | Indexes, Materialized Views | ✅ Complete |
| [[Sprint 1.5.0.6 — Private Submissions]] | POPIA fix, signed URLs | ✅ Complete |
| [[Sprint 1.5.1 — Pathways Gaps]] | APS weighting, year scope, seed | ✅ Complete |
| [[Sprint 1.5.2 — Matric Hub]] | Past papers, AI tutor | ✅ Complete |
| [[Sprint 1.5.2.5 — Marks Capture]] | The flagship feature | ✅ Complete |
| [[Sprint 1.5.3 — Smart Reports]] | At-risk dashboard | ✅ Complete |
| [[Sprint 1.6 — Design Foundation & Colour Rollout]] | Token design system + branding prereq | ✅ Complete |
| [[Sprint 1.5.4 — SchoolPay]] | PayFast integration | 📋 Planned |
| [[Sprint 1.5.5 — WhatsApp]] | Parent notifications | 📋 Planned |

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
