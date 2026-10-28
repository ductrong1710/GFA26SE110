using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledMissionLaunch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "launch_approved_at",
                table: "missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "launch_consumed_at",
                table: "missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "launch_ready_at",
                table: "missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "launch_session",
                table: "missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "plan_revision",
                table: "missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "launch_approved_at",
                table: "missions");

            migrationBuilder.DropColumn(
                name: "launch_consumed_at",
                table: "missions");

            migrationBuilder.DropColumn(
                name: "launch_ready_at",
                table: "missions");

            migrationBuilder.DropColumn(
                name: "launch_session",
                table: "missions");

            migrationBuilder.DropColumn(
                name: "plan_revision",
                table: "missions");
        }
    }
}
