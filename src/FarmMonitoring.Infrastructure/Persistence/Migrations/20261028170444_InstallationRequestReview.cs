using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstallationRequestReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "installation_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "installation_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reviewed_by_user_id",
                table: "installation_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_installation_requests_reviewed_by_user_id",
                table: "installation_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_installation_requests_users_reviewed_by_user_id",
                table: "installation_requests",
                column: "reviewed_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_installation_requests_users_reviewed_by_user_id",
                table: "installation_requests");

            migrationBuilder.DropIndex(
                name: "IX_installation_requests_reviewed_by_user_id",
                table: "installation_requests");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "installation_requests");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "installation_requests");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                table: "installation_requests");
        }
    }
}
