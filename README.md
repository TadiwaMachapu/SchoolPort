# School Portal - Backend API

## Overview
A comprehensive school management system backend API built with **ASP.NET Core 8**, featuring JWT authentication, role-based authorization, and soft multi-tenancy. Now powered by **Supabase Postgres**.

## Tech Stack
- **Framework**: ASP.NET Core 8
- **Database**: Supabase Postgres with EF Core 8 + Npgsql
- **Authentication**: JWT Bearer Tokens
- **Validation**: FluentValidation
- **Logging**: Serilog
- **API Documentation**: Swagger/OpenAPI
- **Testing**: xUnit, Moq, Testcontainers.PostgreSql

## Architecture
Clean-ish layered architecture:
- **Controllers** → Handle HTTP requests/responses
- **Services** → Business logic and validation
- **Data Layer** → EF Core DbContext and entities
- **DTOs** → Request/response models (never expose EF entities)

## Key Features

### Security
- JWT bearer authentication with role-based authorization
- Roles: **Admin**, **Teacher**, **Student**, **Parent**
- Soft multi-tenancy: Every request is scoped by SchoolId (from JWT claim)
- Automatic tenant filtering in all queries

### Cross-Cutting Concerns
- Global exception middleware with ProblemDetails responses
- Request/response logging with Serilog
- Response caching for GET endpoints
- Concurrency control using BIGINT row_version
- Health checks with Postgres database connectivity

### Database
- **Supabase Postgres** cloud database
- EF Core 8 with Npgsql provider
- Snake_case naming conventions for Postgres compatibility
- Bulk operations using `INSERT ... ON CONFLICT DO UPDATE`
- Database views for reporting (`vw_attendance_summary`, `vw_gradebook_simple`)

## Project Structure

```
School Portal/
├── SchoolPortal.Server/          # API project
│   ├── Controllers/              # API controllers
│   ├── Services/                 # Business logic services
│   ├── Middleware/               # Custom middleware
│   ├── Validators/               # FluentValidation validators
│   └── Program.cs                # Application startup
├── SchoolPortal.Data/            # Data access layer
│   ├── Entities/                 # EF Core entities
│   └── SchoolPortalDbContext.cs  # DbContext
├── SchoolPortal.Shared/          # Shared DTOs
│   └── DTOs/                     # Request/response models
├── SchoolPortal.Tests/           # Unit tests
└── DatabaseSetup.sql             # Database setup script
```

## Getting Started

### Prerequisites
- .NET 8 SDK
- Supabase account (free tier works)
- Visual Studio 2022 or VS Code

### Database Setup (Supabase)

