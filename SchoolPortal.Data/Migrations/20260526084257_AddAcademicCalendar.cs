using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_super_admins",
                table: "super_admins");

            migrationBuilder.AddColumn<string>(
                name: "caps_phase",
                table: "subjects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_super_admins",
                table: "super_admins",
                column: "super_admin_id");

            migrationBuilder.CreateTable(
                name: "academic_years",
                columns: table => new
                {
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_academic_years", x => x.academic_year_id);
                    table.ForeignKey(
                        name: "fk_academic_years__schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terms",
                columns: table => new
                {
                    term_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    academic_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms", x => x.term_id);
                    table.ForeignKey(
                        name: "fk_terms_academic_years_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "academic_year_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_terms_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_years_school_id",
                table: "academic_years",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_years_school_id_year",
                table: "academic_years",
                columns: new[] { "school_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terms_academic_year_id_term_number",
                table: "terms",
                columns: new[] { "academic_year_id", "term_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terms_school_id",
                table: "terms",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_terms_school_id_is_current",
                table: "terms",
                columns: new[] { "school_id", "is_current" },
                unique: true,
                filter: "is_current = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms");

            migrationBuilder.DropTable(
                name: "academic_years");

            migrationBuilder.DropPrimaryKey(
                name: "pk_super_admins",
                table: "super_admins");

            migrationBuilder.DropColumn(
                name: "caps_phase",
                table: "subjects");

            migrationBuilder.AddPrimaryKey(
                name: "PK_super_admins",
                table: "super_admins",
                column: "super_admin_id");
        }
    }
}
