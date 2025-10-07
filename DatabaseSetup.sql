-- Database Setup Script for School Portal
-- Run this script after creating the database schema

USE SchoolPortalDB;
GO

-- Create Table-Valued Parameter Type for Attendance Bulk Upsert
IF TYPE_ID('dbo.AttendanceTableType') IS NOT NULL
    DROP TYPE dbo.AttendanceTableType;
GO

CREATE TYPE dbo.AttendanceTableType AS TABLE
(
    ClassId INT NOT NULL,
    StudentId INT NOT NULL,
    Date DATE NOT NULL,
    Status INT NOT NULL,
    Notes NVARCHAR(500),
    SchoolId INT NOT NULL
);
GO

-- Create Stored Procedure for Attendance Bulk Upsert
IF OBJECT_ID('dbo.usp_Attendance_BulkUpsert', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Attendance_BulkUpsert;
GO

CREATE PROCEDURE dbo.usp_Attendance_BulkUpsert
    @AttendanceData dbo.AttendanceTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    
    MERGE INTO Attendance AS target
    USING @AttendanceData AS source
    ON target.ClassId = source.ClassId 
        AND target.StudentId = source.StudentId 
        AND target.Date = source.Date
        AND target.SchoolId = source.SchoolId
    WHEN MATCHED THEN
        UPDATE SET 
            Status = source.Status,
            Notes = source.Notes,
            UpdatedAt = GETUTCDATE()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ClassId, StudentId, SchoolId, Date, Status, Notes, CreatedAt)
        VALUES (source.ClassId, source.StudentId, source.SchoolId, source.Date, source.Status, source.Notes, GETUTCDATE());
END;
GO

-- Create Attendance Summary View
IF OBJECT_ID('dbo.vw_AttendanceSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_AttendanceSummary;
GO

CREATE VIEW dbo.vw_AttendanceSummary
AS
SELECT 
    a.SchoolId,
    a.ClassId,
    c.Name AS ClassName,
    a.StudentId,
    u.FirstName + ' ' + u.LastName AS StudentName,
    s.StudentNumber,
    YEAR(a.Date) AS Year,
    MONTH(a.Date) AS Month,
    SUM(CASE WHEN a.Status = 1 THEN 1 ELSE 0 END) AS PresentCount,
    SUM(CASE WHEN a.Status = 0 THEN 1 ELSE 0 END) AS AbsentCount,
    SUM(CASE WHEN a.Status = 2 THEN 1 ELSE 0 END) AS LateCount,
    COUNT(*) AS TotalDays
FROM Attendance a
INNER JOIN Student s ON a.StudentId = s.StudentId
INNER JOIN [User] u ON s.UserId = u.UserId
INNER JOIN Class c ON a.ClassId = c.ClassId
GROUP BY 
    a.SchoolId,
    a.ClassId,
    c.Name,
    a.StudentId,
    u.FirstName,
    u.LastName,
    s.StudentNumber,
    YEAR(a.Date),
    MONTH(a.Date);
GO

-- Create Gradebook Simple View
IF OBJECT_ID('dbo.vw_GradebookSimple', 'V') IS NOT NULL
    DROP VIEW dbo.vw_GradebookSimple;
GO

CREATE VIEW dbo.vw_GradebookSimple
AS
SELECT 
    g.SchoolId,
    cs.ClassId,
    c.Name AS ClassName,
    sub.Name AS SubjectName,
    a.AssignmentId,
    a.Title AS AssignmentTitle,
    a.MaxMarks,
    s.StudentId,
    u.FirstName + ' ' + u.LastName AS StudentName,
    st.StudentNumber,
    g.Score,
    g.Feedback,
    g.GradedAt,
    CAST((g.Score / a.MaxMarks * 100) AS DECIMAL(5,2)) AS Percentage
FROM Grade g
INNER JOIN Submission sub_rec ON g.SubmissionId = sub_rec.SubmissionId
INNER JOIN Student st ON sub_rec.StudentId = st.StudentId
INNER JOIN [User] u ON st.UserId = u.UserId
INNER JOIN Assignment a ON sub_rec.AssignmentId = a.AssignmentId
INNER JOIN ClassSubject cs ON a.ClassSubjectId = cs.ClassSubjectId
INNER JOIN Class c ON cs.ClassId = c.ClassId
INNER JOIN Subject sub ON cs.SubjectId = sub.SubjectId;
GO

PRINT 'Database objects created successfully.';
GO

-- Seed Data Script
PRINT 'Starting seed data insertion...';
GO

-- Insert Sample School
IF NOT EXISTS (SELECT 1 FROM School WHERE Domain = 'demo.schoolportal.com')
BEGIN
    INSERT INTO School (Name, Domain, BrandingPrimaryColor, IsActive, CreatedAt)
    VALUES ('Demo High School', 'demo.schoolportal.com', '#1E40AF', 1, GETUTCDATE());
    
    PRINT 'Sample school created.';
END

DECLARE @SchoolId INT = (SELECT SchoolId FROM School WHERE Domain = 'demo.schoolportal.com');

-- Insert Admin User
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Email = 'admin@demo.schoolportal.com')
BEGIN
    -- Password: Admin@123 (BCrypt hashed)
    INSERT INTO [User] (SchoolId, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt)
    VALUES (
        @SchoolId, 
        'admin@demo.schoolportal.com', 
        '$2a$11$qGXZJKrLXrH8qYx9SLkfYOYx4jKx/9YHgEYxVX0ZK5/TLGl0KHK/K',
        'System',
        'Administrator',
        'Admin',
        1,
        GETUTCDATE()
    );
    
    PRINT 'Admin user created (email: admin@demo.schoolportal.com, password: Admin@123)';
