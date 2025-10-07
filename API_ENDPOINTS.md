# School Portal API - Complete Endpoint Reference

## Base URL
- **Development**: `https://localhost:7071`
- **Production**: `https://your-domain.com`

## Authentication
All endpoints except `/api/auth/*` and `/health` require Bearer token authentication.

**Header Format**:
```
Authorization: Bearer {your-jwt-token}
```

---

## 📋 Auth Endpoints

### Login
**POST** `/api/auth/login`

**Request Body**:
```json
{
  "email": "admin@demo.schoolportal.com",
  "password": "Admin@123"
}
```

**Response**: `200 OK`
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl...",
  "expiresAt": "2025-10-07T21:45:03Z",
  "role": "Admin",
  "userId": 1,
  "schoolId": 1
}
```

### Refresh Token
**POST** `/api/auth/refresh`

**Request Body**:
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl..."
}
```

---

## 👤 User Endpoints

### Get Current User Profile
**GET** `/api/me`

**Response**: `200 OK`
```json
{
  "userId": 1,
  "email": "admin@demo.schoolportal.com",
  "firstName": "System",
  "lastName": "Administrator",
  "role": "Admin",
  "schoolId": 1,
  "schoolName": "Demo High School",
  "schoolLogoUrl": null,
  "schoolPrimaryColor": "#1E40AF"
}
```

### List Users (Admin Only)
**GET** `/api/users?role={role}&q={search}&page={page}&pageSize={pageSize}`

**Query Parameters**:
- `role` (optional): Filter by role (Admin, Teacher, Student, Parent)
- `q` (optional): Search by name or email
- `page` (default: 1): Page number
- `pageSize` (default: 20): Items per page

