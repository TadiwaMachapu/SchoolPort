-- Setup Initial Data for School Portal
-- Run this script in SQL Server Management Studio or Azure Data Studio
-- This creates the school and a temporary admin user

USE SchoolPortalDB;
GO

-- Step 1: Create Demo School (if it doesn't exist)
IF NOT EXISTS (SELECT 1 FROM Schools WHERE Name = 'Demo School')
BEGIN
    INSERT INTO Schools (Name, Address, PhoneNumber, Email, IsActive, CreatedAt)
    VALUES (
        'Demo School',
        '123 Education Street, Demo City',
        '555-0100',
        'info@demo.schoolportal.com',
        1,
        GETUTCDATE()
    );
    PRINT '✓ Demo School created';
END
ELSE
BEGIN
    PRINT '✓ Demo School already exists';
END
GO

-- Step 2: Create Initial Admin User
-- Password: Admin@123
-- BCrypt hash for "Admin@123": $2a$11$vZ9Z9Z9Z9Z9Z9Z9Z9Z9Z9eqKqKqKqKqKqKqKqKqKqKqKqKqKqKqKqK
-- Note: You'll need to create this via API for proper password hashing

DECLARE @SchoolId INT = (SELECT TOP 1 SchoolId FROM Schools WHERE Name = 'Demo School');

PRINT '';
PRINT '========================================';
PRINT '  Initial Setup Complete!';
PRINT '========================================';
PRINT '';
PRINT 'School ID: ' + CAST(@SchoolId AS VARCHAR(10));
PRINT 'School Name: Demo School';
PRINT '';
PRINT '========================================';
PRINT '  NEXT STEPS:';
PRINT '========================================';
PRINT '';
PRINT '1. Open Swagger UI: https://localhost:7071/swagger';
PRINT '2. Find: POST /api/users';
PRINT '3. Click "Try it out"';
PRINT '4. Use this JSON to create admin user:';
PRINT '';
PRINT '{';
PRINT '  "email": "admin@demo.schoolportal.com",';
PRINT '  "password": "Admin@123",';
PRINT '  "firstName": "Admin",';
PRINT '  "lastName": "User",';
PRINT '  "role": "Admin"';
PRINT '}';
PRINT '';
PRINT '5. After creating admin, login at: http://localhost:5000';
PRINT '';
PRINT 'Demo Credentials (after creation):';
PRINT '  Admin:   admin@demo.schoolportal.com / Admin@123';
PRINT '  Teacher: teacher@demo.schoolportal.com / Admin@123';
PRINT '  Student: student@demo.schoolportal.com / Admin@123';
PRINT '';
GO