END

DECLARE @AdminUserId INT = (SELECT UserId FROM [User] WHERE Email = 'admin@demo.schoolportal.com');

-- Insert Sample Teacher
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Email = 'teacher@demo.schoolportal.com')
BEGIN
    INSERT INTO [User] (SchoolId, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt)
    VALUES (
        @SchoolId,
        'teacher@demo.schoolportal.com',
        '$2a$11$qGXZJKrLXrH8qYx9SLkfYOYx4jKx/9YHgEYxVX0ZK5/TLGl0KHK/K',
        'John',
        'Teacher',
        'Teacher',
        1,
        GETUTCDATE()
    );
    
    DECLARE @TeacherUserId INT = SCOPE_IDENTITY();
    
    INSERT INTO Teacher (UserId, SchoolId, EmployeeNumber, Specialization, CreatedAt)
    VALUES (@TeacherUserId, @SchoolId, 'T001', 'Mathematics', GETUTCDATE());
    
    PRINT 'Teacher user created (email: teacher@demo.schoolportal.com, password: Admin@123)';
END

-- Insert Sample Student
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Email = 'student@demo.schoolportal.com')
BEGIN
    INSERT INTO [User] (SchoolId, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt)
    VALUES (
        @SchoolId,
        'student@demo.schoolportal.com',
        '$2a$11$qGXZJKrLXrH8qYx9SLkfYOYx4jKx/9YHgEYxVX0ZK5/TLGl0KHK/K',
        'Jane',
        'Student',
        'Student',
        1,
        GETUTCDATE()
    );
    
    DECLARE @StudentUserId INT = SCOPE_IDENTITY();
    
    INSERT INTO Student (UserId, SchoolId, StudentNumber, GradeLevel, CreatedAt)
    VALUES (@StudentUserId, @SchoolId, 'S2024001', 10, GETUTCDATE());
    
    PRINT 'Student user created (email: student@demo.schoolportal.com, password: Admin@123)';
END

-- Insert Sample Subjects
IF NOT EXISTS (SELECT 1 FROM Subject WHERE SchoolId = @SchoolId)
BEGIN
    INSERT INTO Subject (SchoolId, Name, Code, CreatedAt)
    VALUES 
        (@SchoolId, 'Mathematics', 'MATH', GETUTCDATE()),
        (@SchoolId, 'English', 'ENG', GETUTCDATE()),
        (@SchoolId, 'Science', 'SCI', GETUTCDATE()),
        (@SchoolId, 'History', 'HIST', GETUTCDATE());
    
    PRINT 'Sample subjects created.';
END

-- Insert Sample Class
DECLARE @TeacherId INT = (SELECT TeacherId FROM Teacher WHERE SchoolId = @SchoolId);

IF NOT EXISTS (SELECT 1 FROM Class WHERE SchoolId = @SchoolId)
BEGIN
    INSERT INTO Class (SchoolId, Name, GradeLevel, AcademicYear, TeacherId, MaxCapacity, CreatedAt)
    VALUES (@SchoolId, 'Grade 10A', 10, 2024, @TeacherId, 30, GETUTCDATE());
    
    PRINT 'Sample class created.';
END

-- Enroll Student in Class
DECLARE @ClassId INT = (SELECT TOP 1 ClassId FROM Class WHERE SchoolId = @SchoolId);
DECLARE @StudentId INT = (SELECT StudentId FROM Student WHERE SchoolId = @SchoolId);

IF NOT EXISTS (SELECT 1 FROM Enrollment WHERE ClassId = @ClassId AND StudentId = @StudentId)
BEGIN
    INSERT INTO Enrollment (ClassId, StudentId, SchoolId, EnrolledAt, IsActive)
    VALUES (@ClassId, @StudentId, @SchoolId, GETUTCDATE(), 1);
    
    PRINT 'Student enrolled in class.';
END

PRINT 'Seed data insertion completed successfully!';
PRINT '';
PRINT 'Login Credentials:';
PRINT '  Admin:   admin@demo.schoolportal.com / Admin@123';
PRINT '  Teacher: teacher@demo.schoolportal.com / Admin@123';
PRINT '  Student: student@demo.schoolportal.com / Admin@123';
GO
