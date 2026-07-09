# Three-Layer Security Model

---
status: implemented
sprint: [[Sprint 1.5.0 — Security Layer]]
---

## The three layers

```
Layer 1: Identity
  Who are you? (one per user)
  Staff | Learner | Parent | External | System
  Enforced at: JWT claims, identity boundary checks

Layer 2: Positions  
  What role do you hold? (many per user, scoped)
  SubjectTeacher, HOD, GradeHead, PhaseHead,
  Principal, DeputyPrincipal, FinanceManager,
  BursarDebtorsClerk, Cashier, ITAdministrator,
  LOTeacher, ClassTeacher, SportCultureMIC,
  Auditor, DistrictOfficial, SystemSupport
  Enforced at: [RequirePermission] + IScopeService

Layer 3: Permissions
  What can you do? (atomic verbs)
  ~40 permission keys, deny-by-default
  Enforced at: PermissionAuthorizationHandler
```

## Enforcement chain
```
Request arrives
  → TenantMiddleware (resolves permissions from DB)
  → [RequirePermission] attribute (checks permission)
  → IScopeService (filters data to caller's scope)
  → [CrossTenantGuard] test (CI enforced)
```

## Tiered JWT TTLs
- Staff: 8 hours
- Finance + External: 1 hour (short — finance positions handle real money)
- System: 30 minutes

## Finance SoD controls
- **FinanceManager:** can initiate but NOT approve exemptions
- **BursarDebtorsClerk:** capture + view only (no create invoice, no approve)
- **Cashier:** capture payment only
- **Principal/Deputy:** approve exemptions, oversight reads

## The [CrossTenantGuard] scanner
CI fails if an ID-bearing mutating endpoint has no cross-tenant test. Prevents regression permanently.

## The body-ID rule (critical)
> Any mutating endpoint accepting a tenant-owned ID in the body MUST validate it belongs to the caller's school before any write.

Route-supplied IDs are protected by the load-with-SchoolId pattern. Body-supplied IDs are NOT — they were the source of all 19 vulnerabilities found in Step 10.

## Known limitations (from SECURITY.md)
- Database-layer RLS not in force (pre-scale). App layer is proven with 188 tests.
- Assignment creation scope is class-level not class-subject-level (intra-tenant write-scope gap)
- Class-metadata read is SchoolId-only (low severity)

## Related
- [[Permission Catalogue]]
- [[Cross-Tenant Write Rule]]
- [[Finance SoD Controls]]
