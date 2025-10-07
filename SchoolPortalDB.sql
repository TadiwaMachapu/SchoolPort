/* ============================================================
   SchoolPortalDB – Core Schema (Phase 1 MVP)
   Entities: School, User, Student, Teacher, Class, Subject,
             ClassSubject, Enrollment, Assignment, Submission,
             Grade, Attendance, Announcement, AuditLog
   ============================================================ */

-- 0) Create DB
IF DB_ID('SchoolPortalDB') IS NULL
BEGIN
    CREATE DATABASE SchoolPortalDB;
END
GO
USE SchoolPortalDB;
GO

/*--------Tables--------*/


--2.1 School 
IF OBJECT_ID('dbo.School','U') IS NOT NULL DROP TABLE dbo.School;
CREATE TABLE dbo.School
(
    SchoolId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_School_SchoolId DEFAULT NEWSEQUENTIALID(),
    Name           NVARCHAR(200)    NOT NULL,
    Domain         NVARCHAR(200)    NULL,
    LogoUrl        NVARCHAR(500)    NULL,
    Active         BIT              NOT NULL CONSTRAINT DF_School_Active DEFAULT (1),
    CreatedAt      DATETIME2(0)     NOT NULL CONSTRAINT DF_School_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVer         ROWVERSION       NOT NULL,
    CONSTRAINT PK_School PRIMARY KEY (SchoolId),
    CONSTRAINT UQ_School_Domain UNIQUE (Domain)
);
GO

-- 2.2 [User]
IF OBJECT_ID('dbo.[User]','U') IS NOT NULL DROP TABLE dbo.[User];
CREATE TABLE dbo.[User]
(
    UserId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_User_UserId DEFAULT NEWSEQUENTIALID(),
    SchoolId      UNIQUEIDENTIFIER NOT NULL,
    FirstName     NVARCHAR(100)    NOT NULL,
    LastName      NVARCHAR(100)    NOT NULL,
    Email         NVARCHAR(256)    NOT NULL,
    PasswordHash  NVARCHAR(400)    NULL, -- if using local auth; nullable for SSO
    Role          NVARCHAR(30)     NOT NULL, -- Admin, Teacher, Student, Parent
    IsActive      BIT              NOT NULL CONSTRAINT DF_User_IsActive DEFAULT (1),
    CreatedAt     DATETIME2(0)     NOT NULL CONSTRAINT DF_User_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVer        ROWVERSION       NOT NULL,
    CONSTRAINT PK_User PRIMARY KEY (UserId),
    CONSTRAINT FK_User_School FOREIGN KEY (SchoolId) REFERENCES dbo.School(SchoolId),
    CONSTRAINT UQ_User_Email_PerSchool UNIQUE (SchoolId, Email)
);
GO

-- 2.3 Student
IF OBJECT_ID('dbo.Student','U') IS NOT NULL DROP TABLE dbo.Student;
CREATE TABLE dbo.Student
(
    StudentId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Student_StudentId DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    AdmissionNumber NVARCHAR(50)     NULL,
    GradeYear       INT              NULL,
    RowVer          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Student PRIMARY KEY (StudentId),
    CONSTRAINT FK_Student_User FOREIGN KEY (UserId) REFERENCES dbo.[User](UserId)
);
GO

-- 2.4 Teacher
IF OBJECT_ID('dbo.Teacher','U') IS NOT NULL DROP TABLE dbo.Teacher;
CREATE TABLE dbo.Teacher
(
    TeacherId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Teacher_TeacherId DEFAULT NEWSEQUENTIALID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    StaffNumber NVARCHAR(50)     NULL,
    Department  NVARCHAR(100)    NULL,
    RowVer      ROWVERSION       NOT NULL,
    CONSTRAINT PK_Teacher PRIMARY KEY (TeacherId),
    CONSTRAINT FK_Teacher_User FOREIGN KEY (UserId) REFERENCES dbo.[User](UserId)
);
GO

