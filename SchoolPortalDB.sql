-- ============================================================
-- SchoolPortal Database Setup Script for Supabase / PostgreSQL
-- Run this in the Supabase SQL Editor
-- ============================================================

-- Migration tracking table
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ============================================================
-- TABLES (in FK dependency order)
-- ============================================================

CREATE TABLE IF NOT EXISTS schools (
    school_id       uuid        NOT NULL DEFAULT gen_random_uuid(),
    name            varchar(200) NOT NULL,
    domain          varchar(100),
    branding_logo_url text,
    branding_primary_color text,
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_schools PRIMARY KEY (school_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_schools_domain ON schools (domain) WHERE domain IS NOT NULL;

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS users (
    user_id         uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id       uuid        NOT NULL,
    email           varchar(255) NOT NULL,
    password_hash   varchar(500) NOT NULL,
    first_name      varchar(100) NOT NULL,
    last_name       varchar(100) NOT NULL,
    role            varchar(50)  NOT NULL,
    is_active       boolean     NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    last_login_at   timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_users PRIMARY KEY (user_id),
    CONSTRAINT fk_users_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_users_email      ON users (email);
CREATE INDEX IF NOT EXISTS ix_users_school_id  ON users (school_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_school_id_email ON users (school_id, email);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS students (
    student_id      uuid        NOT NULL DEFAULT gen_random_uuid(),
    user_id         uuid        NOT NULL,
    school_id       uuid        NOT NULL,
    student_number  varchar(50) NOT NULL,
    grade_level     integer,
    date_of_birth   timestamptz,
    parent_user_id  uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_students PRIMARY KEY (student_id),
    CONSTRAINT fk_students_users   FOREIGN KEY (user_id)        REFERENCES users (user_id)  ON DELETE CASCADE,
    CONSTRAINT fk_students_schools FOREIGN KEY (school_id)      REFERENCES schools (school_id) ON DELETE RESTRICT,
    CONSTRAINT fk_students_parent  FOREIGN KEY (parent_user_id) REFERENCES users (user_id)  ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_students_user_id                ON students (user_id);
CREATE INDEX IF NOT EXISTS        ix_students_school_id              ON students (school_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_students_school_id_student_number ON students (school_id, student_number);
CREATE INDEX IF NOT EXISTS        ix_students_parent_user_id         ON students (parent_user_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS teachers (
    teacher_id      uuid        NOT NULL DEFAULT gen_random_uuid(),
    user_id         uuid        NOT NULL,
    school_id       uuid        NOT NULL,
    employee_number varchar(50),
    specialization  varchar(200),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_teachers PRIMARY KEY (teacher_id),
    CONSTRAINT fk_teachers_users   FOREIGN KEY (user_id)   REFERENCES users (user_id)   ON DELETE CASCADE,
    CONSTRAINT fk_teachers_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_teachers_user_id   ON teachers (user_id);
CREATE INDEX IF NOT EXISTS        ix_teachers_school_id ON teachers (school_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS subjects (
    subject_id      uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id       uuid        NOT NULL,
    name            varchar(100) NOT NULL,
    code            varchar(20),
    description     text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_subjects PRIMARY KEY (subject_id),
    CONSTRAINT fk_subjects_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS        ix_subjects_school_id      ON subjects (school_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_subjects_school_id_code ON subjects (school_id, code) WHERE code IS NOT NULL;

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS classes (
    class_id        uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id       uuid        NOT NULL,
    name            varchar(100) NOT NULL,
    grade_level     integer,
    academic_year   integer,
    teacher_id      uuid,
    max_capacity    integer,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_classes PRIMARY KEY (class_id),
    CONSTRAINT fk_classes_schools  FOREIGN KEY (school_id) REFERENCES schools (school_id)   ON DELETE RESTRICT,
    CONSTRAINT fk_classes_teachers FOREIGN KEY (teacher_id) REFERENCES teachers (teacher_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_classes_school_id  ON classes (school_id);
CREATE INDEX IF NOT EXISTS ix_classes_teacher_id ON classes (teacher_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS announcements (
    announcement_id     uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id           uuid        NOT NULL,
    title               varchar(200) NOT NULL,
    content             text        NOT NULL,
    audience            varchar(50) NOT NULL,
    audience_value      varchar(100),
    created_by_user_id  uuid        NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz,
    expires_at          timestamptz,
    is_active           boolean     NOT NULL DEFAULT true,
    row_version         bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_announcements PRIMARY KEY (announcement_id),
    CONSTRAINT fk_announcements_schools FOREIGN KEY (school_id)          REFERENCES schools (school_id) ON DELETE RESTRICT,
    CONSTRAINT fk_announcements_users   FOREIGN KEY (created_by_user_id) REFERENCES users (user_id)    ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_announcements_school_id          ON announcements (school_id);
CREATE INDEX IF NOT EXISTS ix_announcements_created_by_user_id ON announcements (created_by_user_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS class_subjects (
    class_subject_id    uuid        NOT NULL DEFAULT gen_random_uuid(),
    class_id            uuid        NOT NULL,
    subject_id          uuid        NOT NULL,
    teacher_id          uuid,
    school_id           uuid        NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    row_version         bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_class_subjects PRIMARY KEY (class_subject_id),
    CONSTRAINT fk_class_subjects_classes   FOREIGN KEY (class_id)   REFERENCES classes (class_id)     ON DELETE CASCADE,
    CONSTRAINT fk_class_subjects_subjects  FOREIGN KEY (subject_id) REFERENCES subjects (subject_id)  ON DELETE RESTRICT,
    CONSTRAINT fk_class_subjects_teachers  FOREIGN KEY (teacher_id) REFERENCES teachers (teacher_id)  ON DELETE SET NULL,
    CONSTRAINT fk_class_subjects_schools   FOREIGN KEY (school_id)  REFERENCES schools (school_id)    ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_class_subjects_class_id_subject_id ON class_subjects (class_id, subject_id);
CREATE INDEX IF NOT EXISTS        ix_class_subjects_school_id            ON class_subjects (school_id);
CREATE INDEX IF NOT EXISTS        ix_class_subjects_class_id             ON class_subjects (class_id);
CREATE INDEX IF NOT EXISTS        ix_class_subjects_subject_id           ON class_subjects (subject_id);
CREATE INDEX IF NOT EXISTS        ix_class_subjects_teacher_id           ON class_subjects (teacher_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS enrollments (
    enrollment_id   uuid        NOT NULL DEFAULT gen_random_uuid(),
    class_id        uuid        NOT NULL,
    student_id      uuid        NOT NULL,
    school_id       uuid        NOT NULL,
    enrolled_at     timestamptz NOT NULL DEFAULT now(),
    dropped_at      timestamptz,
    is_active       boolean     NOT NULL DEFAULT true,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_enrollments PRIMARY KEY (enrollment_id),
    CONSTRAINT fk_enrollments_classes  FOREIGN KEY (class_id)  REFERENCES classes (class_id)   ON DELETE CASCADE,
    CONSTRAINT fk_enrollments_students FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT,
    CONSTRAINT fk_enrollments_schools  FOREIGN KEY (school_id) REFERENCES schools (school_id)  ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_enrollments_class_id_student_id ON enrollments (class_id, student_id);
CREATE INDEX IF NOT EXISTS        ix_enrollments_school_id           ON enrollments (school_id);
CREATE INDEX IF NOT EXISTS        ix_enrollments_class_id            ON enrollments (class_id);
CREATE INDEX IF NOT EXISTS        ix_enrollments_student_id          ON enrollments (student_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS assignments (
    assignment_id       uuid        NOT NULL DEFAULT gen_random_uuid(),
    class_subject_id    uuid        NOT NULL,
    school_id           uuid        NOT NULL,
    title               varchar(200) NOT NULL,
    description         text,
    due_at              timestamptz NOT NULL,
    max_marks           numeric(10,2) NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz,
    created_by_user_id  uuid        NOT NULL,
    row_version         bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_assignments PRIMARY KEY (assignment_id),
    CONSTRAINT fk_assignments_class_subjects FOREIGN KEY (class_subject_id)  REFERENCES class_subjects (class_subject_id) ON DELETE CASCADE,
    CONSTRAINT fk_assignments_schools        FOREIGN KEY (school_id)          REFERENCES schools (school_id)              ON DELETE RESTRICT,
    CONSTRAINT fk_assignments_users          FOREIGN KEY (created_by_user_id) REFERENCES users (user_id)                 ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_assignments_school_id          ON assignments (school_id);
CREATE INDEX IF NOT EXISTS ix_assignments_class_subject_id   ON assignments (class_subject_id);
CREATE INDEX IF NOT EXISTS ix_assignments_created_by_user_id ON assignments (created_by_user_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS submissions (
    submission_id   uuid        NOT NULL DEFAULT gen_random_uuid(),
    assignment_id   uuid        NOT NULL,
    student_id      uuid        NOT NULL,
    school_id       uuid        NOT NULL,
    submitted_at    timestamptz NOT NULL DEFAULT now(),
    file_url        varchar(500),
    file_name       varchar(255),
    comments        text,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_submissions PRIMARY KEY (submission_id),
    CONSTRAINT fk_submissions_assignments FOREIGN KEY (assignment_id) REFERENCES assignments (assignment_id) ON DELETE CASCADE,
    CONSTRAINT fk_submissions_students    FOREIGN KEY (student_id)    REFERENCES students (student_id)    ON DELETE RESTRICT,
    CONSTRAINT fk_submissions_schools     FOREIGN KEY (school_id)     REFERENCES schools (school_id)     ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_submissions_assignment_id_student_id ON submissions (assignment_id, student_id);
CREATE INDEX IF NOT EXISTS        ix_submissions_school_id                ON submissions (school_id);
CREATE INDEX IF NOT EXISTS        ix_submissions_assignment_id            ON submissions (assignment_id);
CREATE INDEX IF NOT EXISTS        ix_submissions_student_id               ON submissions (student_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS grades (
    grade_id            uuid        NOT NULL DEFAULT gen_random_uuid(),
    submission_id       uuid        NOT NULL,
    school_id           uuid        NOT NULL,
    score               numeric(10,2) NOT NULL,
    feedback            text,
    graded_by_user_id   uuid        NOT NULL,
    graded_at           timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz,
    row_version         bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_grades PRIMARY KEY (grade_id),
    CONSTRAINT fk_grades_submissions FOREIGN KEY (submission_id)     REFERENCES submissions (submission_id) ON DELETE CASCADE,
    CONSTRAINT fk_grades_schools     FOREIGN KEY (school_id)          REFERENCES schools (school_id)        ON DELETE RESTRICT,
    CONSTRAINT fk_grades_users       FOREIGN KEY (graded_by_user_id) REFERENCES users (user_id)            ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_grades_submission_id    ON grades (submission_id);
CREATE INDEX IF NOT EXISTS        ix_grades_school_id        ON grades (school_id);
CREATE INDEX IF NOT EXISTS        ix_grades_graded_by_user_id ON grades (graded_by_user_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS attendances (
    attendance_id   uuid        NOT NULL DEFAULT gen_random_uuid(),
    class_id        uuid        NOT NULL,
    student_id      uuid        NOT NULL,
    school_id       uuid        NOT NULL,
    date            timestamptz NOT NULL,
    status          integer     NOT NULL, -- 0=Absent, 1=Present, 2=Late
    notes           text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz,
    row_version     bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_attendances PRIMARY KEY (attendance_id),
    CONSTRAINT fk_attendances_classes  FOREIGN KEY (class_id)  REFERENCES classes (class_id)   ON DELETE CASCADE,
    CONSTRAINT fk_attendances_students FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendances_schools  FOREIGN KEY (school_id) REFERENCES schools (school_id)  ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_attendances_school_class_student_date ON attendances (school_id, class_id, student_id, date);
CREATE INDEX IF NOT EXISTS        ix_attendances_school_id                  ON attendances (school_id);
CREATE INDEX IF NOT EXISTS        ix_attendances_class_id                   ON attendances (class_id);
CREATE INDEX IF NOT EXISTS        ix_attendances_student_id                 ON attendances (student_id);

-- ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS audit_logs (
    audit_log_id    bigint      NOT NULL GENERATED BY DEFAULT AS IDENTITY,
    school_id       uuid,
    user_id         uuid,
    action          varchar(50) NOT NULL,
    entity_type     varchar(100) NOT NULL,
    entity_id       text,
    old_values      text,
    new_values      text,
    ip_address      varchar(50),
    timestamp       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_logs PRIMARY KEY (audit_log_id)
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_timestamp        ON audit_logs (timestamp);
CREATE INDEX IF NOT EXISTS ix_audit_logs_school_id_user_id ON audit_logs (school_id, user_id);

-- ============================================================
-- VIEWS
-- ============================================================

DROP VIEW IF EXISTS vw_attendance_summary;
CREATE VIEW vw_attendance_summary AS
SELECT
    a.school_id,
    a.class_id,
    c.name                                              AS class_name,
    a.student_id,
    u.first_name || ' ' || u.last_name                 AS student_name,
    s.student_number,
    EXTRACT(YEAR  FROM a.date)::integer                 AS year,
    EXTRACT(MONTH FROM a.date)::integer                 AS month,
    SUM(CASE WHEN a.status = 1 THEN 1 ELSE 0 END)::integer AS present_count,
    SUM(CASE WHEN a.status = 0 THEN 1 ELSE 0 END)::integer AS absent_count,
    SUM(CASE WHEN a.status = 2 THEN 1 ELSE 0 END)::integer AS late_count,
    COUNT(*)::integer                                   AS total_days
FROM attendances a
INNER JOIN students s ON a.student_id = s.student_id
INNER JOIN users    u ON s.user_id    = u.user_id
INNER JOIN classes  c ON a.class_id   = c.class_id
GROUP BY
    a.school_id, a.class_id, c.name,
    a.student_id, u.first_name, u.last_name, s.student_number,
    EXTRACT(YEAR FROM a.date), EXTRACT(MONTH FROM a.date);

DROP VIEW IF EXISTS vw_gradebook_simple;
CREATE VIEW vw_gradebook_simple AS
SELECT
    g.school_id,
    cs.class_id,
    c.name                                             AS class_name,
    sub.name                                           AS subject_name,
    a.assignment_id,
    a.title                                            AS assignment_title,
    a.max_marks,
    st.student_id,
    u.first_name || ' ' || u.last_name                AS student_name,
    st.student_number,
    g.score,
    g.feedback,
    g.graded_at,
    ROUND((g.score / a.max_marks * 100)::numeric, 2)  AS percentage
FROM grades         g
INNER JOIN submissions  sr  ON g.submission_id    = sr.submission_id
INNER JOIN students     st  ON sr.student_id      = st.student_id
INNER JOIN users        u   ON st.user_id         = u.user_id
INNER JOIN assignments  a   ON sr.assignment_id   = a.assignment_id
INNER JOIN class_subjects cs ON a.class_subject_id = cs.class_subject_id
INNER JOIN classes      c   ON cs.class_id        = c.class_id
INNER JOIN subjects     sub ON cs.subject_id      = sub.subject_id;

-- ============================================================
-- SEED DATA
-- ============================================================

DO $$
DECLARE
    v_school_id     uuid;
    v_admin_id      uuid;
    v_teacher_user_id uuid;
    v_student_user_id uuid;
    v_teacher_id    uuid;
    v_student_id    uuid;
    v_class_id      uuid;
    v_math_id       uuid;
    v_eng_id        uuid;
    v_sci_id        uuid;
    v_hist_id       uuid;
    v_cs_id         uuid;
BEGIN
    -- School
    INSERT INTO schools (name, domain, branding_primary_color, is_active, created_at)
    VALUES ('Demo High School', 'demo.schoolportal.com', '#1E40AF', true, now())
    ON CONFLICT DO NOTHING
    RETURNING school_id INTO v_school_id;

    IF v_school_id IS NULL THEN
        SELECT school_id INTO v_school_id FROM schools WHERE domain = 'demo.schoolportal.com';
    END IF;

    -- Admin user  (password: Admin@123)
    INSERT INTO users (school_id, email, password_hash, first_name, last_name, role, is_active, created_at)
    VALUES (v_school_id, 'admin@demo.schoolportal.com',
            '$2a$11$k9j/L2NaoEQfrhbDKyHM6O3SK3UoMX7RiPTaprJYoSD/tFzq03Rf.',
            'System', 'Administrator', 'Admin', true, now())
    ON CONFLICT (school_id, email) DO NOTHING
    RETURNING user_id INTO v_admin_id;

    IF v_admin_id IS NULL THEN
        SELECT user_id INTO v_admin_id FROM users WHERE email = 'admin@demo.schoolportal.com';
    END IF;

    -- Teacher user  (password: Admin@123)
    INSERT INTO users (school_id, email, password_hash, first_name, last_name, role, is_active, created_at)
    VALUES (v_school_id, 'teacher@demo.schoolportal.com',
            '$2a$11$k9j/L2NaoEQfrhbDKyHM6O3SK3UoMX7RiPTaprJYoSD/tFzq03Rf.',
            'John', 'Smith', 'Teacher', true, now())
    ON CONFLICT (school_id, email) DO NOTHING
    RETURNING user_id INTO v_teacher_user_id;

    IF v_teacher_user_id IS NOT NULL THEN
        INSERT INTO teachers (user_id, school_id, employee_number, specialization, created_at)
        VALUES (v_teacher_user_id, v_school_id, 'T001', 'Mathematics', now())
        RETURNING teacher_id INTO v_teacher_id;
    ELSE
        SELECT u.user_id INTO v_teacher_user_id FROM users u WHERE u.email = 'teacher@demo.schoolportal.com';
        SELECT t.teacher_id INTO v_teacher_id FROM teachers t WHERE t.user_id = v_teacher_user_id;
    END IF;

    -- Student user  (password: Admin@123)
    INSERT INTO users (school_id, email, password_hash, first_name, last_name, role, is_active, created_at)
    VALUES (v_school_id, 'student@demo.schoolportal.com',
            '$2a$11$k9j/L2NaoEQfrhbDKyHM6O3SK3UoMX7RiPTaprJYoSD/tFzq03Rf.',
            'Jane', 'Doe', 'Student', true, now())
    ON CONFLICT (school_id, email) DO NOTHING
    RETURNING user_id INTO v_student_user_id;

    IF v_student_user_id IS NOT NULL THEN
        INSERT INTO students (user_id, school_id, student_number, grade_level, created_at)
        VALUES (v_student_user_id, v_school_id, 'S2024001', 10, now())
        RETURNING student_id INTO v_student_id;
    ELSE
        SELECT u.user_id INTO v_student_user_id FROM users u WHERE u.email = 'student@demo.schoolportal.com';
        SELECT s.student_id INTO v_student_id FROM students s WHERE s.user_id = v_student_user_id;
    END IF;

    -- Subjects
    INSERT INTO subjects (school_id, name, code, created_at) VALUES (v_school_id, 'Mathematics', 'MATH', now()) ON CONFLICT DO NOTHING RETURNING subject_id INTO v_math_id;
    INSERT INTO subjects (school_id, name, code, created_at) VALUES (v_school_id, 'English',     'ENG',  now()) ON CONFLICT DO NOTHING RETURNING subject_id INTO v_eng_id;
    INSERT INTO subjects (school_id, name, code, created_at) VALUES (v_school_id, 'Science',     'SCI',  now()) ON CONFLICT DO NOTHING RETURNING subject_id INTO v_sci_id;
    INSERT INTO subjects (school_id, name, code, created_at) VALUES (v_school_id, 'History',     'HIST', now()) ON CONFLICT DO NOTHING RETURNING subject_id INTO v_hist_id;

    IF v_math_id IS NULL THEN SELECT subject_id INTO v_math_id FROM subjects WHERE school_id = v_school_id AND code = 'MATH'; END IF;
    IF v_eng_id  IS NULL THEN SELECT subject_id INTO v_eng_id  FROM subjects WHERE school_id = v_school_id AND code = 'ENG';  END IF;

    -- Class
    INSERT INTO classes (school_id, name, grade_level, academic_year, teacher_id, max_capacity, created_at)
    VALUES (v_school_id, 'Grade 10A', 10, 2024, v_teacher_id, 30, now())
    ON CONFLICT DO NOTHING
    RETURNING class_id INTO v_class_id;

    IF v_class_id IS NULL THEN
        SELECT class_id INTO v_class_id FROM classes WHERE school_id = v_school_id AND name = 'Grade 10A';
    END IF;

    -- Class-Subject links
    INSERT INTO class_subjects (class_id, subject_id, teacher_id, school_id, created_at)
    VALUES (v_class_id, v_math_id, v_teacher_id, v_school_id, now())
    ON CONFLICT DO NOTHING
    RETURNING class_subject_id INTO v_cs_id;

    INSERT INTO class_subjects (class_id, subject_id, teacher_id, school_id, created_at)
    VALUES (v_class_id, v_eng_id, v_teacher_id, v_school_id, now())
    ON CONFLICT DO NOTHING;

    -- Enrollment
    INSERT INTO enrollments (class_id, student_id, school_id, enrolled_at, is_active)
    VALUES (v_class_id, v_student_id, v_school_id, now(), true)
    ON CONFLICT DO NOTHING;

    -- EF migrations history record
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260521090406_InitialCreate', '8.0.4')
    ON CONFLICT DO NOTHING;

    RAISE NOTICE '';
    RAISE NOTICE '=== SchoolPortal DB setup complete ===';
    RAISE NOTICE 'School ID: %', v_school_id;
    RAISE NOTICE '';
    RAISE NOTICE 'Login credentials (all passwords: Admin@123)';
    RAISE NOTICE '  Admin:   admin@demo.schoolportal.com';
    RAISE NOTICE '  Teacher: teacher@demo.schoolportal.com';
    RAISE NOTICE '  Student: student@demo.schoolportal.com';
END $$;
