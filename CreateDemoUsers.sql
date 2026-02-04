-- Create Demo Users for School Portal
-- Run this script against your SchoolPortalDB database

USE SchoolPortalDB;
GO

-- Check if demo school exists, if not create it
IF NOT EXISTS (SELECT 1 FROM Schools WHERE Name = 'Demo School')
BEGIN
    INSERT INTO Schools (Name, Address, PhoneNumber, Email, IsActive, CreatedAt)
    VALUES ('Demo School', '123 Education Street', '555-0100', 'info@demo.schoolportal.com', 1, GETUTCDATE());
END
GO

DECLARE @SchoolId INT = (SELECT SchoolId FROM Schools WHERE Name = 'Demo School');

-- Create Admin User
-- Password: Admin@123 (BCrypt hashed)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@demo.schoolportal.com')
BEGIN
    INSERT INTO Users (SchoolId, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt)
    VALUES (
        @SchoolId,
        'admin@demo.schoolportal.com',
        '$2a$11$8YpNm5vJZ5F5F5F5F5F5FeKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK', -- This is a placeholder, see note below
        'Admin',
        'User',
        'Admin',
        1,
        GETUTCDATE()
    );
    PRINT 'Admin user created';
END
ELSE
BEGIN
    PRINT 'Admin user already exists';
END
GO

DECLARE @SchoolId INT = (SELECT SchoolId FROM Schools WHERE Name = 'Demo School');

-- Create Teacher User
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'teacher@demo.schoolportal.com')
BEGIN
    DECLARE @TeacherUserId INT;
    
    INSERT INTO Users (SchoolId, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt)
    VALUES (
        @SchoolId,
        'teacher@demo.schoolportal.com',
        '$2a$11$8YpNm5vJZ5F5F5F5F5F5FeKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK', -- This is a placeholder
        'Teacher',
        'Demo',
        'Teacher',
        1,
        GETUTCDATE()
    );
    
    SET @TeacherUserId = SCOPE_IDENTITY();
    
    -- Create Teacher record
    INSERT INTO Teachers (UserId, SchoolId, EmployeeNumber, Specialization, CreatedAt)
    VALUES (@TeacherUserId, @SchoolId, 'T001', 'Mathematics', GETUTCDATE());
    
    PRINT 'Teacher user created';
END
ELSE
BEGIN
    PRINT 'Teacher user already exists';
END
GO

DECLARE @SchoolId INT = (SELECT SchoolId FROM Schools WHERE Name = 'Demo School');

-- Create Student User
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'student@demo.schoolportal.com')
BEGIN
    DECLARE @StudentUserId INT;
    
    INSERT INTO Users (SchoolId, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt)
    VALUES (
        @SchoolId,
        'student@demo.schoolportal.com',
        '$2a$11$8YpNm5vJZ5F5F5F5F5F5FeKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK', -- This is a placeholder
        'Student',
        'Demo',
        'Student',
        1,
        GETUTCDATE()
    );
    
    SET @StudentUserId = SCOPE_IDENTITY();
    
    -- Create Student record
    INSERT INTO Students (UserId, SchoolId, StudentNumber, DateOfBirth, CreatedAt)
    VALUES (@StudentUserId, @SchoolId, 'S001', '2010-01-01', GETUTCDATE());
    
    PRINT 'Student user created';
END
ELSE
BEGIN
    PRINT 'Student user already exists';
END
GO

PRINT 'Demo users setup complete!';
PRINT '';
PRINT 'IMPORTANT: The password hashes above are placeholders.';
PRINT 'You need to use the backend API to create users with proper password hashing.';
PRINT 'Or use Option 2 below to create users via the API endpoint.';
GO
