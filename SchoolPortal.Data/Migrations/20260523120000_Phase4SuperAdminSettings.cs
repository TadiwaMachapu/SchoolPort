using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations;

/// <summary>
/// Reconstructed stub for the lost migration <c>20260523120000_Phase4SuperAdminSettings</c>, which
/// originally created the <c>super_admins</c> table. The original file was gitignored at the time and
/// later deleted from the dev machine, but its id IS recorded in every existing/live database's
/// <c>__EFMigrationsHistory</c>. Consequences:
///   - On live/existing databases EF sees the id is already applied and SKIPS this migration (no-op).
///   - On a FRESH database EF runs it, creating <c>super_admins</c> so that the next migration,
///     <c>AddAcademicCalendar</c> (which renames super_admins' primary key from PK_super_admins to
///     pk_super_admins), can apply. Without it the chain aborts on a from-scratch replay.
/// CREATE TABLE IF NOT EXISTS keeps it idempotent. The primary key is named "PK_super_admins" (the
/// pre-rename name) precisely because AddAcademicCalendar drops that constraint by name.
/// Schema matches the live super_admins table (verified 2026-06-19).
/// </summary>
[DbContext(typeof(SchoolPortalDbContext))]
[Migration("20260523120000_Phase4SuperAdminSettings")]
public partial class Phase4SuperAdminSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS super_admins (
                super_admin_id uuid NOT NULL DEFAULT gen_random_uuid(),
                email character varying NOT NULL,
                password_hash character varying NOT NULL,
                first_name character varying NOT NULL,
                last_name character varying NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                last_login_at timestamp with time zone NULL,
                CONSTRAINT ""PK_super_admins"" PRIMARY KEY (super_admin_id)
            );");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS super_admins;");
    }
}
