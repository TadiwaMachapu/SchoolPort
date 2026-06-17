-- Migration 005: Phase 2 Features
-- Run this in the Supabase SQL Editor
-- NOTE: courses_lms, lessons, quizzes (etc) were created in 003/004 - those are skipped here.

CREATE TABLE IF NOT EXISTS grade_categories (
    category_id      uuid         NOT NULL DEFAULT gen_random_uuid(),
    class_subject_id uuid         NOT NULL,
    name             varchar(100) NOT NULL,
    weight           numeric(5,2) NOT NULL,
    created_at       timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_grade_categories PRIMARY KEY (category_id),
    CONSTRAINT fk_grade_categories_cs FOREIGN KEY (class_subject_id)
        REFERENCES class_subjects (class_subject_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_grade_categories_class_subject_id ON grade_categories (class_subject_id);

CREATE TABLE IF NOT EXISTS calendar_events (
    event_id           uuid         NOT NULL DEFAULT gen_random_uuid(),
    school_id          uuid         NOT NULL,
    title              varchar(200) NOT NULL,
    description        text,
    type               varchar(50)  NOT NULL,
    start_at           timestamptz  NOT NULL,
    end_at             timestamptz,
    all_day            boolean      NOT NULL DEFAULT false,
    class_id           uuid,
    created_by_user_id uuid,
    created_at         timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_calendar_events PRIMARY KEY (event_id),
    CONSTRAINT fk_calendar_events_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT,
    CONSTRAINT fk_calendar_events_classes FOREIGN KEY (class_id)  REFERENCES classes (class_id)  ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_calendar_events_school_id ON calendar_events (school_id);
CREATE INDEX IF NOT EXISTS ix_calendar_events_start_at  ON calendar_events (start_at);
CREATE INDEX IF NOT EXISTS ix_calendar_events_class_id  ON calendar_events (class_id);

CREATE TABLE IF NOT EXISTS timetable_slots (
    slot_id          uuid         NOT NULL DEFAULT gen_random_uuid(),
    school_id        uuid         NOT NULL,
    class_subject_id uuid         NOT NULL,
    day_of_week      integer      NOT NULL,
    start_time       time         NOT NULL,
    end_time         time         NOT NULL,
    room             varchar(100),
    CONSTRAINT pk_timetable_slots PRIMARY KEY (slot_id),
    CONSTRAINT fk_timetable_slots_cs FOREIGN KEY (class_subject_id)
        REFERENCES class_subjects (class_subject_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_timetable_slots_school_id ON timetable_slots (school_id);

CREATE TABLE IF NOT EXISTS message_threads (
    thread_id       uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id       uuid        NOT NULL,
    subject         text,
    type            varchar(50) NOT NULL,
    class_id        uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    last_message_at timestamptz,
    CONSTRAINT pk_message_threads PRIMARY KEY (thread_id),
    CONSTRAINT fk_message_threads_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT,
    CONSTRAINT fk_message_threads_classes FOREIGN KEY (class_id)  REFERENCES classes (class_id)  ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_message_threads_school_id ON message_threads (school_id);
CREATE INDEX IF NOT EXISTS ix_message_threads_class_id  ON message_threads (class_id);

CREATE TABLE IF NOT EXISTS thread_participants (
    participant_id uuid        NOT NULL DEFAULT gen_random_uuid(),
    thread_id      uuid        NOT NULL,
    user_id        uuid        NOT NULL,
    joined_at      timestamptz NOT NULL DEFAULT now(),
    last_read_at   timestamptz,
    CONSTRAINT pk_thread_participants PRIMARY KEY (participant_id),
    CONSTRAINT fk_thread_participants_threads FOREIGN KEY (thread_id) REFERENCES message_threads (thread_id) ON DELETE CASCADE,
    CONSTRAINT fk_thread_participants_users   FOREIGN KEY (user_id)   REFERENCES users (user_id)             ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_thread_participants_thread_user ON thread_participants (thread_id, user_id);

CREATE TABLE IF NOT EXISTS chat_messages (
    message_id     uuid        NOT NULL DEFAULT gen_random_uuid(),
    thread_id      uuid        NOT NULL,
    sender_user_id uuid        NOT NULL,
    content        text        NOT NULL,
    sent_at        timestamptz NOT NULL DEFAULT now(),
    is_deleted     boolean     NOT NULL DEFAULT false,
    CONSTRAINT pk_chat_messages PRIMARY KEY (message_id),
    CONSTRAINT fk_chat_messages_threads FOREIGN KEY (thread_id)      REFERENCES message_threads (thread_id) ON DELETE CASCADE,
    CONSTRAINT fk_chat_messages_users   FOREIGN KEY (sender_user_id) REFERENCES users (user_id)             ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_chat_messages_thread_id ON chat_messages (thread_id);

CREATE TABLE IF NOT EXISTS lesson_progress (
    progress_id        uuid        NOT NULL DEFAULT gen_random_uuid(),
    lesson_id          uuid        NOT NULL,
    student_id         uuid        NOT NULL,
    school_id          uuid        NOT NULL,
    is_completed       boolean     NOT NULL DEFAULT false,
    completed_at       timestamptz,
    time_spent_seconds integer,
    last_accessed_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_lesson_progress PRIMARY KEY (progress_id),
    CONSTRAINT fk_lesson_progress_lessons  FOREIGN KEY (lesson_id)  REFERENCES lessons  (lesson_id)  ON DELETE CASCADE,
    CONSTRAINT fk_lesson_progress_students FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_lesson_progress_lesson_student ON lesson_progress (lesson_id, student_id);
CREATE INDEX IF NOT EXISTS        ix_lesson_progress_school_id      ON lesson_progress (school_id);
CREATE INDEX IF NOT EXISTS        ix_lesson_progress_student_id     ON lesson_progress (student_id);

CREATE TABLE IF NOT EXISTS learning_paths (
    path_id      uuid         NOT NULL DEFAULT gen_random_uuid(),
    school_id    uuid         NOT NULL,
    title        varchar(200) NOT NULL,
    description  text,
    is_published boolean      NOT NULL DEFAULT false,
    created_at   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_learning_paths PRIMARY KEY (path_id),
    CONSTRAINT fk_learning_paths_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_learning_paths_school_id ON learning_paths (school_id);

CREATE TABLE IF NOT EXISTS learning_path_courses (
    path_course_id         uuid    NOT NULL DEFAULT gen_random_uuid(),
    path_id                uuid    NOT NULL,
    course_id              uuid    NOT NULL,
    "order"                integer NOT NULL DEFAULT 0,
    prerequisite_course_id uuid,
    CONSTRAINT pk_learning_path_courses PRIMARY KEY (path_course_id),
    CONSTRAINT fk_lpc_paths   FOREIGN KEY (path_id)   REFERENCES learning_paths (path_id)   ON DELETE CASCADE,
    CONSTRAINT fk_lpc_courses FOREIGN KEY (course_id) REFERENCES courses_lms   (course_id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_learning_path_courses_path_id   ON learning_path_courses (path_id);
CREATE INDEX IF NOT EXISTS ix_learning_path_courses_course_id ON learning_path_courses (course_id);

CREATE TABLE IF NOT EXISTS subscriptions (
    subscription_id        uuid         NOT NULL DEFAULT gen_random_uuid(),
    school_id              uuid         NOT NULL,
    plan                   varchar(50)  NOT NULL,
    status                 varchar(50)  NOT NULL,
    stripe_customer_id     varchar(100),
    stripe_subscription_id varchar(100),
    trial_ends_at          timestamptz,
    current_period_end     timestamptz,
    created_at             timestamptz  NOT NULL DEFAULT now(),
    updated_at             timestamptz,
    CONSTRAINT pk_subscriptions PRIMARY KEY (subscription_id),
    CONSTRAINT fk_subscriptions_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_subscriptions_school_id              ON subscriptions (school_id);
CREATE INDEX IF NOT EXISTS        ix_subscriptions_stripe_subscription_id ON subscriptions (stripe_subscription_id);

CREATE TABLE IF NOT EXISTS plugins (
    plugin_id       uuid         NOT NULL DEFAULT gen_random_uuid(),
    name            varchar(100) NOT NULL,
    description     text,
    icon_url        text,
    webhook_url     text,
    iframe_url      text,
    developer_name  varchar(100) NOT NULL,
    developer_email varchar(255) NOT NULL,
    is_approved     boolean      NOT NULL DEFAULT false,
    is_public       boolean      NOT NULL DEFAULT false,
    permissions     text         NOT NULL DEFAULT '[]',
    created_at      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_plugins PRIMARY KEY (plugin_id)
);

CREATE TABLE IF NOT EXISTS plugin_installations (
    installation_id uuid        NOT NULL DEFAULT gen_random_uuid(),
    plugin_id       uuid        NOT NULL,
    school_id       uuid        NOT NULL,
    configuration   text,
    is_active       boolean     NOT NULL DEFAULT true,
    installed_at    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_plugin_installations PRIMARY KEY (installation_id),
    CONSTRAINT fk_plugin_inst_plugins FOREIGN KEY (plugin_id) REFERENCES plugins (plugin_id) ON DELETE CASCADE,
    CONSTRAINT fk_plugin_inst_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_plugin_installations_plugin_school ON plugin_installations (plugin_id, school_id);
CREATE INDEX IF NOT EXISTS        ix_plugin_installations_school_id     ON plugin_installations (school_id);

-- Mark migration as applied in EF Core history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260521000000_Phase2Features', '8.0.4')
ON CONFLICT DO NOTHING;
