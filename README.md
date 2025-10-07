# School Portal - Backend API (MVP)

## Overview
A comprehensive school management system backend API built with **ASP.NET Core 8**, featuring JWT authentication, role-based authorization, and soft multi-tenancy.

## Tech Stack
- **Framework**: ASP.NET Core 8 (Minimal Controllers)
- **Database**: SQL Server with EF Core 8 (Database-First)
- **Authentication**: JWT Bearer Tokens
- **Validation**: FluentValidation
- **Logging**: Serilog
- **API Documentation**: Swagger/OpenAPI
- **Testing**: xUnit, Moq

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
- Concurrency control using rowversion
- Health checks with SQL Server database connectivity

### Database
- SQL Server with existing database: `SchoolPortalDB`
- EF Core entities with proper relationships
- Stored procedure support for bulk operations (TVP)
- Database views for reporting

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
- SQL Server (LocalDB or Express)
- Visual Studio 2022 or VS Code

### Database Setup

1. **Create the database** (or use existing SchoolPortalDB)

2. **Run the setup script**:
```sql
sqlcmd -S localhost -d SchoolPortalDB -i DatabaseSetup.sql
```

This creates:
- Table-Valued Parameter type for attendance bulk operations
- Stored procedure: `usp_Attendance_BulkUpsert`
- Views: `vw_AttendanceSummary`, `vw_GradebookSimple`
- Seed data with demo users

3. **Update connection string** in `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=SchoolPortalDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

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
https://localhost:7071/swagger
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
- `POST /api/attendance/bulk` - Bulk upsert attendance (uses TVP stored procedure)

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
- **Bulk operations** using TVP for attendance (single DB round-trip)
- **Efficient queries** with proper includes and projections

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
- [ ] Configure production connection string
- [ ] Set up CORS origins for production domains
- [ ] Enable HTTPS and SSL certificates
- [ ] Configure file storage for submission uploads
- [ ] Set up database backups
- [ ] Configure log retention policies
- [ ] Review and tighten security policies

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

**Built with ❤️ using ASP.NET Core 8**
