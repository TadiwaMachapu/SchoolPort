# Quick Start Guide - School Portal API

## 5-Minute Setup

### Step 1: Database Setup (2 minutes)

1. Open SQL Server Management Studio (SSMS) or Azure Data Studio
2. Connect to your SQL Server instance
3. Run the database setup script:
   ```sql
   -- Open and execute: DatabaseSetup.sql
   ```
4. Verify the seed data was created successfully

### Step 2: Configure Connection String (1 minute)

1. Open `SchoolPortal.Server/appsettings.json`
2. Update the connection string:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=SchoolPortalDB;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```

### Step 3: Run the API (2 minutes)

```bash
cd "c:\Users\tadiw\OneDrive\Documents\Business\School Portal\SchoolPortal.Server"
dotnet run
```

The API should start at: `https://localhost:7071`

### Step 4: Test with Swagger

1. Open browser: `https://localhost:7071/swagger`
2. Click on **POST /api/auth/login**
3. Click "Try it out"
4. Use these credentials:
   ```json
   {
     "email": "admin@demo.schoolportal.com",
     "password": "Admin@123"
   }
   ```
5. Copy the `accessToken` from the response
6. Click "Authorize" button at top
7. Enter: `Bearer {paste-your-token-here}`
8. Now you can test any endpoint!

## Quick Test Scenarios

### Scenario 1: Create a New Assignment (Teacher)

1. Login as teacher: `teacher@demo.schoolportal.com` / `Admin@123`
2. Call **POST /api/assignments**:
   ```json
   {
     "classSubjectId": 1,
     "title": "Chapter 1 Quiz",
     "description": "Complete all questions",
     "dueAt": "2025-10-15T23:59:59Z",
     "maxMarks": 100
   }
   ```

### Scenario 2: Submit Assignment (Student)

1. Login as student: `student@demo.schoolportal.com` / `Admin@123`
2. Call **POST /api/submissions** with form-data:
   - `assignmentId`: 1
   - `comments`: "My submission"

### Scenario 3: Take Attendance (Teacher)

1. Login as teacher
2. Call **POST /api/attendance/bulk**:
   ```json
   {
     "attendances": [
       {
         "classId": 1,
         "studentId": 1,
         "date": "2025-10-07",
         "status": 1,
         "notes": "Present"
       }
     ]
   }
   ```

## Using Postman

### Import Collection
1. Open Postman
2. File → Import
3. Select `SchoolPortal.postman_collection.json`

### Set Environment
1. Click "Collections" → "School Portal API"
2. Variables tab
3. Set `baseUrl` to `https://localhost:7071`

### Login & Auto-Authentication
1. Run **Auth → Login** request
2. Token is automatically saved to `{{accessToken}}`
3. All other requests will use this token automatically

## Common Issues & Solutions

### Issue: Database connection fails
**Solution**: 
- Check SQL Server is running
- Verify connection string
- Ensure SchoolPortalDB exists

### Issue: 401 Unauthorized
**Solution**:
- Login first to get a token
- Check token is not expired (8-hour validity)
- Ensure "Authorization: Bearer {token}" header is set

### Issue: 403 Forbidden
**Solution**:
- Check your user role has permission
- Admin-only endpoints require Admin role

### Issue: Port already in use
**Solution**:
```bash
# Change port in launchSettings.json or use:
dotnet run --urls "https://localhost:5001"
```

## Next Steps

✅ Test all endpoints in Swagger
✅ Import Postman collection
✅ Create test data (classes, subjects, students)
✅ Review logs in `logs/` directory
✅ Check `/health` endpoint
✅ Read full README.md for detailed documentation

## Default Test Data

| Entity          | Count | Examples                      |
|-----------------|-------|-------------------------------|
| Schools         | 1     | Demo High School              |
| Users (Admin)   | 1     | admin@demo.schoolportal.com   |
| Users (Teacher) | 1     | teacher@demo.schoolportal.com |
| Users (Student) | 1     | student@demo.schoolportal.com |
| Subjects        | 4     | Math, English, Science, History|
| Classes         | 1     | Grade 10A                     |

## API Health Check

Quick verification that everything is working:

```bash
curl https://localhost:7071/health
```

Expected response:
```
Healthy
```

## Development Tips

### Hot Reload
The API supports hot reload. Just save your changes and they'll be applied automatically (most of the time).

### View Logs
```bash
# Windows
type logs\schoolportal-*.txt | more

# Linux/Mac
tail -f logs/schoolportal-*.txt
```

### Run Tests
```bash
cd SchoolPortal.Tests
dotnet test
```

### Database Reset
If you need to reset the database, simply re-run `DatabaseSetup.sql`.

---

**Need Help?** Check the full README.md or review Swagger documentation at `/swagger`
