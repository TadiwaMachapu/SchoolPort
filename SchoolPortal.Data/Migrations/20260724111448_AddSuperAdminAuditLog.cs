using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "super_admin_audit_logs",
                columns: table => new
                {
                    audit_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    super_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_school_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_super_admin_audit_logs", x => x.audit_id);
                    table.ForeignKey(
                        name: "fk_super_admin_audit_logs_schools_target_school_id",
                        column: x => x.target_school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_super_admin_audit_logs_super_admins_super_admin_id",
                        column: x => x.super_admin_id,
                        principalTable: "super_admins",
                        principalColumn: "super_admin_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_super_admin_audit_logs_created_at",
                table: "super_admin_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_super_admin_audit_logs_super_admin_id_created_at",
                table: "super_admin_audit_logs",
                columns: new[] { "super_admin_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_super_admin_audit_logs_target_school_id",
                table: "super_admin_audit_logs",
                column: "target_school_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "super_admin_audit_logs");
        }
    }
}
