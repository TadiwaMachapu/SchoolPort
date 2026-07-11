using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarksCaptureSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "submission_id",
                table: "grades",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                table: "grades",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            // HAND-EDITED (do not regenerate blindly): the scaffold added student_id/assignment_id
            // as NOT NULL DEFAULT all-zeros, which would violate the FKs added below on any DB
            // with existing grades. Correct sequence: add nullable → backfill from submissions
            // (total: every pre-1.5.2.5 grade has a submission, and submissions carry a unique
            // (assignment_id, student_id) index so the backfill cannot collide) → SET NOT NULL.
            migrationBuilder.AddColumn<Guid>(
                name: "assignment_id",
                table: "grades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_absent",
                table: "grades",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "student_id",
                table: "grades",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE grades g
                SET student_id = s.student_id,
                    assignment_id = s.assignment_id
                FROM submissions s
                WHERE g.submission_id = s.submission_id;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "assignment_id",
                table: "grades",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "student_id",
                table: "grades",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_rubric",
                table: "assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "sba_weight",
                table: "assignments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "term_number",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "approval_records",
                columns: table => new
                {
                    approval_record_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_records", x => x.approval_record_id);
                    table.ForeignKey(
                        name: "fk_approval_records__assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "assignments",
                        principalColumn: "assignment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_approval_records__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_records__users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_records__users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_criteria",
                columns: table => new
                {
                    criteria_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    max_mark = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_criteria", x => x.criteria_id);
                    table.ForeignKey(
                        name: "fk_assessment_criteria__assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "assignments",
                        principalColumn: "assignment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_assessment_criteria__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mark_capture_audit_logs",
                columns: table => new
                {
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    new_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    previous_is_absent = table.Column<bool>(type: "boolean", nullable: false),
                    new_is_absent = table.Column<bool>(type: "boolean", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mark_capture_audit_logs", x => x.audit_id);
                    table.ForeignKey(
                        name: "fk_mark_capture_audit_logs__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mark_capture_audit_logs__users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mark_capture_audit_logs_grades_grade_id",
                        column: x => x.grade_id,
                        principalTable: "grades",
                        principalColumn: "grade_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "criteria_scores",
                columns: table => new
                {
                    criteria_score_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criteria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criteria_scores", x => x.criteria_score_id);
                    table.ForeignKey(
                        name: "fk_criteria_scores__assessment_criteria_criteria_id",
                        column: x => x.criteria_id,
                        principalTable: "assessment_criteria",
                        principalColumn: "criteria_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_criteria_scores__grades_grade_id",
                        column: x => x.grade_id,
                        principalTable: "grades",
                        principalColumn: "grade_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_criteria_scores__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grades_assignment_id_student_id",
                table: "grades",
                columns: new[] { "assignment_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grades_student_id",
                table: "grades",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_records_assignment_id",
                table: "approval_records",
                column: "assignment_id",
                unique: true,
                filter: "status IN ('Draft', 'Submitted')");

            migrationBuilder.CreateIndex(
                name: "ix_approval_records_reviewed_by_user_id",
                table: "approval_records",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_approval_records_school_id_status",
                table: "approval_records",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_records_submitted_by_user_id",
                table: "approval_records",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_assessment_criteria_assignment_id_display_order",
                table: "assessment_criteria",
                columns: new[] { "assignment_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_criteria_school_id",
                table: "assessment_criteria",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_criteria_scores_criteria_id",
                table: "criteria_scores",
                column: "criteria_id");

            migrationBuilder.CreateIndex(
                name: "IX_criteria_scores_grade_id_criteria_id",
                table: "criteria_scores",
                columns: new[] { "grade_id", "criteria_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_criteria_scores_school_id",
                table: "criteria_scores",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_mark_capture_audit_logs_changed_by_user_id",
                table: "mark_capture_audit_logs",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mark_capture_audit_logs_grade_id_changed_at",
                table: "mark_capture_audit_logs",
                columns: new[] { "grade_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mark_capture_audit_logs_school_id",
                table: "mark_capture_audit_logs",
                column: "school_id");

            migrationBuilder.AddForeignKey(
                name: "fk_grades__students_student_id",
                table: "grades",
                column: "student_id",
                principalTable: "students",
                principalColumn: "student_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_grades_assignments_assignment_id",
                table: "grades",
                column: "assignment_id",
                principalTable: "assignments",
                principalColumn: "assignment_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_grades__students_student_id",
                table: "grades");

            migrationBuilder.DropForeignKey(
                name: "fk_grades_assignments_assignment_id",
                table: "grades");

            migrationBuilder.DropTable(
                name: "approval_records");

            migrationBuilder.DropTable(
                name: "criteria_scores");

            migrationBuilder.DropTable(
                name: "mark_capture_audit_logs");

            migrationBuilder.DropTable(
                name: "assessment_criteria");

            migrationBuilder.DropIndex(
                name: "IX_grades_assignment_id_student_id",
                table: "grades");

            migrationBuilder.DropIndex(
                name: "ix_grades_student_id",
                table: "grades");

            migrationBuilder.DropColumn(
                name: "assignment_id",
                table: "grades");

            migrationBuilder.DropColumn(
                name: "is_absent",
                table: "grades");

            migrationBuilder.DropColumn(
                name: "student_id",
                table: "grades");

            migrationBuilder.DropColumn(
                name: "has_rubric",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "sba_weight",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "term_number",
                table: "assignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "submission_id",
                table: "grades",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "score",
                table: "grades",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
