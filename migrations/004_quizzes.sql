-- Migration 004: Quiz Engine tables
-- Run this in the Supabase SQL Editor

CREATE TABLE IF NOT EXISTS quizzes (
    quiz_id                 uuid        NOT NULL DEFAULT gen_random_uuid(),
    school_id               uuid        NOT NULL,
    class_subject_id        uuid,
    title                   varchar(200) NOT NULL,
    description             text,
    time_limit_minutes      integer,
    max_attempts            integer     NOT NULL DEFAULT 1,
    shuffle_questions       boolean     NOT NULL DEFAULT false,
    show_results_immediately boolean    NOT NULL DEFAULT true,
    is_published            boolean     NOT NULL DEFAULT false,
    created_by_user_id      uuid        NOT NULL,
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz,
    row_version             bigint      NOT NULL DEFAULT 1,
    CONSTRAINT pk_quizzes PRIMARY KEY (quiz_id),
    CONSTRAINT fk_quizzes_schools FOREIGN KEY (school_id) REFERENCES schools (school_id) ON DELETE RESTRICT,
    CONSTRAINT fk_quizzes_class_subjects FOREIGN KEY (class_subject_id) REFERENCES class_subjects (class_subject_id) ON DELETE SET NULL,
    CONSTRAINT fk_quizzes_users FOREIGN KEY (created_by_user_id) REFERENCES users (user_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_quizzes_school_id ON quizzes (school_id);

CREATE TABLE IF NOT EXISTS quiz_questions (
    question_id uuid        NOT NULL DEFAULT gen_random_uuid(),
    quiz_id     uuid        NOT NULL,
    text        text        NOT NULL,
    type        varchar(50) NOT NULL,
    "order"     integer     NOT NULL DEFAULT 0,
    marks       numeric(10,2) NOT NULL DEFAULT 1,
    explanation text,
    CONSTRAINT pk_quiz_questions PRIMARY KEY (question_id),
    CONSTRAINT fk_quiz_questions_quizzes FOREIGN KEY (quiz_id) REFERENCES quizzes (quiz_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_quiz_questions_quiz_id ON quiz_questions (quiz_id);

CREATE TABLE IF NOT EXISTS quiz_options (
    option_id   uuid        NOT NULL DEFAULT gen_random_uuid(),
    question_id uuid        NOT NULL,
    text        text        NOT NULL,
    is_correct  boolean     NOT NULL DEFAULT false,
    "order"     integer     NOT NULL DEFAULT 0,
    CONSTRAINT pk_quiz_options PRIMARY KEY (option_id),
    CONSTRAINT fk_quiz_options_questions FOREIGN KEY (question_id) REFERENCES quiz_questions (question_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS quiz_attempts (
    attempt_id  uuid        NOT NULL DEFAULT gen_random_uuid(),
    quiz_id     uuid        NOT NULL,
    student_id  uuid        NOT NULL,
    school_id   uuid        NOT NULL,
    started_at  timestamptz NOT NULL DEFAULT now(),
    submitted_at timestamptz,
    score       numeric(10,2),
    max_score   numeric(10,2),
    is_completed boolean    NOT NULL DEFAULT false,
    CONSTRAINT pk_quiz_attempts PRIMARY KEY (attempt_id),
    CONSTRAINT fk_quiz_attempts_quizzes FOREIGN KEY (quiz_id) REFERENCES quizzes (quiz_id) ON DELETE CASCADE,
    CONSTRAINT fk_quiz_attempts_students FOREIGN KEY (student_id) REFERENCES students (student_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_quiz_attempts_quiz_student ON quiz_attempts (quiz_id, student_id);

CREATE TABLE IF NOT EXISTS quiz_answers (
    answer_id           uuid    NOT NULL DEFAULT gen_random_uuid(),
    attempt_id          uuid    NOT NULL,
    question_id         uuid    NOT NULL,
    selected_option_id  uuid,
    text_answer         text,
    is_correct          boolean,
    marks_awarded       numeric(10,2),
    CONSTRAINT pk_quiz_answers PRIMARY KEY (answer_id),
    CONSTRAINT fk_quiz_answers_attempts FOREIGN KEY (attempt_id) REFERENCES quiz_attempts (attempt_id) ON DELETE CASCADE,
    CONSTRAINT fk_quiz_answers_questions FOREIGN KEY (question_id) REFERENCES quiz_questions (question_id) ON DELETE RESTRICT,
    CONSTRAINT fk_quiz_answers_options FOREIGN KEY (selected_option_id) REFERENCES quiz_options (option_id) ON DELETE RESTRICT
);
