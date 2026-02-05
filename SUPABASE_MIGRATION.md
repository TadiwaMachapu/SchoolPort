# SchoolPortal API - Supabase/Postgres Migration Guide

## Overview

This document describes the migration from SQL Server to Supabase Postgres for the SchoolPortal API.

## Breaking Changes

### 1. Connection String Format
**Before (SQL Server):**
```
Server=localhost;Database=SchoolPortalDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

**After (Supabase Postgres):**
```
Host=YOUR_SUPABASE_HOST;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

### 2. RowVersion Concurrency
- **Before:** `byte[] RowVersion` with SQL Server `rowversion` type
- **After:** `long RowVersion` with BIGINT, manually incremented on updates

### 3. Bulk Attendance Upsert
- **Before:** SQL Server TVP (`AttendanceTableType`) + Stored Procedure (`usp_Attendance_BulkUpsert`)
- **After:** Postgres `INSERT ... ON CONFLICT DO UPDATE` with batched raw SQL

### 4. Table/Column Naming
- All tables now use `snake_case` naming
- `[User]` table renamed to `users`
- All columns use `snake_case` (e.g., `SchoolId` → `school_id`)

### 5. Views
- `vw_AttendanceSummary` → `vw_attendance_summary`
- `vw_GradebookSimple` → `vw_gradebook_simple`
- SQL Server functions replaced with Postgres equivalents

---

## Deployment Steps

### Step 1: Create Supabase Project

1. Go to [supabase.com](https://supabase.com) and create a new project
2. Note your project credentials:
   - **Host:** `db.YOUR_PROJECT_REF.supabase.co`
   - **Port:** `5432`
   - **Database:** `postgres`
   - **Username:** `postgres`
   - **Password:** Your database password

### Step 2: Update Connection String

Update `appsettings.json` (and `appsettings.Production.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.YOUR_PROJECT_REF.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

**For production, use environment variables:**
```bash
ConnectionStrings__DefaultConnection="Host=...;Port=5432;..."
```

### Step 3: Run EF Core Migrations

```bash
# Navigate to the Server project
cd SchoolPortal.Server

# Create initial Postgres migration (removes old SQL Server migrations first)
rm -rf ../SchoolPortal.Data/Migrations/*

# Add new Postgres migration
dotnet ef migrations add InitialPostgres --project ../SchoolPortal.Data

# Apply migration to Supabase
dotnet ef database update --project ../SchoolPortal.Data
```

### Step 4: Create Views and Seed Data

Run the `PostgresSetup.sql` script against your Supabase database:

**Option A: Supabase SQL Editor**
1. Go to your Supabase project dashboard
2. Navigate to SQL Editor
3. Paste contents of `PostgresSetup.sql`
4. Click "Run"

**Option B: psql CLI**
```bash
psql "postgresql://postgres:YOUR_PASSWORD@db.YOUR_PROJECT_REF.supabase.co:5432/postgres" -f PostgresSetup.sql
```

### Step 5: Verify Deployment

```bash
# Run the application
dotnet run --project SchoolPortal.Server

# Test health endpoint
curl http://localhost:5000/health

# Test login with seed data
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.schoolportal.com","password":"Admin@123"}'
```

---

## Configuration Reference

### Required NuGet Packages

**SchoolPortal.Data.csproj:**
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.4" />
```

**SchoolPortal.Server.csproj:**
```xml
<PackageReference Include="AspNetCore.HealthChecks.Npgsql" Version="8.0.0" />
```

### Program.cs Changes

```csharp
// DbContext
builder.Services.AddDbContext<SchoolPortalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgres",
        tags: new[] { "db", "postgres", "supabase" });
```

---

## Multi-Tenancy

**No changes required.** The existing multi-tenancy implementation is preserved:

- JWT claims still contain `schoolId`
- `TenantMiddleware` still extracts and validates `schoolId`
- All queries still filter by `school_id`
- All inserts still auto-set `school_id` from `ICurrentUserService`

**Important:** Supabase Row-Level Security (RLS) is **NOT** enabled. Multi-tenancy is enforced at the application layer.

---

## Concurrency Handling

### How It Works

1. Each entity has a `RowVersion` property (`long`, default `1`)
2. On read, the current `row_version` is loaded
3. On update, the service checks if `row_version` matches
4. If mismatch, return `409 Conflict`
5. On successful update, increment `row_version` by 1

### Example Update Pattern

```csharp
var entity = await _context.Entities.FindAsync(id);
if (entity.RowVersion != dto.RowVersion)
{
    return Conflict("Record has been modified by another user");
}

entity.Name = dto.Name;
entity.RowVersion++; // Increment version
await _context.SaveChangesAsync();
```

---

## Bulk Attendance Upsert

The bulk attendance operation uses Postgres `ON CONFLICT`:

```sql
INSERT INTO attendances (school_id, class_id, student_id, date, status, notes, created_at, row_version)
VALUES ($1, $2, $3, $4, $5, $6, NOW(), 1)
ON CONFLICT (school_id, class_id, student_id, date)
DO UPDATE SET
    status = EXCLUDED.status,
    notes = EXCLUDED.notes,
    updated_at = NOW(),
    row_version = attendances.row_version + 1;
```

**Performance:** Batched in groups of 100 records for optimal performance with 500+ records.

---

## Indexes

The following indexes are automatically created by EF Core migrations:

| Table | Index | Columns |
|-------|-------|---------|
| `attendances` | Unique | `school_id, class_id, student_id, date` |
| All tables | Index | `school_id` |
| All tables | Index | Foreign key columns |

---

## Troubleshooting

### Connection Issues

```
Npgsql.NpgsqlException: Failed to connect
```

**Solutions:**
1. Verify Supabase project is active
2. Check connection string format
3. Ensure SSL Mode is set to `Require`
4. Verify IP is not blocked (Supabase allows all by default)

### Migration Errors

```
relation "schools" already exists
```

**Solution:** Drop existing tables or use a fresh database:
```sql
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
```

### Concurrency Conflicts

```
409 Conflict: Record has been modified
```

**Solution:** This is expected behavior. Refresh the data and retry the operation.

---

## Removed SQL Server Artifacts

The following SQL Server-specific items have been removed:

- ❌ `dbo.AttendanceTableType` (TVP)
- ❌ `dbo.usp_Attendance_BulkUpsert` (Stored Procedure)
- ❌ `vw_AttendanceSummary` (SQL Server view)
- ❌ `vw_GradebookSimple` (SQL Server view)
- ❌ `GETUTCDATE()` → `NOW()`
- ❌ `YEAR()`, `MONTH()` → `EXTRACT()`
- ❌ String `+` concatenation → `||`
- ❌ `rowversion` → `BIGINT row_version`

---

## Testing

### Unit Tests
Unit tests continue to use `InMemoryDatabase` and require no changes.

### Integration Tests
Integration tests use `InMemoryDatabase` by default. For full Postgres testing, use Testcontainers:

```csharp
// Add to test project
<PackageReference Include="Testcontainers.PostgreSql" Version="3.8.0" />
```

---

## Support

For issues related to this migration, check:
1. Supabase documentation: https://supabase.com/docs
2. Npgsql documentation: https://www.npgsql.org/doc/
3. EF Core Postgres provider: https://www.npgsql.org/efcore/
