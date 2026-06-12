using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPathwaysV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                columns: table => new
                {
                    ai_usage_log_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost_zar = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage_logs", x => x.ai_usage_log_id);
                    table.ForeignKey(
                        name: "fk_ai_usage_logs__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "careers",
                columns: table => new
                {
                    career_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_careers", x => x.career_id);
                });

            migrationBuilder.CreateTable(
                name: "senior_phase_requirements",
                columns: table => new
                {
                    senior_phase_requirement_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fet_subject_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    required_senior_phase_subject_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recommended_min_percent = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_senior_phase_requirements", x => x.senior_phase_requirement_id);
                });

            migrationBuilder.CreateTable(
                name: "universities",
                columns: table => new
                {
                    university_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_universities", x => x.university_id);
                });

            migrationBuilder.CreateTable(
                name: "university_courses",
                columns: table => new
                {
                    university_course_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    career_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    faculty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    minimum_aps = table.Column<int>(type: "integer", nullable: false),
                    aps_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_university_courses", x => x.university_course_id);
                    table.ForeignKey(
                        name: "fk_university_courses_careers_career_id",
                        column: x => x.career_id,
                        principalTable: "careers",
                        principalColumn: "career_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_university_courses_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "university_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_gap_analysis_cache",
                columns: table => new
                {
                    ai_gap_analysis_cache_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_gap_analysis_caches", x => x.ai_gap_analysis_cache_id);
                    table.ForeignKey(
                        name: "fk_ai_gap_analysis_caches__students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ai_gap_analysis_caches__university_courses_university_course_~",
                        column: x => x.university_course_id,
                        principalTable: "university_courses",
                        principalColumn: "university_course_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_subject_requirements",
                columns: table => new
                {
                    course_subject_requirement_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    university_course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    minimum_percent = table.Column<int>(type: "integer", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_subject_requirements", x => x.course_subject_requirement_id);
                    table.ForeignKey(
                        name: "fk_course_subject_requirements__university_courses_university_co~",
                        column: x => x.university_course_id,
                        principalTable: "university_courses",
                        principalColumn: "university_course_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learner_career_goals",
                columns: table => new
                {
                    learner_career_goal_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learner_career_goals", x => x.learner_career_goal_id);
                    table.ForeignKey(
                        name: "fk_learner_career_goals__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learner_career_goals__students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_learner_career_goals__university_courses_university_course_id",
                        column: x => x.university_course_id,
                        principalTable: "university_courses",
                        principalColumn: "university_course_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_gap_analysis_cache_student_id_university_course_id_input~",
                table: "ai_gap_analysis_cache",
                columns: new[] { "student_id", "university_course_id", "input_fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_gap_analysis_caches_university_course_id",
                table: "ai_gap_analysis_cache",
                column: "university_course_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_school_id_created_at",
                table: "ai_usage_logs",
                columns: new[] { "school_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_course_subject_requirements_university_course_id",
                table: "course_subject_requirements",
                column: "university_course_id");

            migrationBuilder.CreateIndex(
                name: "ix_learner_career_goals_school_id",
                table: "learner_career_goals",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_learner_career_goals_student_id",
                table: "learner_career_goals",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_learner_career_goals_student_id_university_course_id",
                table: "learner_career_goals",
                columns: new[] { "student_id", "university_course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_learner_career_goals_university_course_id",
                table: "learner_career_goals",
                column: "university_course_id");

            migrationBuilder.CreateIndex(
                name: "ix_university_courses_career_id",
                table: "university_courses",
                column: "career_id");

            migrationBuilder.CreateIndex(
                name: "ix_university_courses_university_id",
                table: "university_courses",
                column: "university_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_gap_analysis_cache");

            migrationBuilder.DropTable(
                name: "ai_usage_logs");

            migrationBuilder.DropTable(
                name: "course_subject_requirements");

            migrationBuilder.DropTable(
                name: "learner_career_goals");

            migrationBuilder.DropTable(
                name: "senior_phase_requirements");

            migrationBuilder.DropTable(
                name: "university_courses");

            migrationBuilder.DropTable(
                name: "careers");

            migrationBuilder.DropTable(
                name: "universities");
        }
    }
}
