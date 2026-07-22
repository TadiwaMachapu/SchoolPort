---
date: 2026-06-10
status: decided
sprint: "[[Sprint 1.5.0 — Security Layer]] Step 6 (FIN-1/2/3)"
---

# Decision: Finance Segregation of Duties Controls

## Context
During the Step 6 permission migration, the live seed was found to violate SoD rules. The FinanceManager was omnipotent — held both `finance.exempt_initiate` and `finance.exempt_approve`, and the Bursar had more access than intended.

## The corrections made

### FIN-1 — FinanceManager lost exempt_approve
FinanceManager can initiate exemptions but NOT approve them. Approval requires Principal or DeputyPrincipal. This prevents a Finance Manager from approving their own exemptions.

### FIN-2 — Bursar corrected to capture-and-chase role
Removed `finance.create_invoice` and `finance.exempt_initiate` from BursarDebtorsClerk. The Bursar records payments that come in and chases debtors — they don't create fee structures or initiate exemptions.

**Final Bursar grants:** `finance.view_all`, `finance.capture_payment`, `finance.reports`

### FIN-3 — Sensitive additions
`finance.view_all` and `finance.reports` marked Sensitive (DB-resolved, never JWT-cached).

## The SoD lines that must always hold
- Cashier: capture + view only ✅
- Bursar: no create invoice, no approve ✅
- FinanceManager: no approve exemptions ✅
- No single position can both create_invoice AND approve_exemption ✅

## How it's enforced
Two regression tests in the security suite:
- `FinanceManager does NOT hold finance.exempt_approve`
- `Bursar does NOT hold finance.create_invoice or finance.exempt_initiate`

The revocation block in PositionsSeedData ensures live DB converges on startup.

## Related
- [[Three-Layer Security Model]]
- [[Sprint 1.5.0 — Security Layer]]
