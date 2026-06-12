-- Migration 003: Course / LMS tables
-- Run this in the Supabase SQL Editor

CREATE TABLE IF NOT EXISTS courses_lms (
    course_id           uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id           uuid        NOT NULL,
    class_subject_id    uuid,
    title               varchar(200) NOT NULL,
    description         text,
    thumbnail_url       text,
    is_published        boolean     NOT NULL DEFAULT false,
    created_by_user_id  uuid        NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz,
    row_version         bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_courses_lms PRIMARY KEY (course_id),
    CONSTRAINT fk_courses_lms_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT,
    CONSTRAINT fk_courses_lms_class_subjects FOREIGN KEY (class_subject_id) REFERENCES class_subjects (class_subject_id) ON DELETE SET NULL,
    CONSTRAINT fk_courses_lms_users FOREIGN KEY (created_by_user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_courses_lms_school_id ON courses_lms (school_id);
CREATE INDEX IF NOT EXISTS ix_courses_lms_class_subject_id ON courses_lms (class_subject_id);

CREATE TABLE IF NOT EXISTS course_modules (
    module_id   uuid        NOT NULL DEFAULT gen_random_uuid(),
    course_id   uuid        NOT NULL,
    title       varchar(200) NOT NULL,
    description text,
    "order"     integer     NOT NULL DEFAULT 0,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz,
    CONSTRAINT pk_course_modules PRIMARY KEY (module_id),
    CONSTRAINT fk_course_modules_courses FOREIGN KEY (course_id) REFERENCES courses_lms (course_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_course_modules_course_id ON course_modules (course_id);

CREATE TABLE IF NOT EXISTS lessons (
    lesson_id           uuid        NOT NULL DEFAULT gen_random_uuid(),
    module_id           uuid        NOT NULL,
    title               varchar(200) NOT NULL,
    type                varchar(50) NOT NULL,  -- RichText, Video, PDF, Link
    content             text,
    video_url           text,
    file_url            text,
    external_url        text,
    "order"             integer     NOT NULL DEFAULT 0,
    duration_minutes    integer,
    is_published        boolean     NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz,
    CONSTRAINT pk_lessons PRIMARY KEY (lesson_id),
    CONSTRAINT fk_lessons_modules FOREIGN KEY (module_id) REFERENCES course_modules (module_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_lessons_module_id ON lessons (module_id);
