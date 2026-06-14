using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolPortal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_activities_owner_user_id",
                table: "activities",
                column: "owner_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_activities__users_owner_user_id",
                table: "activities",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_activities__users_owner_user_id",
                table: "activities");

            migrationBuilder.DropIndex(
                name: "ix_activities_owner_user_id",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "activities");
        }
    }
}