1. **Create a Supabase project** at [supabase.com](https://supabase.com)

2. **Get your connection string** from Project Settings → Database → Connection string (URI)

3. **Update connection string** in `appsettings.json` and `appsettings.Development.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=YOUR_PROJECT.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_PASSWORD;SSL Mode=Require"
}
```

4. **Run EF Core migrations**:
```bash
cd SchoolPortal.Server
dotnet ef database update --project ../SchoolPortal.Data
```

5. **Run the setup script** in Supabase SQL Editor (`PostgresSetup.sql`):
   - Creates views: `vw_attendance_summary`, `vw_gradebook_simple`
   - Inserts seed data with demo users

### Running the API

1. **Restore packages**:
```bash
dotnet restore
```

2. **Run the application**:
```bash
cd SchoolPortal.Server
dotnet run
```

3. **Access Swagger UI**:
```
http://localhost:5128/swagger
```

### Default Login Credentials

| Role    | Email                              | Password   |
|---------|-------------------------------------|------------|
| Admin   | admin@demo.schoolportal.com        | Admin@123  |
| Teacher | teacher@demo.schoolportal.com      | Admin@123  |
| Student | student@demo.schoolportal.com      | Admin@123  |

## API Endpoints

### Authentication
- `POST /api/auth/login` - Login and get JWT token
- `POST /api/auth/refresh` - Refresh access token

### Users & Profile
- `GET /api/me` - Get current user profile
- `GET /api/users` - List users (Admin only)
- `POST /api/users` - Create user (Admin only)

### Schools
- `GET /api/schools/current` - Get current school branding

### Classes & Enrollments
- `GET /api/classes` - List classes (with pagination, filtering)
- `GET /api/classes/{id}` - Get class details with subjects
- `POST /api/classes` - Create class (Admin only)
- `POST /api/enrolments/bulk` - Bulk enroll students (Admin only)

### Subjects
- `GET /api/subjects` - List all subjects
- `POST /api/subjects` - Create subject (Admin only)
- `POST /api/class-subjects/bulk` - Assign subjects to classes (Admin/Teacher)

### Assignments
- `GET /api/assignments` - List assignments (filtered by role)
- `GET /api/assignments/{id}` - Get assignment details
- `POST /api/assignments` - Create assignment (Admin/Teacher)
- `PUT /api/assignments/{id}` - Update assignment with concurrency check

### Submissions & Grades
- `POST /api/submissions` - Submit assignment (Student)
- `GET /api/submissions/by-assignment/{id}` - Get all submissions (Admin/Teacher)
- `POST /api/grades` - Grade submission (Admin/Teacher)
- `PATCH /api/grades/bulk` - Bulk grade submissions (Admin/Teacher)

### Attendance
- `GET /api/attendance` - Get attendance records by class and date
- `POST /api/attendance/bulk` - Bulk upsert attendance (uses Postgres ON CONFLICT)

### Announcements
- `GET /api/announcements` - List announcements
- `POST /api/announcements` - Create announcement (Admin/Teacher)

### Reports
- `GET /api/reports/attendance-summary` - Attendance summary report
- `GET /api/reports/gradebook-simple` - Gradebook report

### Health
- `GET /health` - Health check endpoint

## Authentication Flow

1. **Login**: POST to `/api/auth/login` with email and password
2. **Receive JWT**: Response contains `accessToken` and user info
3. **Use token**: Add header to all subsequent requests:
   ```
   Authorization: Bearer {accessToken}
   ```
4. **Token Claims**:
   - `sub` (NameIdentifier): UserId
   - `role`: User role
   - `schoolId`: School identifier for tenant filtering

## Tenant Enforcement

Every authenticated request is automatically filtered by SchoolId:
- Extracted from JWT `schoolId` claim
- Applied in `TenantMiddleware`
- Services use `ICurrentUserService` to access SchoolId
- All queries automatically filter to user's school

## Testing

### Run Unit Tests
```bash
cd SchoolPortal.Tests
dotnet test
```

### Test Coverage
- Assignment service validation and CRUD operations
- Attendance service with bulk operations
- Mock-based testing with in-memory database

### Using Postman

1. **Import collection**: `SchoolPortal.postman_collection.json`
2. **Set base URL**: Update `baseUrl` variable (default: `https://localhost:7071`)
3. **Login**: Run "Auth → Login" request
4. **Auto-token**: Collection automatically saves token to `{{accessToken}}`
5. **Test endpoints**: All authenticated requests use the saved token

## Validation Rules

### Assignments
- Title: Required, max 200 characters
- DueAt: Must be in the future
- MaxMarks: Must be > 0

### Grades
- Score: Must be 0 ≤ Score ≤ MaxMarks (validated against assignment)

### Attendance
- Status: Must be 0 (Absent), 1 (Present), or 2 (Late)

### Announcements
- Audience: Must be "All", "Grade", or "Class"
- AudienceValue: Required when Audience ≠ "All"

## Error Handling

All errors return RFC 7807 ProblemDetails format:
```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "Due date must be in the future",
  "instance": "/api/assignments"
}
```

Common status codes:
- **400**: Bad Request (validation errors)
- **401**: Unauthorized (missing/invalid token)
- **403**: Forbidden (insufficient permissions)
- **404**: Not Found
- **409**: Conflict (concurrency issues)
- **500**: Internal Server Error

## Performance Considerations

- **AsNoTracking** used for all read operations
- **Pagination** on all list endpoints (default pageSize: 20)
- **Response caching** for GET requests
- **Bulk operations** using Postgres `ON CONFLICT` for attendance (single DB round-trip)
- **Efficient queries** with proper includes and projections
- **Connection pooling** via Supabase pooler

## Security Best Practices

✅ JWT tokens expire after 8 hours
✅ Passwords hashed with BCrypt
✅ SQL injection prevention via parameterized queries
✅ CORS configured for specific origins
✅ HTTPS enforced
✅ Sensitive data never logged
✅ Role-based authorization on all endpoints

## Configuration

Key settings in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "JwtSettings": {
    "SecretKey": "YourVeryLongSecretKeyThatIsAtLeast32CharactersLongForHS256Algorithm",
    "Issuer": "SchoolPortalAPI",
    "Audience": "SchoolPortalClient",
    "ExpirationHours": 8
  },
  "CorsOrigins": [
    "http://localhost:3000",
    "http://localhost:4200"
  ]
}
```

## Logging

Serilog configured with:
- Console sink for development
- File sink: `logs/schoolportal-YYYYMMDD.txt` (daily rolling)
- Request logging for all HTTP requests
- Exception logging with stack traces

## Deployment Checklist

- [ ] Update JWT SecretKey in production
- [ ] Configure Supabase production connection string
- [ ] Set up CORS origins for production domains
- [ ] Enable HTTPS and SSL certificates
- [ ] Configure file storage for submission uploads
- [ ] Set up Supabase database backups (automatic with paid plans)
- [ ] Configure log retention policies
- [ ] Review and tighten security policies
- [ ] Run `PostgresSetup.sql` to create views and seed data

## Future Enhancements (Post-MVP)

- Refresh token persistence in database
- File upload to Azure Blob Storage
- Real-time notifications (SignalR)
- Email notifications for assignments/grades
- Advanced reporting with filtering
- Parent portal features
- Mobile app support
- OAuth2 integration (Google, Microsoft)

## Support & Documentation

- **Swagger UI**: Available at `/swagger` in development
- **Health Check**: Monitor at `/health`
- **Logs**: Check `logs/` directory for detailed logs

## License

Proprietary - School Portal System

---

**Built with ❤️ using ASP.NET Core 8 + Supabase Postgres**