-- 2.5 Class
IF OBJECT_ID('dbo.Class','U') IS NOT NULL DROP TABLE dbo.Class;
CREATE TABLE dbo.Class
(
    ClassId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Class_ClassId DEFAULT NEWSEQUENTIALID(),
    SchoolId      UNIQUEIDENTIFIER NOT NULL,
    Name          NVARCHAR(100)    NOT NULL,  -- e.g., Grade 10A
    AcademicYear  INT              NOT NULL,
    HomeroomTeacherId UNIQUEIDENTIFIER NULL, -- FK -> Teacher
    CreatedAt     DATETIME2(0)     NOT NULL CONSTRAINT DF_Class_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVer        ROWVERSION       NOT NULL,
    CONSTRAINT PK_Class PRIMARY KEY (ClassId),
    CONSTRAINT FK_Class_School FOREIGN KEY (SchoolId) REFERENCES dbo.School(SchoolId),
    CONSTRAINT FK_Class_HomeroomTeacher FOREIGN KEY (HomeroomTeacherId) REFERENCES dbo.Teacher(TeacherId),
    CONSTRAINT UQ_Class_NameYear UNIQUE (SchoolId, Name, AcademicYear)
);
GO

-- 2.6 Subject
IF OBJECT_ID('dbo.Subject','U') IS NOT NULL DROP TABLE dbo.Subject;
CREATE TABLE dbo.Subject
(
    SubjectId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Subject_SubjectId DEFAULT NEWSEQUENTIALID(),
    SchoolId  UNIQUEIDENTIFIER NOT NULL,
    Name      NVARCHAR(120)    NOT NULL,
    Code      NVARCHAR(50)     NULL,
    RowVer    ROWVERSION       NOT NULL,
    CONSTRAINT PK_Subject PRIMARY KEY (SubjectId),
    CONSTRAINT FK_Subject_School FOREIGN KEY (SchoolId) REFERENCES dbo.School(SchoolId),
    CONSTRAINT UQ_Subject_Code UNIQUE (SchoolId, Code)
);
GO

-- 2.7 ClassSubject (which teacher teaches which subject in which class)
IF OBJECT_ID('dbo.ClassSubject','U') IS NOT NULL DROP TABLE dbo.ClassSubject;
CREATE TABLE dbo.ClassSubject
(
    ClassSubjectId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ClassSubject_Id DEFAULT NEWSEQUENTIALID(),
    ClassId        UNIQUEIDENTIFIER NOT NULL,
    SubjectId      UNIQUEIDENTIFIER NOT NULL,
    TeacherId      UNIQUEIDENTIFIER NOT NULL,
    RowVer         ROWVERSION       NOT NULL,
    CONSTRAINT PK_ClassSubject PRIMARY KEY (ClassSubjectId),
    CONSTRAINT FK_ClassSubject_Class   FOREIGN KEY (ClassId)   REFERENCES dbo.Class(ClassId),
    CONSTRAINT FK_ClassSubject_Subject FOREIGN KEY (SubjectId) REFERENCES dbo.Subject(SubjectId),
    CONSTRAINT FK_ClassSubject_Teacher FOREIGN KEY (TeacherId) REFERENCES dbo.Teacher(TeacherId),
    CONSTRAINT UQ_ClassSubject UNIQUE (ClassId, SubjectId)
);
GO

-- 2.8 Enrollment (student in a class)
IF OBJECT_ID('dbo.Enrollment','U') IS NOT NULL DROP TABLE dbo.Enrollment;
CREATE TABLE dbo.Enrollment
(
    EnrollmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Enrollment_Id DEFAULT NEWSEQUENTIALID(),
    ClassId      UNIQUEIDENTIFIER NOT NULL,
    StudentId    UNIQUEIDENTIFIER NOT NULL,
    EnrolledAt   DATETIME2(0)     NOT NULL CONSTRAINT DF_Enrollment_At DEFAULT SYSUTCDATETIME(),
    RowVer       ROWVERSION       NOT NULL,
    CONSTRAINT PK_Enrollment PRIMARY KEY (EnrollmentId),
    CONSTRAINT FK_Enrollment_Class   FOREIGN KEY (ClassId)   REFERENCES dbo.Class(ClassId),
    CONSTRAINT FK_Enrollment_Student FOREIGN KEY (StudentId) REFERENCES dbo.Student(StudentId),
    CONSTRAINT UQ_Enrollment UNIQUE (ClassId, StudentId)
);
GO

-- 2.9 Assignment
IF OBJECT_ID('dbo.Assignment','U') IS NOT NULL DROP TABLE dbo.Assignment;
CREATE TABLE dbo.Assignment
(
    AssignmentId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Assignment_Id DEFAULT NEWSEQUENTIALID(),
    ClassSubjectId UNIQUEIDENTIFIER NOT NULL,
    Title          NVARCHAR(200)    NOT NULL,
    Description    NVARCHAR(MAX)    NULL,
    DueAt          DATETIME2(0)     NOT NULL,
    MaxMarks       INT              NOT NULL CONSTRAINT DF_Assignment_MaxMarks DEFAULT (100),
    CreatedAt      DATETIME2(0)     NOT NULL CONSTRAINT DF_Assignment_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVer         ROWVERSION       NOT NULL,
    CONSTRAINT PK_Assignment PRIMARY KEY (AssignmentId),
    CONSTRAINT FK_Assignment_ClassSubject FOREIGN KEY (ClassSubjectId) REFERENCES dbo.ClassSubject(ClassSubjectId)
);
GO

