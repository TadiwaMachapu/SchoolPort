using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <summary>
    /// Sprint 1.5.1 Gap 1 — vw_matric_aps_summary.projected_aps was a flat sum of CAPS-code points
    /// (no LO exclusion, no best-6), so it disagreed with the live calculator
    /// (PathwaysService.GetLearnerApsAsync). Redefined to match the C# semantics exactly:
    ///   projected_aps = STANDARD APS — best 6 subjects excluding Life Orientation
    ///   total_aps (new) = all subjects, each Life Orientation subject capped at 4 points
    /// Outputs cast ::int (the old view summed to bigint while the EF entity maps int — latent
    /// read bug, nothing queried it yet). Same SQL ships as migrations/008_fix_matric_aps_view.sql
    /// for immediate manual apply to live (dual-vehicle convention for matview changes).
    /// </summary>
    public partial class FixMatricApsWeighting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS vw_matric_aps_summary;");
            migrationBuilder.Sql(@"
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
WITH NO DATA;");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ux_vw_matric_aps_summary ON vw_matric_aps_summary (school_id, student_id, academic_year_id);");
            migrationBuilder.Sql(@"REFRESH MATERIALIZED VIEW vw_matric_aps_summary;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the Sprint 1.5.0.5 definition (flat sum, no total_aps).
            migrationBuilder.Sql(@"DROP MATERIALIZED VIEW IF EXISTS vw_matric_aps_summary;");
            migrationBuilder.Sql(@"
CREATE MATERIALIZED VIEW vw_matric_aps_summary AS
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
        }
    }
}
