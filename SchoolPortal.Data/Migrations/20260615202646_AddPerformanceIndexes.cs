using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <summary>
    /// Sprint 1.5.0.5 — purely-additive performance indexes for the hottest query paths, ahead of
    /// pilot-school data. Raw <c>CREATE INDEX IF NOT EXISTS</c> (idempotent, plain — not CONCURRENTLY,
    /// since EF wraps the migration in a transaction and the tables are near-empty pre-pilot). These
    /// are NOT modelled via EF <c>HasIndex</c> on purpose: they change no schema, so future scaffolds
    /// ignore them and won't try to drop them. The corrected 9-index set (the requested grades /
    /// submission-status / fee-status indexes reference columns that don't exist in the normalized
    /// schema — see CLAUDE.md "Deferred indexes — Sprint 1.5.4").
    /// </summary>
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Attendance — class-by-date capture/view, and a learner's own attendance over time.
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_attendances_school_class_date ON attendances (school_id, class_id, date);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_attendances_school_student_date ON attendances (school_id, student_id, date);");

            // Assignments — term-bounded class gradebook / My Academics fetch (term derived from due_at),
            // and a teacher's own authored assignments.
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_assignments_school_class_subject_due ON assignments (school_id, class_subject_id, due_at);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_assignments_school_created_by ON assignments (school_id, created_by_user_id);");

            // Audit logs — recent-activity feeds, school-wide and per-user (column is ""timestamp"").
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_audit_logs_school_timestamp ON audit_logs (school_id, ""timestamp"" DESC);");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_audit_logs_school_user_timestamp ON audit_logs (school_id, user_id, ""timestamp"" DESC);");

            // Fees — due-date driven reminders / listings.
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_fees_school_due_date ON fees (school_id, due_date);");

            // Payments — payment history per fee, newest first (fee_id is the real FK; no invoice model yet).
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_fee_payments_fee_created ON fee_payments (fee_id, created_at DESC);");

            // WhatsApp — outbound queue/status monitoring, newest first.
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ix_whatsapp_logs_school_status_created ON whatsapp_logs (school_id, status, created_at DESC);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_attendances_school_class_date;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_attendances_school_student_date;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_assignments_school_class_subject_due;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_assignments_school_created_by;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_audit_logs_school_timestamp;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_audit_logs_school_user_timestamp;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_fees_school_due_date;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_fee_payments_fee_created;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_whatsapp_logs_school_status_created;");
        }
    }
}
