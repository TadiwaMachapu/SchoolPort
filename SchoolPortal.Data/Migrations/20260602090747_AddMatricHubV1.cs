using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatricHubV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gr9_subject_advice_cache",
                columns: table => new
                {
                    gr9_subject_advice_cache_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    advice_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gr9_subject_advice_caches", x => x.gr9_subject_advice_cache_id);
                });

            migrationBuilder.CreateTable(
                name: "matric_past_papers",
                columns: table => new
                {
                    matric_past_paper_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subject = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    paper_number = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    has_memo = table.Column<bool>(type: "boolean", nullable: false),
                    memo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matric_past_papers", x => x.matric_past_paper_id);
                });

            migrationBuilder.CreateTable(
                name: "matric_quiz_questions",
                columns: table => new
                {
                    matric_quiz_question_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subject = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    question_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    option_a = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_b = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_c = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    option_d = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    correct_option = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matric_quiz_questions", x => x.matric_quiz_question_id);
                });

            migrationBuilder.CreateTable(
                name: "matric_tutor_cache",
                columns: table => new
                {
                    matric_tutor_cache_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    subject = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    answer_markdown = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matric_tutor_caches", x => x.matric_tutor_cache_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gr9_subject_advice_cache_student_id_input_fingerprint",
                table: "gr9_subject_advice_cache",
                columns: new[] { "student_id", "input_fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_matric_past_papers_subject_year_paper_number_language",
                table: "matric_past_papers",
                columns: new[] { "subject", "year", "paper_number", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matric_quiz_questions_subject",
                table: "matric_quiz_questions",
                column: "subject");

            migrationBuilder.CreateIndex(
                name: "IX_matric_tutor_cache_input_fingerprint",
                table: "matric_tutor_cache",
                column: "input_fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gr9_subject_advice_cache");

            migrationBuilder.DropTable(
                name: "matric_past_papers");

            migrationBuilder.DropTable(
                name: "matric_quiz_questions");

            migrationBuilder.DropTable(
                name: "matric_tutor_cache");
        }
    }
}
