-- Migration 008: Fix vw_matric_aps_summary APS weighting (Sprint 1.5.1 Gap 1)
-- Run this in the Supabase SQL Editor
--
-- The Sprint 1.5.0.5 definition summed CAPS-code points over ALL subjects (no Life
-- Orientation exclusion, no best-6), so projected_aps disagreed with the live
-- calculator (PathwaysService.GetLearnerApsAsync). Two surfaces giving a learner
-- different APS numbers is a correctness bug. After this migration the view matches
-- the C# semantics exactly:
--   * projected_aps = STANDARD APS: best 6 subjects excluding Life Orientation
--   * total_aps (new column) = all subjects, each LO subject capped at 4 points
--     (the C# caps per subject inside the sum — multiple LO subjects each cap at 4)
--   * outputs cast ::int (EF entity maps int; the old view produced bigint)
--
-- DUAL-VEHICLE NOTE: identical SQL lives in the EF migration FixMatricApsWeighting
-- (20260706070412) — the chain's source of truth, so fresh replays build the correct
-- view. This file exists to apply the fix to live immediately; the EF migration's
-- later DROP+CREATE on live is a harmless re-execution.

DROP MATERIALIZED VIEW IF EXISTS vw_matric_aps_summary;

CREATE MATERIALIZED VIEW vw_matric_aps_summary AS
WITH subject_year_avg AS (
  SELECT
    a.school_id,
    sub.student_id,
    t.academic_year_id,
    cs.subject_id,
    s.name AS subject_name,
    AVG(g.score / a.max_marks * 100.0) AS avg_percent
  FROM grades g
  JOIN submissions    sub ON sub.submission_id  = g.submission_id
  JOIN assignments    a   ON a.assignment_id     = sub.assignment_id
  JOIN class_subjects cs  ON cs.class_subject_id = a.class_subject_id
  JOIN subjects       s   ON s.subject_id        = cs.subject_id
  JOIN terms          t   ON t.school_id = a.school_id
                         AND a.due_at >= t.start_date AND a.due_at <= t.end_date
  JOIN students       st  ON st.student_id = sub.student_id
  WHERE a.max_marks > 0 AND st.grade_level = 12
  GROUP BY a.school_id, sub.student_id, t.academic_year_id, cs.subject_id, s.name
),
scored AS (
  SELECT *,
    CASE
      WHEN avg_percent >= 80 THEN 7  WHEN avg_percent >= 70 THEN 6
      WHEN avg_percent >= 60 THEN 5  WHEN avg_percent >= 50 THEN 4
      WHEN avg_percent >= 40 THEN 3  WHEN avg_percent >= 30 THEN 2
      ELSE 1
    END AS aps_points,
    subject_name ILIKE '%life orientation%' AS is_lo
  FROM subject_year_avg
),
ranked AS (
  SELECT *,
    ROW_NUMBER() OVER (
      PARTITION BY school_id, student_id, academic_year_id, is_lo
      ORDER BY aps_points DESC, avg_percent DESC, subject_id
    ) AS rank_in_group
  FROM scored
)
SELECT
  school_id,
  student_id,
  academic_year_id,
  COALESCE(SUM(aps_points) FILTER (WHERE NOT is_lo AND rank_in_group <= 6), 0)::int AS projected_aps,
  SUM(CASE WHEN is_lo THEN LEAST(aps_points, 4) ELSE aps_points END)::int AS total_aps,
  COUNT(*)::int AS subject_count
FROM ranked
GROUP BY school_id, student_id, academic_year_id
WITH NO DATA;

CREATE UNIQUE INDEX ux_vw_matric_aps_summary
  ON vw_matric_aps_summary (school_id, student_id, academic_year_id);

REFRESH MATERIALIZED VIEW vw_matric_aps_summary;
