-- PostgreSQL/Supabase Database Setup Script for School Portal
-- Run this script after EF Core migrations have created the base schema

-- ============================================
-- Create Attendance Summary View (Postgres)
-- ============================================
DROP VIEW IF EXISTS vw_attendance_summary;

CREATE VIEW vw_attendance_summary AS
SELECT 
    a.school_id,
    a.class_id,
    c.name AS class_name,
    a.student_id,
    u.first_name || ' ' || u.last_name AS student_name,
    s.student_number,
    EXTRACT(YEAR FROM a.date)::INTEGER AS year,
    EXTRACT(MONTH FROM a.date)::INTEGER AS month,
    SUM(CASE WHEN a.status = 1 THEN 1 ELSE 0 END)::INTEGER AS present_count,
    SUM(CASE WHEN a.status = 0 THEN 1 ELSE 0 END)::INTEGER AS absent_count,
    SUM(CASE WHEN a.status = 2 THEN 1 ELSE 0 END)::INTEGER AS late_count,
    COUNT(*)::INTEGER AS total_days
FROM attendances a
INNER JOIN students s ON a.student_id = s.student_id
INNER JOIN users u ON s.user_id = u.user_id
INNER JOIN classes c ON a.class_id = c.class_id
GROUP BY 
    a.school_id,
    a.class_id,
    c.name,
    a.student_id,
    u.first_name,
    u.last_name,
    s.student_number,
    EXTRACT(YEAR FROM a.date),
    EXTRACT(MONTH FROM a.date);

-- ============================================
-- Create Gradebook Simple View (Postgres)
-- ============================================
DROP VIEW IF EXISTS vw_gradebook_simple;

CREATE VIEW vw_gradebook_simple AS
SELECT 
    g.school_id,
    cs.class_id,
    c.name AS class_name,
    sub.name AS subject_name,
    a.assignment_id,
    a.title AS assignment_title,
    a.max_marks,
    st.student_id,
    u.first_name || ' ' || u.last_name AS student_name,
    st.student_number,
    g.score,
    g.feedback,
    g.graded_at,
    ROUND((g.score / a.max_marks * 100)::NUMERIC, 2) AS percentage
FROM grades g
INNER JOIN submissions sub_rec ON g.submission_id = sub_rec.submission_id
INNER JOIN students st ON sub_rec.student_id = st.student_id
INNER JOIN users u ON st.user_id = u.user_id
INNER JOIN assignments a ON sub_rec.assignment_id = a.assignment_id
INNER JOIN class_subjects cs ON a.class_subject_id = cs.class_subject_id
INNER JOIN classes c ON cs.class_id = c.class_id
INNER JOIN subjects sub ON cs.subject_id = sub.subject_id;

-- ============================================
-- Create unique index for attendance upsert
-- (This should already exist from EF Core migration, but ensure it exists)
-- ============================================
CREATE UNIQUE INDEX IF NOT EXISTS ix_attendances_school_class_student_date 
ON attendances (school_id, class_id, student_id, date);

-- ============================================
-- Seed Data Script (Postgres)
-- ============================================
DO $$
DECLARE
    v_school_id INTEGER;
    v_admin_user_id INTEGER;
    v_teacher_user_id INTEGER;
    v_student_user_id INTEGER;
    v_teacher_id INTEGER;
    v_student_id INTEGER;
    v_class_id INTEGER;
