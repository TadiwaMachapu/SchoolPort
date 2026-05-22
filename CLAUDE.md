# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout

```
SchoolPort/
├── SchoolPortal.Server/     ASP.NET Core 8 Web API
├── SchoolPortal.Data/       EF Core entities, DbContext, migrations
├── SchoolPortal.Shared/     DTOs shared between server and (Blazor) client
├── SchoolPortal.Client/     Blazor WebAssembly (legacy, largely unused)
├── SchoolPortal.Tests/      xUnit tests (unit + integration)
├── schoolportal-web/        Next.js 16 + Tailwind primary frontend
└── PostgresSetup.sql        Supabase Postgres schema + seed data
```

## Backend Commands (run from repo root)

```bash
# Build entire solution
dotnet build SchoolPortal.sln

# Run the API (listens on http://localhost:5128)
cd SchoolPortal.Server && dotnet run

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~AssignmentServiceTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~AssignmentServiceTests.CreateAssignment_ShouldReturnDto"

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project SchoolPortal.Data --startup-project SchoolPortal.Server

# Apply migrations
dotnet ef database update --project SchoolPortal.Data --startup-project SchoolPortal.Server
```

## Frontend Commands (run from `schoolportal-web/`)

```bash
yarn dev          # dev server on http://localhost:3000
yarn build        # production build
yarn lint         # ESLint
```

## Secrets / Configuration

The real Supabase password, JWT secret, Anthropic API key, Stripe keys, and SSO credentials are **never** in `appsettings.json` — they carry `CHANGE_ME_USE_USER_SECRETS` placeholders. Supply them via:

```bash
cd SchoolPortal.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<real-connection-string>"
dotnet user-secrets set "Anthropic:ApiKey" "<key>"
# etc.
```

Or set the env var `CONNECTIONSTRINGS__DEFAULTCONNECTION` at runtime.

## Backend Architecture

### Multi-tenancy
Every authenticated request carries a `schoolId` JWT claim. `TenantMiddleware` (runs after auth, before authorization) reads this claim and stores it in `HttpContext.Items["SchoolId"]`. All services use `ICurrentUserService` to get `SchoolId`, `UserId`, and `Role` from the current context — never pass tenant IDs through request parameters.

### Service pattern
Controllers are thin. Business logic lives in services injected by interface. All services in `SchoolPortal.Server/Services/` — the bulk of them are co-located in `AdditionalServices.cs` (Class, School, Subject, Grade, Submission, Announcement, etc.). Larger services have their own files (Auth, User, Assignment, Attendance, Course, Quiz, AI, Storage, Notification).

### Authorization model
- `[Authorize]` — any authenticated user
- `[Authorize(Roles = "Admin")]` — school admin only
- `[Authorize(Roles = "Admin,Teacher")]` — admin or teacher
- `[Authorize(Roles = "Student")]` — student only
- `[Authorize(Roles = "Parent")]` — parent only

The JWT token contains `schoolId`, `email`, `role` claims. `schoolId` is a `Guid`.

### Database
Supabase Postgres. EF Core 8 with Npgsql. All table and column names use `snake_case` (applied globally in `SchoolPortalDbContext.OnModelCreating`). Two EF migrations exist: `InitialCreate` and `Phase2Features`. Two Postgres views exist: `attendance_summary_view` and `gradebook_simple_view` (mapped as keyless entities).

Two key entities with important relationships:
- `Student.ParentUserId` → links a parent `User` to their child `Student`
- `ClassSubject` is the join between `Class` and `Subject`, and carries a `TeacherId`

### SignalR
`NotificationHub` at `/hubs/notifications`. On connect, users are added to three groups: `school:{schoolId}`, `user:{userId}`, and `school:{schoolId}:role:{Role}`. JWT is extracted from the `access_token` query parameter (required for WebSocket upgrades — configured in `Program.cs` via `OnMessageReceived`).

## Frontend Architecture (schoolportal-web)

### Routing & Auth
`proxy.ts` (Next.js 16's equivalent of `middleware.ts`) handles:
1. Redirect unauthenticated users to `/login`
2. Redirect authenticated users away from `/login` to `/dashboard`
3. Inject the `x-pathname` header so the server-layout breadcrumb can resolve the page title

JWT is stored as `sp_token` cookie (8hr). Role is stored as `sp_role` cookie. User ID as `sp_userid`. These are set on login and read client-side via `getClientRole()` / `getClientUserId()` helpers in `lib/utils.ts` — avoids extra `/api/me` calls for role-gated UI.

### Layout
`app/(dashboard)/layout.tsx` is a **server component** that fetches `/api/me` and `/api/schools/current` on every navigation. It renders the `<Sidebar>` and a sticky header with breadcrumb + notification bell. If the API is unreachable it shows an error screen instead of crashing.

### API layer
All API calls go through `lib/api.ts`, a typed fetch wrapper. The `request<T>()` function reads `sp_token` from cookies and sets the `Authorization: Bearer` header automatically. The base URL is `NEXT_PUBLIC_API_URL` (defaults to `http://localhost:5128`).

### Role-gated UI
Use `getClientRole()` from `lib/utils.ts` to hide Admin-only controls on the client. Pattern used in `classes/page.tsx`, `assignments/page.tsx`, `courses/page.tsx`, etc.:
```ts
const isAdmin = role === "Admin";
// then conditionally render buttons, table columns, modals
```

### UI component conventions
- All icons are **lucide-react** — no emoji in UI content
- `components/ui/stat-card.tsx` — KPI metric cards used on Dashboard and Analytics
- `components/ui/skeleton.tsx` — `SkeletonTable`, `SkeletonCards`, `SkeletonKPIs` for loading states
- Empty states: `<SomeIcon className="h-10 w-10 text-gray-300" />` + title + description
- Modals: `fixed inset-0 z-50 bg-black/40 backdrop-blur-sm` overlay with a `rounded-2xl bg-white shadow-2xl` panel
- Page titles: `text-2xl font-semibold text-gray-900 tracking-tight`
- Table headers: `text-xs font-semibold text-gray-500 uppercase tracking-wider`

## Key Constraints

- **Never** restore the `middleware.ts` file — Next.js 16 uses `proxy.ts` instead. Both files existing simultaneously crashes the dev server.
- The `School.Theme` and `School.Features` columns are `jsonb` in Postgres. EF Core deserializes them via `.EnableDynamicJson()` on the Npgsql data source.
- Attendance bulk upsert uses `ON CONFLICT DO UPDATE` (Postgres) — the original SQL Server TVP approach was removed during the Supabase migration.
- `RowVersion` is `long` (not `byte[]`) throughout — Postgres doesn't support SQL Server's `rowversion`.