-- 2.10 Submission (one per student per assignment)
IF OBJECT_ID('dbo.Submission','U') IS NOT NULL DROP TABLE dbo.Submission;
CREATE TABLE dbo.Submission
(
    SubmissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Submission_Id DEFAULT NEWSEQUENTIALID(),
    AssignmentId UNIQUEIDENTIFIER NOT NULL,
    StudentId    UNIQUEIDENTIFIER NOT NULL,
    BlobPath     NVARCHAR(500)    NOT NULL, -- or BlobUrl
    SubmittedAt  DATETIME2(0)     NOT NULL CONSTRAINT DF_Submission_SubmittedAt DEFAULT SYSUTCDATETIME(),
    RowVer       ROWVERSION       NOT NULL,
    CONSTRAINT PK_Submission PRIMARY KEY (SubmissionId),
    CONSTRAINT FK_Submission_Assignment FOREIGN KEY (AssignmentId) REFERENCES dbo.Assignment(AssignmentId),
    CONSTRAINT FK_Submission_Student    FOREIGN KEY (StudentId)    REFERENCES dbo.Student(StudentId),
    CONSTRAINT UQ_Submission UNIQUE (AssignmentId, StudentId)
);
GO

-- 2.11 Grade (1:1 with Submission)
IF OBJECT_ID('dbo.Grade','U') IS NOT NULL DROP TABLE dbo.Grade;
CREATE TABLE dbo.Grade
(
    GradeId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Grade_Id DEFAULT NEWSEQUENTIALID(),
    SubmissionId UNIQUEIDENTIFIER NOT NULL,
    Score        DECIMAL(5,2)     NOT NULL,
    Feedback     NVARCHAR(MAX)    NULL,
    TeacherId    UNIQUEIDENTIFIER NOT NULL,
    GradedAt     DATETIME2(0)     NOT NULL CONSTRAINT DF_Grade_At DEFAULT SYSUTCDATETIME(),
    RowVer       ROWVERSION       NOT NULL,
    CONSTRAINT PK_Grade PRIMARY KEY (GradeId),
    CONSTRAINT FK_Grade_Submission FOREIGN KEY (SubmissionId) REFERENCES dbo.Submission(SubmissionId),
    CONSTRAINT FK_Grade_Teacher    FOREIGN KEY (TeacherId)    REFERENCES dbo.Teacher(TeacherId),
    CONSTRAINT UQ_Grade_Submission UNIQUE (SubmissionId)
);
GO

-- 2.12 Attendance
IF OBJECT_ID('dbo.Attendance','U') IS NOT NULL DROP TABLE dbo.Attendance;
CREATE TABLE dbo.Attendance
(
    AttendanceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Attendance_Id DEFAULT NEWSEQUENTIALID(),
    ClassId      UNIQUEIDENTIFIER NOT NULL,
    StudentId    UNIQUEIDENTIFIER NOT NULL,
    [Date]       DATE             NOT NULL,
    Status       TINYINT          NOT NULL,  -- 0=Present,1=Absent,2=Late (client maps)
    Note         NVARCHAR(300)    NULL,
    MarkedAt     DATETIME2(0)     NOT NULL CONSTRAINT DF_Attendance_MarkedAt DEFAULT SYSUTCDATETIME(),
    RowVer       ROWVERSION       NOT NULL,
    CONSTRAINT PK_Attendance PRIMARY KEY (AttendanceId),
    CONSTRAINT FK_Attendance_Class   FOREIGN KEY (ClassId)   REFERENCES dbo.Class(ClassId),
    CONSTRAINT FK_Attendance_Student FOREIGN KEY (StudentId) REFERENCES dbo.Student(StudentId),
    CONSTRAINT UQ_Attendance UNIQUE (ClassId, StudentId, [Date])
);
GO