BEGIN
    -- Insert Sample School
    INSERT INTO schools (name, domain, branding_primary_color, is_active, created_at, row_version)
    SELECT 'Demo High School', 'demo.schoolportal.com', '#1E40AF', true, NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM schools WHERE domain = 'demo.schoolportal.com')
    RETURNING school_id INTO v_school_id;

    IF v_school_id IS NULL THEN
        SELECT school_id INTO v_school_id FROM schools WHERE domain = 'demo.schoolportal.com';
    END IF;

    RAISE NOTICE 'School ID: %', v_school_id;

    -- Insert Admin User
    -- Password: Admin@123 (BCrypt hashed)
    INSERT INTO users (school_id, email, password_hash, first_name, last_name, role, is_active, created_at, row_version)
    SELECT v_school_id, 'admin@demo.schoolportal.com', 
           '$2a$11$k9j/L2NaoEQfrhbDKyHM6O3SK3UoMX7RiPTaprJYoSD/tFzq03Rf.',
           'System', 'Administrator', 'Admin', true, NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'admin@demo.schoolportal.com')
    RETURNING user_id INTO v_admin_user_id;

    RAISE NOTICE 'Admin user created (email: admin@demo.schoolportal.com, password: Admin@123)';

    -- Insert Sample Teacher
    INSERT INTO users (school_id, email, password_hash, first_name, last_name, role, is_active, created_at, row_version)
    SELECT v_school_id, 'teacher@demo.schoolportal.com',
           '$2a$11$k9j/L2NaoEQfrhbDKyHM6O3SK3UoMX7RiPTaprJYoSD/tFzq03Rf.',
           'John', 'Teacher', 'Teacher', true, NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'teacher@demo.schoolportal.com')
    RETURNING user_id INTO v_teacher_user_id;

    IF v_teacher_user_id IS NOT NULL THEN
        INSERT INTO teachers (user_id, school_id, employee_number, specialization, created_at, row_version)
        VALUES (v_teacher_user_id, v_school_id, 'T001', 'Mathematics', NOW(), 1)
        RETURNING teacher_id INTO v_teacher_id;
        
        RAISE NOTICE 'Teacher user created (email: teacher@demo.schoolportal.com, password: Admin@123)';
    ELSE
        SELECT teacher_id INTO v_teacher_id FROM teachers WHERE school_id = v_school_id LIMIT 1;
    END IF;

    -- Insert Sample Student
    INSERT INTO users (school_id, email, password_hash, first_name, last_name, role, is_active, created_at, row_version)
    SELECT v_school_id, 'student@demo.schoolportal.com',
           '$2a$11$k9j/L2NaoEQfrhbDKyHM6O3SK3UoMX7RiPTaprJYoSD/tFzq03Rf.',
           'Jane', 'Student', 'Student', true, NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'student@demo.schoolportal.com')
    RETURNING user_id INTO v_student_user_id;

    IF v_student_user_id IS NOT NULL THEN
        INSERT INTO students (user_id, school_id, student_number, grade_level, created_at, row_version)
        VALUES (v_student_user_id, v_school_id, 'S2024001', 10, NOW(), 1)
        RETURNING student_id INTO v_student_id;
        
        RAISE NOTICE 'Student user created (email: student@demo.schoolportal.com, password: Admin@123)';
    ELSE
        SELECT student_id INTO v_student_id FROM students WHERE school_id = v_school_id LIMIT 1;
    END IF;

    -- Insert Sample Subjects
    INSERT INTO subjects (school_id, name, code, created_at, row_version)
    SELECT v_school_id, 'Mathematics', 'MATH', NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE school_id = v_school_id AND code = 'MATH');

    INSERT INTO subjects (school_id, name, code, created_at, row_version)
    SELECT v_school_id, 'English', 'ENG', NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE school_id = v_school_id AND code = 'ENG');

    INSERT INTO subjects (school_id, name, code, created_at, row_version)
    SELECT v_school_id, 'Science', 'SCI', NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE school_id = v_school_id AND code = 'SCI');

    INSERT INTO subjects (school_id, name, code, created_at, row_version)
    SELECT v_school_id, 'History', 'HIST', NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM subjects WHERE school_id = v_school_id AND code = 'HIST');

    RAISE NOTICE 'Sample subjects created.';

    -- Insert Sample Class
    INSERT INTO classes (school_id, name, grade_level, academic_year, teacher_id, max_capacity, created_at, row_version)
    SELECT v_school_id, 'Grade 10A', 10, 2024, v_teacher_id, 30, NOW(), 1
    WHERE NOT EXISTS (SELECT 1 FROM classes WHERE school_id = v_school_id AND name = 'Grade 10A')
    RETURNING class_id INTO v_class_id;

    IF v_class_id IS NULL THEN
        SELECT class_id INTO v_class_id FROM classes WHERE school_id = v_school_id LIMIT 1;
    END IF;

    RAISE NOTICE 'Sample class created.';

    -- Enroll Student in Class
    INSERT INTO enrollments (class_id, student_id, school_id, enrolled_at, is_active, row_version)
    SELECT v_class_id, v_student_id, v_school_id, NOW(), true, 1
    WHERE v_class_id IS NOT NULL AND v_student_id IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM enrollments WHERE class_id = v_class_id AND student_id = v_student_id);

    RAISE NOTICE 'Student enrolled in class.';

    RAISE NOTICE '';
    RAISE NOTICE 'Seed data insertion completed successfully!';
    RAISE NOTICE '';
    RAISE NOTICE 'Login Credentials:';
    RAISE NOTICE '  Admin:   admin@demo.schoolportal.com / Admin@123';
    RAISE NOTICE '  Teacher: teacher@demo.schoolportal.com / Admin@123';
    RAISE NOTICE '  Student: student@demo.schoolportal.com / Admin@123';
END $$;
