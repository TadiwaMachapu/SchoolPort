using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <summary>
    /// Sprint 1.5.0.5 — three materialized views over the normalized marks path
    /// (grade → submission → assignment → class_subject → subject; term via assignment.due_at ∈
    /// [term.start_date, term.end_date]). Created WITH NO DATA, given a UNIQUE index (so the admin
    /// refresh-views endpoint can REFRESH … CONCURRENTLY), then populated by an initial REFRESH.
    /// Refreshed manually only — never on grade save (bulk capture would thrash full refreshes).
    /// </summary>
    public partial class AddMaterializedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. vw_subject_term_averages — per-learner / per-subject / per-term assignment average.
            migrationBuilder.Sql(@"
CREATE MATERIALIZED VIEW IF NOT EXISTS vw_subject_term_averages AS
SELECT
  a.school_id,
  sub.student_id,
  cs.subject_id,
  s.name AS subject_name,
  t.term_id,
  t.term_number,
  ROUND(AVG(g.score / a.max_marks * 100.0), 2) AS average_percent,
  COUNT(*) AS tasks_assessed
FROM grades g
JOIN submissions    sub ON sub.submission_id  = g.submission_id
JOIN assignments    a   ON a.assignment_id     = sub.assignment_id
JOIN class_subjects cs  ON cs.class_subject_id = a.class_subject_id
JOIN subjects       s   ON s.subject_id        = cs.subject_id
JOIN terms          t   ON t.school_id = a.school_id AND a.due_at >= t.start_date AND a.due_at <= t.end_date
WHERE a.max_marks > 0
GROUP BY a.school_id, sub.student_id, cs.subject_id, s.name, t.term_id, t.term_number
WITH NO DATA;");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ux_vw_subject_term_averages ON vw_subject_term_averages (school_id, student_id, subject_id, term_id);");
            migrationBuilder.Sql(@"REFRESH MATERIALIZED VIEW vw_subject_term_averages;");

            // 2. vw_matric_aps_summary — PROJECTED APS per Grade-12 learner per academic year.
            //    projected_aps = sum of CAPS-code points from each subject's year-average (simplified;
            //    NOT an official APS — no LO exclusion / best-6 / promotion-mark weighting yet).
            migrationBuilder.Sql(@"
CREATE MATERIALIZED VIEW IF NOT EXISTS vw_matric_aps_summary AS
WITH subject_year_avg AS (
  SELECT
    a.school_id,
    sub.student_id,
    t.academic_year_id,
    cs.subject_id,
    AVG(g.score / a.max_marks * 100.0) AS avg_percent
  FROM grades g
  JOIN submissions    sub ON sub.submission_id  = g.submission_id
  JOIN assignments    a   ON a.assignment_id     = sub.assignment_id
  JOIN class_subjects cs  ON cs.class_subject_id = a.class_subject_id
  JOIN terms          t   ON t.school_id = a.school_id AND a.due_at >= t.start_date AND a.due_at <= t.end_date
  JOIN students       st  ON st.student_id = sub.student_id
  WHERE a.max_marks > 0 AND st.grade_level = 12
  GROUP BY a.school_id, sub.student_id, t.academic_year_id, cs.subject_id
)
SELECT
  school_id,
  student_id,
  academic_year_id,
  SUM(
    CASE
      WHEN avg_percent >= 80 THEN 7  WHEN avg_percent >= 70 THEN 6
      WHEN avg_percent >= 60 THEN 5  WHEN avg_percent >= 50 THEN 4
      WHEN avg_percent >= 40 THEN 3  WHEN avg_percent >= 30 THEN 2
      ELSE 1
    END
  ) AS projected_aps,
  COUNT(*) AS subject_count
FROM subject_year_avg
GROUP BY school_id, student_id, academic_year_id
WITH NO DATA;");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ux_vw_matric_aps_summary ON vw_matric_aps_summary (school_id, student_id, academic_year_id);");
            migrationBuilder.Sql(@"REFRESH MATERIALIZED VIEW vw_matric_aps_summary;");

            // 3. vw_school_performance_summary — per-school / per-term / per-subject stats.
            //    Built independently from base tables (own refresh cadence). Pass/at-risk = 40% (CAPS min).
            migrationBuilder.Sql(@"
CREATE MATERIALIZED VIEW IF NOT EXISTS vw_school_performance_summary AS
WITH learner_subject_term AS (
  SELECT
    a.school_id, sub.student_id, cs.subject_id, s.name AS subject_name,
    t.term_id, t.term_number,
    AVG(g.score / a.max_marks * 100.0) AS avg_percent
  FROM grades g
  JOIN submissions    sub ON sub.submission_id  = g.submission_id
  JOIN assignments    a   ON a.assignment_id     = sub.assignment_id
  JOIN class_subjects cs  ON cs.class_subject_id = a.class_subject_id
  JOIN subjects       s   ON s.subject_id        = cs.subject_id
  JOIN terms          t   ON t.school_id = a.school_id AND a.due_at >= t.start_date AND a.due_at <= t.end_date
  WHERE a.max_marks > 0
  GROUP BY a.school_id, sub.student_id, cs.subject_id, s.name, t.term_id, t.term_number
)
SELECT
  school_id, term_id, term_number, subject_id, subject_name,
  ROUND(AVG(avg_percent), 2) AS subject_average,
  COUNT(*) AS learner_count,
  COUNT(*) FILTER (WHERE avg_percent < 40) AS at_risk_count,
  ROUND(100.0 * COUNT(*) FILTER (WHERE avg_percent >= 40) / NULLIF(COUNT(*), 0), 2) AS pass_rate
FROM learner_subject_term
GROUP BY school_id, term_id, term_number, subject_id, subject_name
WITH NO DATA;");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ux_vw_school_performance_summary ON vw_school_performance_summary (school_id, term_id, subject_id);");
            migrationBuilder.Sql(@"REFRESH MATERIALIZED VIEW vw_school_performance_summary;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS vw_school_performance_summary;");
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS vw_matric_aps_summary;");
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS vw_subject_term_averages;");
        }
    }
}