-- 2.13 Announcement
IF OBJECT_ID('dbo.Announcement','U') IS NOT NULL DROP TABLE dbo.Announcement;
CREATE TABLE dbo.Announcement
(
    AnnouncementId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Announcement_Id DEFAULT NEWSEQUENTIALID(),
    SchoolId        UNIQUEIDENTIFIER NOT NULL,
    Title           NVARCHAR(200)    NOT NULL,
    Body            NVARCHAR(MAX)    NOT NULL,
    Audience        NVARCHAR(30)     NOT NULL, -- All, Grade, Class
    AudienceValue   NVARCHAR(50)     NULL,     -- e.g., "10" for Grade 10, or ClassId string
    PublishAt       DATETIME2(0)     NOT NULL CONSTRAINT DF_Announcement_PublishAt DEFAULT SYSUTCDATETIME(),
    CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
    RowVer          ROWVERSION       NOT NULL,
    CONSTRAINT PK_Announcement PRIMARY KEY (AnnouncementId),
    CONSTRAINT FK_Announcement_School FOREIGN KEY (SchoolId) REFERENCES dbo.School(SchoolId),
    CONSTRAINT FK_Announcement_User   FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User](UserId)
);
GO

-- 2.14 AuditLog
IF OBJECT_ID('dbo.AuditLog','U') IS NOT NULL DROP TABLE dbo.AuditLog;
CREATE TABLE dbo.AuditLog
(
    AuditId     BIGINT IDENTITY(1,1) NOT NULL,
    SchoolId    UNIQUEIDENTIFIER     NULL,
    UserId      UNIQUEIDENTIFIER     NULL,
    Action      NVARCHAR(100)        NOT NULL, -- e.g., "CREATE_ASSIGNMENT"
    EntityType  NVARCHAR(100)        NOT NULL,
    EntityId    NVARCHAR(100)        NOT NULL,
    DetailsJson NVARCHAR(MAX)        NULL,
    CreatedAt   DATETIME2(0)         NOT NULL CONSTRAINT DF_Audit_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLog PRIMARY KEY (AuditId)
);
GO


/*---------- 3) Indexes (This will help with performance) -------------*/

CREATE INDEX IX_User_School_Role      ON dbo.[User](SchoolId, Role);
CREATE INDEX IX_Class_School_Year     ON dbo.Class(SchoolId, AcademicYear);
CREATE INDEX IX_Enrollment_Class      ON dbo.Enrollment(ClassId);
CREATE INDEX IX_Enrollment_Student    ON dbo.Enrollment(StudentId);
CREATE INDEX IX_Assignment_ClassSub   ON dbo.Assignment(ClassSubjectId, DueAt);
CREATE INDEX IX_Submission_Assign_Stu ON dbo.Submission(AssignmentId, StudentId);
CREATE INDEX IX_Grade_Teacher         ON dbo.Grade(TeacherId);
CREATE INDEX IX_Attendance_Class_Date ON dbo.Attendance(ClassId, [Date]);
CREATE INDEX IX_Announcement_School   ON dbo.Announcement(SchoolId, PublishAt DESC);

/* ---------- 4) Views (useful for UI/API) ---------- */

-- 4.1 Attendance summary per student per class & year
CREATE VIEW dbo.vw_AttendanceSummary AS
SELECT
    c.SchoolId,
    c.ClassId,
    c.Name      AS ClassName,
    c.AcademicYear,
    s.StudentId,
    u.FirstName + ' ' + u.LastName AS StudentName,
    SUM(CASE WHEN a.Status = 0 THEN 1 ELSE 0 END) AS PresentCount,
    SUM(CASE WHEN a.Status = 1 THEN 1 ELSE 0 END) AS AbsentCount,
    SUM(CASE WHEN a.Status = 2 THEN 1 ELSE 0 END) AS LateCount,
    COUNT(a.AttendanceId)                           AS TotalMarked
FROM dbo.Attendance a
JOIN dbo.Class c     ON c.ClassId   = a.ClassId
JOIN dbo.Student s   ON s.StudentId = a.StudentId
JOIN dbo.[User] u    ON u.UserId    = s.UserId
GROUP BY c.SchoolId, c.ClassId, c.Name, c.AcademicYear, s.StudentId, u.FirstName, u.LastName;
GO

-- 4.2 Upcoming assignments for a student
CREATE VIEW dbo.vw_StudentAssignmentsDue AS
SELECT
    st.StudentId,
    a.AssignmentId,
    a.Title,
    a.DueAt,
    cs.ClassId,
    c.Name AS ClassName,
    sub.SubmissionId
