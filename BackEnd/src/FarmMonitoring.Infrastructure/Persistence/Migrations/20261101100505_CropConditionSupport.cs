using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CropConditionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.AddColumn<int>(
                name: "crop_condition_report_id",
                table: "evidence_images",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "crop_consultation_update_id",
                table: "evidence_images",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "crop_condition_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    zone_id = table.Column<int>(type: "integer", nullable: true),
                    reporter_id = table.Column<int>(type: "integer", nullable: false),
                    engineer_id = table.Column<int>(type: "integer", nullable: true),
                    crop = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crop_condition_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_crop_condition_reports_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_condition_reports_users_engineer_id",
                        column: x => x.engineer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_condition_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_condition_reports_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crop_condition_sensors",
                columns: table => new
                {
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_channel_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crop_condition_sensors", x => new { x.report_id, x.sensor_channel_id });
                    table.ForeignKey(
                        name: "FK_crop_condition_sensors_crop_condition_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "crop_condition_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_condition_sensors_sensor_channels_sensor_channel_id",
                        column: x => x.sensor_channel_id,
                        principalTable: "sensor_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crop_consultation_updates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    recommend_site_survey = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crop_consultation_updates", x => x.id);
                    table.ForeignKey(
                        name: "FK_crop_consultation_updates_crop_condition_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "crop_condition_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_consultation_updates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_crop_condition_report_id",
                table: "evidence_images",
                column: "crop_condition_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_crop_consultation_update_id",
                table: "evidence_images",
                column: "crop_consultation_update_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id, service_report_id, crop_condition_report_id, crop_consultation_update_id) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_crop_condition_reports_engineer_id_status_reported_at",
                table: "crop_condition_reports",
                columns: new[] { "engineer_id", "status", "reported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_crop_condition_reports_farm_id_reported_at",
                table: "crop_condition_reports",
                columns: new[] { "farm_id", "reported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_crop_condition_reports_reporter_id",
                table: "crop_condition_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_condition_reports_zone_id",
                table: "crop_condition_reports",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_condition_sensors_sensor_channel_id",
                table: "crop_condition_sensors",
                column: "sensor_channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_consultation_updates_report_id_id",
                table: "crop_consultation_updates",
                columns: new[] { "report_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_crop_consultation_updates_user_id",
                table: "crop_consultation_updates",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_images_crop_condition_reports_crop_condition_repor~",
                table: "evidence_images",
                column: "crop_condition_report_id",
                principalTable: "crop_condition_reports",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_images_crop_consultation_updates_crop_consultation~",
                table: "evidence_images",
                column: "crop_consultation_update_id",
                principalTable: "crop_consultation_updates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM crop_condition_reports) THEN RAISE EXCEPTION 'Cannot downgrade while crop condition reports exist'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_images_crop_condition_reports_crop_condition_repor~",
                table: "evidence_images");

            migrationBuilder.DropForeignKey(
                name: "FK_evidence_images_crop_consultation_updates_crop_consultation~",
                table: "evidence_images");

            migrationBuilder.DropTable(
                name: "crop_condition_sensors");

            migrationBuilder.DropTable(
                name: "crop_consultation_updates");

            migrationBuilder.DropTable(
                name: "crop_condition_reports");

            migrationBuilder.DropIndex(
                name: "IX_evidence_images_crop_condition_report_id",
                table: "evidence_images");

            migrationBuilder.DropIndex(
                name: "IX_evidence_images_crop_consultation_update_id",
                table: "evidence_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.DropColumn(
                name: "crop_condition_report_id",
                table: "evidence_images");

            migrationBuilder.DropColumn(
                name: "crop_consultation_update_id",
                table: "evidence_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id, service_report_id) = 1");
        }
    }
}
