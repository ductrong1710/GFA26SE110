using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CropSupportFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "assigned_engineer_id",
                table: "crop_consultation_updates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cultivation_activity_id",
                table: "crop_consultation_updates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "device_incident_id",
                table: "crop_consultation_updates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "follow_up_type",
                table: "crop_consultation_updates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referral_type",
                table: "crop_consultation_updates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "crop_season_id",
                table: "crop_condition_reports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "observed_at",
                table: "crop_condition_reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_crop_consultation_updates_assigned_engineer_id",
                table: "crop_consultation_updates",
                column: "assigned_engineer_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_consultation_updates_cultivation_activity_id",
                table: "crop_consultation_updates",
                column: "cultivation_activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_consultation_updates_device_incident_id",
                table: "crop_consultation_updates",
                column: "device_incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_condition_reports_crop_season_id",
                table: "crop_condition_reports",
                column: "crop_season_id");

            migrationBuilder.AddForeignKey(
                name: "FK_crop_condition_reports_crop_seasons_crop_season_id",
                table: "crop_condition_reports",
                column: "crop_season_id",
                principalTable: "crop_seasons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_crop_consultation_updates_cultivation_activities_cultivatio~",
                table: "crop_consultation_updates",
                column: "cultivation_activity_id",
                principalTable: "cultivation_activities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_crop_consultation_updates_device_incidents_device_incident_~",
                table: "crop_consultation_updates",
                column: "device_incident_id",
                principalTable: "device_incidents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_crop_consultation_updates_users_assigned_engineer_id",
                table: "crop_consultation_updates",
                column: "assigned_engineer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM crop_condition_reports WHERE crop_season_id IS NOT NULL OR observed_at IS NOT NULL)
                     OR EXISTS (SELECT 1 FROM crop_consultation_updates WHERE action IN ('ASSIGNED','FOLLOW_UP','REVIEW_REQUESTED','REOPENED','REFERRED'))
                  THEN RAISE EXCEPTION 'Cannot remove MF-04 follow-up schema while its history exists'; END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_crop_condition_reports_crop_seasons_crop_season_id",
                table: "crop_condition_reports");

            migrationBuilder.DropForeignKey(
                name: "FK_crop_consultation_updates_cultivation_activities_cultivatio~",
                table: "crop_consultation_updates");

            migrationBuilder.DropForeignKey(
                name: "FK_crop_consultation_updates_device_incidents_device_incident_~",
                table: "crop_consultation_updates");

            migrationBuilder.DropForeignKey(
                name: "FK_crop_consultation_updates_users_assigned_engineer_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropIndex(
                name: "IX_crop_consultation_updates_assigned_engineer_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropIndex(
                name: "IX_crop_consultation_updates_cultivation_activity_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropIndex(
                name: "IX_crop_consultation_updates_device_incident_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropIndex(
                name: "IX_crop_condition_reports_crop_season_id",
                table: "crop_condition_reports");

            migrationBuilder.DropColumn(
                name: "assigned_engineer_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropColumn(
                name: "cultivation_activity_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropColumn(
                name: "device_incident_id",
                table: "crop_consultation_updates");

            migrationBuilder.DropColumn(
                name: "follow_up_type",
                table: "crop_consultation_updates");

            migrationBuilder.DropColumn(
                name: "referral_type",
                table: "crop_consultation_updates");

            migrationBuilder.DropColumn(
                name: "crop_season_id",
                table: "crop_condition_reports");

            migrationBuilder.DropColumn(
                name: "observed_at",
                table: "crop_condition_reports");
        }
    }
}
