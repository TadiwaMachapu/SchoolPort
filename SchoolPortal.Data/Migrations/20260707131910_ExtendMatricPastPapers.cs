using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMatricPastPapers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matric_past_papers_subject_year_paper_number_language",
                table: "matric_past_papers");

            migrationBuilder.AddColumn<int>(
                name: "grade",
                table: "matric_past_papers",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "matric_past_papers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "paper_type",
                table: "matric_past_papers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NSCNovember");

            migrationBuilder.CreateIndex(
                name: "IX_matric_past_papers_subject_year_paper_number_language_paper~",
                table: "matric_past_papers",
                columns: new[] { "subject", "year", "paper_number", "language", "paper_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matric_past_papers_subject_year_paper_number_language_paper~",
                table: "matric_past_papers");

            migrationBuilder.DropColumn(
                name: "grade",
                table: "matric_past_papers");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "matric_past_papers");

            migrationBuilder.DropColumn(
                name: "paper_type",
                table: "matric_past_papers");

            migrationBuilder.CreateIndex(
                name: "IX_matric_past_papers_subject_year_paper_number_language",
                table: "matric_past_papers",
                columns: new[] { "subject", "year", "paper_number", "language" },
                unique: true);
        }
    }
}
