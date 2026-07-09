# Decision: Cross-Tenant Write Rule

---
date: 2026-06-10
status: decided
sprint: [[Sprint 1.5.0 — Security Layer]] Step 10
---

## Context
During Step 10 security testing, 19 vulnerabilities were found — all with the same root cause. A systematic rule was needed to prevent recurrence.

## The pattern that caused 19 vulnerabilities
Route-supplied IDs are protected automatically by the `load-with-SchoolId` pattern (e.g. `WHERE id = @id AND school_id = @schoolId` → 404 if not found).

Body-supplied IDs are NOT protected — the FK resolves across tenants, and the write succeeds silently.

## The rule (binding for all future endpoints)
> Any mutating endpoint (POST/PUT/PATCH/DELETE) that accepts a tenant-owned ID in the **request body** MUST validate that ID belongs to the caller's school before any write or file upload.

**Violation → 404** (resource not found in your school)

## Examples of body IDs that caused gaps
- `ClassSubjects POST /bulk` — classId, subjectId, teacherId in body
- `Fees POST /{id}/payments` — studentId in body (money crossing tenants)
- `Messages POST /threads/class/{classId}` — classId in body (pulled foreign class's learners into a thread)
- `Quizzes POST /attempts/{id}/submit` — attemptId (cross-user, not just cross-tenant)

## The enforcement mechanism
The `[CrossTenantGuard]` scanner (CI job) enumerates every ID-bearing mutating endpoint and fails the build if no guard test exists. New endpoints cannot ship without proving the guard works.

## Consequences
- Every new endpoint with body IDs needs validation code + a cross-tenant test
- The scanner makes this permanent — it's not a policy, it's a CI gate

## Related
- [[Three-Layer Security Model]]
- [[Sprint 1.5.0 — Security Layer]]
