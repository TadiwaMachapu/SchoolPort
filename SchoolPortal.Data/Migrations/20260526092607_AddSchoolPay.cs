using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolPay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fees",
                columns: table => new
                {
                    fee_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    amount_zar = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fees", x => x.fee_id);
                    table.ForeignKey(
                        name: "fk_fees__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fees__terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "term_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "learner_subjects",
                columns: table => new
                {
                    learner_subject_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learner_subjects", x => x.learner_subject_id);
                    table.ForeignKey(
                        name: "fk_learner_subjects__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learner_subjects__students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_learner_subjects__subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learner_subjects_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "academic_year_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fee_payments",
                columns: table => new
                {
                    fee_payment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_paid_zar = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fee_payments", x => x.fee_payment_id);
                    table.ForeignKey(
                        name: "fk_fee_payments__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fee_payments__students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fee_payments__users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fee_payments_fees_fee_id",
                        column: x => x.fee_id,
                        principalTable: "fees",
                        principalColumn: "fee_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fee_payments_fee_id",
                table: "fee_payments",
                column: "fee_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_payments_recorded_by_user_id",
                table: "fee_payments",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_payments_school_id",
                table: "fee_payments",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_fee_payments_student_id",
                table: "fee_payments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_fees_school_id",
                table: "fees",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_fees_term_id",
                table: "fees",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_subjects_academic_year_id",
                table: "learner_subjects",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_subjects_school_id",
                table: "learner_subjects",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_learner_subjects_student_id",
                table: "learner_subjects",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_learner_subjects_student_id_subject_id_academic_year_id",
                table: "learner_subjects",
                columns: new[] { "student_id", "subject_id", "academic_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_learner_subjects_subject_id",
                table: "learner_subjects",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_payments");

            migrationBuilder.DropTable(
                name: "learner_subjects");

            migrationBuilder.DropTable(
                name: "fees");
        }
    }
}
