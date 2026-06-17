using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityPositionsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // audit_logs.audit_log_id: bigint -> uuid.
            // The prior bigint column carried an invalid `gen_random_uuid()` default and
            // never accepted inserts (uuid cannot be assigned to bigint), so the table is
            // empty. bigint cannot be cast to uuid, so we drop and recreate the key column.
            // The guard ABORTS if any rows exist, so audit data can never be silently lost.
            // DROP COLUMN handles both prior definitions (uuid-default or IDENTITY).
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM audit_logs LIMIT 1) THEN
        RAISE EXCEPTION 'audit_logs is not empty; aborting audit_log_id type change. Migrate/export existing rows first.';
    END IF;
    ALTER TABLE audit_logs DROP CONSTRAINT IF EXISTS pk_audit_logs;
    ALTER TABLE audit_logs DROP COLUMN IF EXISTS audit_log_id;
    ALTER TABLE audit_logs ADD COLUMN audit_log_id uuid NOT NULL DEFAULT gen_random_uuid();
    ALTER TABLE audit_logs ADD CONSTRAINT pk_audit_logs PRIMARY KEY (audit_log_id);
END $$;
");

            migrationBuilder.AddColumn<string>(
                name: "authorizing_position_key",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "permission_used",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.permission_id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    position_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scope_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_external = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    requires_time_limit = table.Column<bool>(type: "boolean", nullable: false),
                    requires_consent = table.Column<bool>(type: "boolean", nullable: false),
                    default_duration_hours = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.position_id);
                });

            migrationBuilder.CreateTable(
                name: "position_permissions",
                columns: table => new
                {
                    position_permission_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_permissions", x => x.position_permission_id);
                    table.ForeignKey(
                        name: "fk_position_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "permission_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_position_permissions_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "position_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_positions",
                columns: table => new
                {
                    user_position_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consent_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_positions", x => x.user_position_id);
                    table.ForeignKey(
                        name: "fk_user_positions_consent_records_consent_record_id",
                        column: x => x.consent_record_id,
                        principalTable: "consent_records",
                        principalColumn: "consent_record_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_positions_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "position_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_positions_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_positions_users_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_positions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_position_scopes",
                columns: table => new
                {
                    user_position_scope_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_position_scopes", x => x.user_position_scope_id);
                    table.ForeignKey(
                        name: "fk_user_position_scopes_user_positions_user_position_id",
                        column: x => x.user_position_id,
                        principalTable: "user_positions",
                        principalColumn: "user_position_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_key",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_permissions_permission_id",
                table: "position_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_permissions_position_id_permission_id",
                table: "position_permissions",
                columns: new[] { "position_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_positions_key",
                table: "positions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_position_scopes_scope_type_scope_ref_id",
                table: "user_position_scopes",
                columns: new[] { "scope_type", "scope_ref_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_position_scopes_user_position_id",
                table: "user_position_scopes",
                column: "user_position_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_positions_consent_record_id",
                table: "user_positions",
                column: "consent_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_positions_granted_by_user_id",
                table: "user_positions",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_positions_position_id",
                table: "user_positions",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_positions_school_id",
                table: "user_positions",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_positions_school_id_user_id_is_active",
                table: "user_positions",
                columns: new[] { "school_id", "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_user_positions_user_id",
                table: "user_positions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "position_permissions");

            migrationBuilder.DropTable(
                name: "user_position_scopes");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "user_positions");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropColumn(
                name: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "authorizing_position_key",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "permission_used",
                table: "audit_logs");

            // Restore audit_log_id to a valid bigint identity column (the intended prior
            // design per SchoolPortalDB.sql), not the invalid bigint+uuid-default combo.
            // Guarded so rollback also cannot silently destroy audit rows.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM audit_logs LIMIT 1) THEN
        RAISE EXCEPTION 'audit_logs is not empty; aborting audit_log_id rollback. Migrate/export existing rows first.';
    END IF;
    ALTER TABLE audit_logs DROP CONSTRAINT IF EXISTS pk_audit_logs;
    ALTER TABLE audit_logs DROP COLUMN IF EXISTS audit_log_id;
    ALTER TABLE audit_logs ADD COLUMN audit_log_id bigint NOT NULL GENERATED BY DEFAULT AS IDENTITY;
    ALTER TABLE audit_logs ADD CONSTRAINT pk_audit_logs PRIMARY KEY (audit_log_id);
END $$;
");
        }
    }
}