**Response**: `200 OK`
```json
{
  "items": [
    {
      "userId": 1,
      "schoolId": 1,
      "email": "admin@demo.schoolportal.com",
      "firstName": "System",
      "lastName": "Administrator",
      "role": "Admin",
      "isActive": true,
      "createdAt": "2025-10-07T10:00:00Z",
      "lastLoginAt": "2025-10-07T13:45:00Z"
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Create User (Admin Only)
**POST** `/api/users`

**Request Body**:
```json
{
  "email": "newteacher@demo.schoolportal.com",
  "password": "SecurePass@123",
  "firstName": "John",
  "lastName": "Doe",
  "role": "Teacher"
}
```

**Response**: `201 Created`

---

## 🏫 School Endpoints

### Get Current School
**GET** `/api/schools/current`

**Response**: `200 OK`
```json
{
  "schoolId": 1,
  "name": "Demo High School",
  "domain": "demo.schoolportal.com",
  "brandingLogoUrl": null,
  "brandingPrimaryColor": "#1E40AF",
  "isActive": true
}
```

---

## 📚 Class Endpoints

### List Classes
**GET** `/api/classes?year={year}&q={search}&page={page}&pageSize={pageSize}`

**Query Parameters**:
- `year` (optional): Filter by academic year
- `q` (optional): Search by class name
- `page` (default: 1)
- `pageSize` (default: 20)

**Response**: `200 OK`
```json
{
  "items": [
    {
      "classId": 1,
      "name": "Grade 10A",
      "gradeLevel": 10,
      "academicYear": 2024,
      "teacherId": 1,
      "teacherName": "John Teacher",
      "maxCapacity": 30,
      "enrollmentCount": 25,
      "subjects": null
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Get Class by ID
**GET** `/api/classes/{id}`

**Response**: `200 OK`
```json
{
  "classId": 1,
  "name": "Grade 10A",
  "gradeLevel": 10,
  "academicYear": 2024,
  "teacherId": 1,
  "teacherName": "John Teacher",
  "maxCapacity": 30,
  "enrollmentCount": 25,
  "subjects": [
    {
      "subjectId": 1,
      "name": "Mathematics",
      "teacherName": "John Teacher"
    }
  ]
}
```

### Create Class (Admin Only)
**POST** `/api/classes`

**Request Body**:
```json
{
  "name": "Grade 11B",
  "gradeLevel": 11,
  "academicYear": 2024,
  "teacherId": 1,
  "maxCapacity": 30
}
```

**Response**: `201 Created`

---

## 📝 Enrollment Endpoints

### Bulk Enroll Students (Admin Only)
**POST** `/api/enrolments/bulk`

**Request Body**:
```json
{
  "enrollments": [
    {
      "classId": 1,
      "studentId": 1
    },
    {
      "classId": 1,
      "studentId": 2
    }
  ]
}
```

**Response**: `204 No Content`

---

## 📖 Subject Endpoints

### List Subjects
**GET** `/api/subjects`

**Response**: `200 OK`
```json
[
  {
    "subjectId": 1,
    "name": "Mathematics",
    "code": "MATH",
    "description": null
  }
]
```

### Create Subject (Admin Only)
**POST** `/api/subjects`

**Request Body**:
```json
{
  "name": "Physics",
  "code": "PHYS",
  "description": "Advanced Physics"
}
```

**Response**: `201 Created`

---

## 🔗 Class Subject Endpoints

### Bulk Assign Subjects to Classes (Admin/Teacher)
**POST** `/api/class-subjects/bulk`

**Request Body**:
```json
{
  "classSubjects": [
    {
      "classId": 1,
      "subjectId": 1,
      "teacherId": 1
    }
  ]
}
```

**Response**: `204 No Content`

---

## 📋 Assignment Endpoints

### List Assignments
**GET** `/api/assignments?classId={classId}&dueFrom={date}&dueTo={date}&status={status}&page={page}&pageSize={pageSize}`

**Query Parameters**:
- `classId` (optional): Filter by class
- `dueFrom` (optional): Minimum due date (ISO 8601)
- `dueTo` (optional): Maximum due date (ISO 8601)
- `status` (optional): Assignment status
- `page` (default: 1)
- `pageSize` (default: 20)

**Response**: `200 OK`
```json
{
  "items": [
    {
      "assignmentId": 1,
      "title": "Chapter 1 Quiz",
      "description": "Complete all questions",
      "dueAt": "2025-10-15T23:59:59Z",
      "maxMarks": 100,
      "createdAt": "2025-10-07T10:00:00Z",
      "className": "Grade 10A",
      "subjectName": "Mathematics",
      "createdByName": "John Teacher",
      "rowVersion": "AAAAAAAAAAA="
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Get Assignment by ID
**GET** `/api/assignments/{id}`

**Response**: `200 OK`

### Create Assignment (Admin/Teacher)
**POST** `/api/assignments`

**Request Body**:
```json
{
  "classSubjectId": 1,
  "title": "Chapter 2 Homework",
  "description": "Complete exercises 1-20",
  "dueAt": "2025-10-20T23:59:59Z",
  "maxMarks": 100
}
```

**Response**: `201 Created`

**Validation Rules**:
- Title: Required, max 200 chars
- DueAt: Must be in future
- MaxMarks: Must be > 0

### Update Assignment (Admin/Teacher)
**PUT** `/api/assignments/{id}`

**Request Body**:
```json
{
  "title": "Updated Title",
  "description": "Updated description",
  "dueAt": "2025-10-25T23:59:59Z",
  "maxMarks": 120,
  "rowVersion": "AAAAAAAAAAA="
}
```

**Response**: `200 OK`

**Note**: Include rowVersion for concurrency control. Returns `409 Conflict` if version mismatch.

---

## 📤 Submission Endpoints

### Submit Assignment (Student)
**POST** `/api/submissions`

**Request Body** (multipart/form-data):
- `assignmentId`: integer
- `comments`: string (optional)
- `file`: file (optional, for MVP)

**Response**: `201 Created`
```json
{
  "submissionId": 1
}
```

### Get Submissions by Assignment (Admin/Teacher)
**GET** `/api/submissions/by-assignment/{assignmentId}`

**Response**: `200 OK`
```json
[
  {
    "submissionId": 1,
    "assignmentId": 1,
    "studentId": 1,
    "studentName": "Jane Student",
    "studentNumber": "S2024001",
    "submittedAt": "2025-10-08T10:30:00Z",
    "fileUrl": null,
    "fileName": null,
    "comments": "My submission",
    "grade": {
      "gradeId": 1,
      "score": 85.5,
      "feedback": "Good work!",
      "gradedAt": "2025-10-09T14:00:00Z"
    }
  }
]
```

---

## 📊 Grade Endpoints

### Create Grade (Admin/Teacher)
**POST** `/api/grades`

**Request Body**:
```json
{
  "submissionId": 1,
  "score": 85.5,
  "feedback": "Excellent work! Keep it up."
}
```

**Response**: `201 Created`

**Validation Rules**:
- Score: 0 ≤ Score ≤ Assignment.MaxMarks

### Bulk Grade Submissions (Admin/Teacher)
**PATCH** `/api/grades/bulk`

**Request Body**:
```json
{
  "grades": [
    {
      "submissionId": 1,
      "score": 90,
      "feedback": "Excellent"
    },
    {
      "submissionId": 2,
      "score": 75,
      "feedback": "Good"
    }
  ]
}
```

**Response**: `204 No Content`

---

## 📅 Attendance Endpoints

### Get Attendance
**GET** `/api/attendance?classId={classId}&date={date}`

**Query Parameters**:
- `classId` (required): Class ID
- `date` (required): Date in YYYY-MM-DD format

**Response**: `200 OK`
```json
[
  {
    "attendanceId": 1,
    "classId": 1,
    "studentId": 1,
    "studentName": "Jane Student",
    "studentNumber": "S2024001",
    "date": "2025-10-07",
    "status": 1,
    "notes": "Present"
  }
]
```

**Status Values**:
- `0`: Absent
- `1`: Present
- `2`: Late

### Bulk Upsert Attendance (Admin/Teacher)
**POST** `/api/attendance/bulk`

**Request Body**:
```json
{
  "attendances": [
    {
      "classId": 1,
      "studentId": 1,
      "date": "2025-10-07",
      "status": 1,
      "notes": "Present"
    },
    {
      "classId": 1,
      "studentId": 2,
      "date": "2025-10-07",
      "status": 0,
      "notes": "Absent - sick"
    }
  ]
}
```

**Response**: `204 No Content`

**Note**: Uses SQL Server TVP for efficient bulk operations. Idempotent on (ClassId, StudentId, Date).

---

## 📢 Announcement Endpoints

### List Announcements
**GET** `/api/announcements?since={date}&page={page}&pageSize={pageSize}`

**Query Parameters**:
- `since` (optional): Only announcements created after this date
- `page` (default: 1)
- `pageSize` (default: 20)

**Response**: `200 OK`
```json
{
  "items": [
    {
      "announcementId": 1,
      "title": "Important Notice",
      "content": "School will be closed tomorrow",
      "audience": "All",
      "audienceValue": null,
      "createdByName": "John Teacher",
      "createdAt": "2025-10-07T10:00:00Z",
      "expiresAt": "2025-10-10T00:00:00Z",
      "isActive": true
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Create Announcement (Admin/Teacher)
**POST** `/api/announcements`

**Request Body**:
```json
{
  "title": "School Event",
  "content": "Annual sports day on Friday",
  "audience": "Grade",
  "audienceValue": "10",
  "expiresAt": "2025-10-15T00:00:00Z"
}
```

**Response**: `201 Created`

**Audience Options**:
- `All`: All users (audienceValue not required)
- `Grade`: Specific grade level (audienceValue = grade number)
- `Class`: Specific class (audienceValue = class ID)

---

## 📈 Report Endpoints

### Attendance Summary Report (Admin/Teacher)
**GET** `/api/reports/attendance-summary?classId={classId}&year={year}`

**Query Parameters**:
- `classId` (optional): Filter by class
- `year` (optional): Filter by year

**Response**: `200 OK`
```json
[
  {
    "schoolId": 1,
    "classId": 1,
    "className": "Grade 10A",
    "studentId": 1,
    "studentName": "Jane Student",
    "studentNumber": "S2024001",
    "year": 2025,
    "month": 10,
    "presentCount": 18,
    "absentCount": 2,
    "lateCount": 1,
    "totalDays": 21
  }
]
```

### Gradebook Simple Report (Admin/Teacher)
**GET** `/api/reports/gradebook-simple?classId={classId}`

**Query Parameters**:
- `classId` (optional): Filter by class

**Response**: `200 OK`
```json
[
  {
    "schoolId": 1,
    "classId": 1,
    "className": "Grade 10A",
    "subjectName": "Mathematics",
    "assignmentId": 1,
    "assignmentTitle": "Chapter 1 Quiz",
    "maxMarks": 100,
    "studentId": 1,
    "studentName": "Jane Student",
    "studentNumber": "S2024001",
    "score": 85.5,
    "feedback": "Good work!",
    "gradedAt": "2025-10-09T14:00:00Z",
    "percentage": 85.50
  }
]
```

---

## 🏥 Health Check

### Health Check
**GET** `/health`

**Authentication**: None required

**Response**: `200 OK`
```
Healthy
```

Returns:
- `200 OK` if API and database are healthy
- `503 Service Unavailable` if database connection fails

---

## 🚨 Error Responses

All errors follow RFC 7807 ProblemDetails format:

### 400 Bad Request
```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "Due date must be in the future",
  "instance": "/api/assignments"
}
```

### 401 Unauthorized
```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "SchoolId claim is missing",
  "instance": "/api/classes"
}
```

### 404 Not Found
```json
{
  "status": 404,
  "title": "Not Found",
  "detail": "Assignment not found",
  "instance": "/api/assignments/999"
}
```

### 409 Conflict
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "The assignment has been modified by another user",
  "instance": "/api/assignments/1"
}
```

---

## 🔐 Authorization Matrix

| Endpoint                     | Admin | Teacher | Student | Parent |
|------------------------------|-------|---------|---------|--------|
| POST /api/users              | ✅    | ❌      | ❌      | ❌     |
| POST /api/classes            | ✅    | ❌      | ❌      | ❌     |
| POST /api/enrolments/bulk    | ✅    | ❌      | ❌      | ❌     |
| POST /api/subjects           | ✅    | ❌      | ❌      | ❌     |
| POST /api/class-subjects/bulk| ✅    | ✅      | ❌      | ❌     |
| POST /api/assignments        | ✅    | ✅      | ❌      | ❌     |
| PUT /api/assignments/{id}    | ✅    | ✅      | ❌      | ❌     |
| POST /api/submissions        | ❌    | ❌      | ✅      | ❌     |
| GET /api/submissions/*       | ✅    | ✅      | ❌      | ❌     |
| POST /api/grades             | ✅    | ✅      | ❌      | ❌     |
| POST /api/attendance/bulk    | ✅    | ✅      | ❌      | ❌     |
| POST /api/announcements      | ✅    | ✅      | ❌      | ❌     |
| GET /api/reports/*           | ✅    | ✅      | ❌      | ❌     |

**Note**: All roles can access GET endpoints unless specified otherwise.

---

## 📚 Additional Resources

- **Swagger UI**: `/swagger` (Development only)
- **Postman Collection**: `SchoolPortal.postman_collection.json`
- **Health Check**: `/health`
- **Full Documentation**: `README.md`