FROM dbo.Student st
JOIN dbo.Enrollment e        ON e.StudentId = st.StudentId
JOIN dbo.Class c             ON c.ClassId   = e.ClassId
JOIN dbo.ClassSubject cs     ON cs.ClassId  = c.ClassId
JOIN dbo.Assignment a        ON a.ClassSubjectId = cs.ClassSubjectId
LEFT JOIN dbo.Submission sub ON sub.AssignmentId = a.AssignmentId AND sub.StudentId = st.StudentId
WHERE a.DueAt >= DATEADD(DAY, -7, SYSUTCDATETIME()); -- recent & upcoming
GO

-- 4.3 Simple gradebook (scores per assignment)
CREATE VIEW dbo.vw_GradebookSimple AS
SELECT
    c.ClassId,
    c.Name AS ClassName,
    a.AssignmentId,
    a.Title AS AssignmentTitle,
    st.StudentId,
    u.FirstName + ' ' + u.LastName AS StudentName,
    g.Score,
    g.GradedAt
FROM dbo.Class c
JOIN dbo.ClassSubject cs ON cs.ClassId = c.ClassId
JOIN dbo.Assignment a    ON a.ClassSubjectId = cs.ClassSubjectId
JOIN dbo.Enrollment e    ON e.ClassId = c.ClassId
JOIN dbo.Student st      ON st.StudentId = e.StudentId
JOIN dbo.[User] u        ON u.UserId = st.UserId
LEFT JOIN dbo.Submission sub ON sub.AssignmentId = a.AssignmentId AND sub.StudentId = st.StudentId
LEFT JOIN dbo.Grade g        ON g.SubmissionId   = sub.SubmissionId;
GO

/* ---------- 5) Table Type & Stored Procedures ---------- */

-- 5.1 TVP for bulk attendance upsert
IF TYPE_ID('dbo.AttendanceBulkType') IS NULL
    CREATE TYPE dbo.AttendanceBulkType AS TABLE
    (
        ClassId   UNIQUEIDENTIFIER NOT NULL,
        StudentId UNIQUEIDENTIFIER NOT NULL,
        [Date]    DATE             NOT NULL,
        Status    TINYINT          NOT NULL,
        Note      NVARCHAR(300)    NULL
    );
GO

-- 5.2 Bulk Upsert Attendance (idempotent for unique key)
CREATE OR ALTER PROCEDURE dbo.usp_Attendance_BulkUpsert
    @Rows dbo.AttendanceBulkType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.Attendance AS trg
    USING @Rows AS src
      ON  trg.ClassId   = src.ClassId
      AND trg.StudentId = src.StudentId
      AND trg.[Date]    = src.[Date]
    WHEN MATCHED THEN
      UPDATE SET
        trg.Status   = src.Status,
        trg.Note     = src.Note,
        trg.MarkedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
      INSERT (AttendanceId, ClassId, StudentId, [Date], Status, Note, MarkedAt)
      VALUES (DEFAULT,      src.ClassId, src.StudentId, src.[Date], src.Status, src.Note, SYSUTCDATETIME());
END
GO

-- 5.3 Dashboard helper for a student (assignments due, recent grades, announcements)
CREATE PROCEDURE dbo.usp_GetStudentDashboard
    @StudentId UNIQUEIDENTIFIER,
    @DaysAhead INT = 7
AS
BEGIN
    SET NOCOUNT ON;

    -- Due soon (without submission)
    SELECT TOP 20
        d.AssignmentId, d.Title, d.DueAt, d.ClassId, d.ClassName
    FROM dbo.vw_StudentAssignmentsDue d
    WHERE d.StudentId = @StudentId
      AND d.SubmissionId IS NULL
      AND d.DueAt <= DATEADD(DAY, @DaysAhead, SYSUTCDATETIME())
    ORDER BY d.DueAt;

    -- Recent grades
    SELECT TOP 10
        a.AssignmentId, a.Title, g.Score, g.GradedAt
    FROM dbo.Submission sub
    JOIN dbo.Grade g      ON g.SubmissionId = sub.SubmissionId
    JOIN dbo.Assignment a ON a.AssignmentId = sub.AssignmentId
    WHERE sub.StudentId = @StudentId
    ORDER BY g.GradedAt DESC;

    -- Announcements (school-wide for the student’s school)
    SELECT TOP 10 an.AnnouncementId, an.Title, an.PublishAt
    FROM dbo.Student st
    JOIN dbo.[User] u ON u.UserId = st.UserId
    JOIN dbo.Announcement an ON an.SchoolId = u.SchoolId
    WHERE st.StudentId = @StudentId
      AND an.PublishAt <= SYSUTCDATETIME()
    ORDER BY an.PublishAt DESC;
END
GO