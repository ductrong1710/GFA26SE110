using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CropSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.AddColumn<int>(
                name: "cultivation_activity_id",
                table: "evidence_images",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "crop_seasons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    zone_id = table.Column<int>(type: "integer", nullable: true),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    crop = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area_hectares = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    crop_age_days = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    yield_kg = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    total_cost_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    revenue_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    completion_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crop_seasons", x => x.id);
                    table.CheckConstraint("ck_crop_season_dates", "(start_date IS NULL OR expected_end_date IS NULL OR expected_end_date >= start_date) AND (start_date IS NULL OR end_date IS NULL OR end_date >= start_date)");
                    table.CheckConstraint("ck_crop_season_status", "(status = 'ACTIVE' AND completed_at IS NULL) OR (status = 'COMPLETED' AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_crop_season_values", "(area_hectares IS NULL OR area_hectares > 0) AND (crop_age_days IS NULL OR crop_age_days >= 0) AND (yield_kg IS NULL OR yield_kg >= 0) AND (total_cost_vnd IS NULL OR total_cost_vnd >= 0) AND (revenue_vnd IS NULL OR revenue_vnd >= 0) AND revision > 0");
                    table.ForeignKey(
                        name: "FK_crop_seasons_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_seasons_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_crop_seasons_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cultivation_activities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    season_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    cost_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cultivation_activities", x => x.id);
                    table.CheckConstraint("ck_cultivation_activity_cost", "cost_vnd IS NULL OR cost_vnd >= 0");
                    table.CheckConstraint("ck_cultivation_activity_type", "type IN ('WATERING', 'FERTILIZING', 'PESTICIDE', 'INSPECTION', 'OTHER')");
                    table.ForeignKey(
                        name: "FK_cultivation_activities_crop_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "crop_seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cultivation_activities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_cultivation_activity_id",
                table: "evidence_images",
                column: "cultivation_activity_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id, service_report_id, crop_condition_report_id, crop_consultation_update_id, cultivation_activity_id) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_crop_seasons_farm_id",
                table: "crop_seasons",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "IX_crop_seasons_owner_id_farm_id_id",
                table: "crop_seasons",
                columns: new[] { "owner_id", "farm_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_crop_seasons_zone_id",
                table: "crop_seasons",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_cultivation_activities_season_id_date_id",
                table: "cultivation_activities",
                columns: new[] { "season_id", "date", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_cultivation_activities_user_id",
                table: "cultivation_activities",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_images_cultivation_activities_cultivation_activity~",
                table: "evidence_images",
                column: "cultivation_activity_id",
                principalTable: "cultivation_activities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM crop_seasons) THEN RAISE EXCEPTION 'Cannot downgrade while crop seasons exist'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_images_cultivation_activities_cultivation_activity~",
                table: "evidence_images");

            migrationBuilder.DropTable(
                name: "cultivation_activities");

            migrationBuilder.DropTable(
                name: "crop_seasons");

            migrationBuilder.DropIndex(
                name: "IX_evidence_images_cultivation_activity_id",
                table: "evidence_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.DropColumn(
                name: "cultivation_activity_id",
                table: "evidence_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id, service_report_id, crop_condition_report_id, crop_consultation_update_id) = 1");
        }
    }
}
