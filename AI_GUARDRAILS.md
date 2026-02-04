# AI Guardrails - Non-Negotiable Rules

This document defines **strict, enforceable rules** that all AI-assisted changes must follow in this project. These are not suggestions—they are requirements.

---

## 1. Architecture Rules

### 1.1 Frontend-Backend Separation
- **RULE:** The frontend (`SchoolPortal.Client`) must **NEVER** access the database directly.
- **RULE:** The backend API (`SchoolPortal.Server`) is the **ONLY** data gateway.
- **RULE:** All data access must go through the API layer using `HttpClient` and service interfaces.

### 1.2 API Contract Stability
- **RULE:** API endpoints, DTOs, and contracts must remain **stable** unless explicitly approved.
- **RULE:** Breaking changes to existing endpoints require explicit approval and migration plan.
- **RULE:** New functionality must be added via new endpoints, not by modifying existing ones.

### 1.3 Shared Contracts
- **RULE:** DTOs in `SchoolPortal.Shared` define the contract between frontend and backend.
- **RULE:** Changes to DTOs must be backward-compatible or coordinated across both layers.

---

## 2. Security & Multi-Tenancy Rules

### 2.1 Tenant Isolation (Critical)
- **RULE:** `SchoolId` is the **tenant boundary** and must be enforced in **every** database query.
- **RULE:** All queries must filter by `SchoolId` to prevent cross-tenant data leaks.
- **RULE:** `SchoolId` must be derived from the authenticated user context, **never** from client input.

### 2.2 Authorization
- **RULE:** Authorization is enforced at the **API layer** using `[Authorize]` attributes.
- **RULE:** Role checks (`Admin`, `Teacher`, `Student`, `Parent`) must be explicit and enforced server-side.
- **RULE:** The frontend may hide UI elements based on roles, but **never** rely on client-side authorization alone.

### 2.3 Authentication & Secrets
- **RULE:** No credentials, API keys, connection strings, or secrets may appear in client code.
- **RULE:** JWTs and tokens must be validated server-side.
- **RULE:** Sensitive operations require server-side validation regardless of client state.

### 2.4 User Context
- **RULE:** `ICurrentUserService` provides `UserId` and `SchoolId` from the authenticated JWT.
- **RULE:** Never trust user identity claims from the client—always use server-side context.

---

## 3. Database & Migration Rules

### 3.1 Migration Discipline
- **RULE:** Database changes must **preserve existing behavior** unless explicitly changing functionality.
- **RULE:** No breaking schema changes without explicit approval and rollback plan.
- **RULE:** Migrations must be reversible where possible.

### 3.2 Schema Changes
- **RULE:** Adding columns is safe; removing or renaming columns requires coordination.
- **RULE:** Foreign key relationships must be preserved or explicitly migrated.
- **RULE:** Unique constraints and indexes must be maintained.

### 3.3 Data Integrity
- **RULE:** All entities must include `SchoolId` for tenant isolation.
- **RULE:** Audit fields (`CreatedAt`, `UpdatedAt`, `CreatedByUserId`) must be maintained.
- **RULE:** Row versioning (`RowVersion`) must be preserved for optimistic concurrency.

---

## 4. AI Behavior Rules

### 4.1 Scope Discipline
- **RULE:** Make **only** the requested changes. Do not add unrequested features.
- **RULE:** Do **not** refactor, redesign, or "improve" code unless explicitly instructed.
- **RULE:** Do **not** change coding style, formatting, or conventions unless requested.

### 4.2 Clarity First
- **RULE:** If requirements are unclear, ambiguous, or conflicting, **STOP** and ask for clarification.
- **RULE:** Do not make assumptions about intended behavior—confirm with the user.
- **RULE:** If a change could have security or data integrity implications, flag it before proceeding.

### 4.3 Minimal Changes
- **RULE:** Prefer the smallest change that achieves the goal.
- **RULE:** Do not introduce new dependencies unless necessary and approved.
- **RULE:** Do not delete code that may be in use elsewhere without verification.

### 4.4 Testing & Verification
- **RULE:** Changes must be testable and verifiable.
- **RULE:** Breaking changes must be identified and communicated.
- **RULE:** If a change affects multiple components, document the impact.

---

## 5. Change Discipline

### 5.1 Incremental Changes
- **RULE:** Make small, incremental changes that can be reviewed individually.
- **RULE:** Large changes must be broken into logical, reviewable steps.
- **RULE:** Each change must have a clear purpose and explanation.

### 5.2 Explainability
- **RULE:** Every change must be explainable in plain language.
- **RULE:** Complex logic must include comments explaining the "why," not just the "what."
- **RULE:** If you cannot explain why a change is necessary, do not make it.

### 5.3 Reversibility
- **RULE:** Prefer changes that can be easily reversed or rolled back.
- **RULE:** Avoid changes that create irreversible side effects.
- **RULE:** Document any changes that cannot be easily undone.

---

## 6. Code Quality Rules

### 6.1 Consistency
- **RULE:** Follow existing code patterns and conventions in the project.
- **RULE:** Match the style of surrounding code.
- **RULE:** Do not introduce new patterns without discussion.

### 6.2 Error Handling
- **RULE:** All API endpoints must handle errors gracefully.
- **RULE:** User-facing errors must be informative but not expose internal details.
- **RULE:** Log errors with sufficient context for debugging.

### 6.3 Performance
- **RULE:** Do not introduce N+1 query problems.
- **RULE:** Use `.AsNoTracking()` for read-only queries.
- **RULE:** Paginate large result sets.

---

## 7. Mock Mode Rules (Development)

### 7.1 Mock Service Behavior
- **RULE:** Mock services must return data that matches the shape of real DTOs.
- **RULE:** Mock data should be realistic enough for UI development.
- **RULE:** Mock services must never persist data or have side effects.

### 7.2 Configuration
- **RULE:** Mock mode is controlled by `UseMockApi` configuration flag.
- **RULE:** Mock mode must never be enabled in production.
- **RULE:** Switching between mock and real services must not require code changes.

---

## 8. When in Doubt

If you are uncertain about:
- Whether a change violates tenant isolation
- Whether a change breaks existing functionality
- Whether a change has security implications
- Whether a change is within scope

**STOP and ask the user before proceeding.**

---

## Enforcement

These rules are **non-negotiable**. Any AI-assisted change that violates these guardrails must be:
1. Flagged immediately
2. Explained to the user
3. Corrected or reverted

The purpose of these guardrails is to maintain:
- **Security** - Protect user data and prevent unauthorized access
- **Stability** - Ensure the system remains functional and predictable
- **Maintainability** - Keep the codebase understandable and manageable
- **Trust** - Build confidence in AI-assisted development

---

*Last Updated: 2026-02-04*
