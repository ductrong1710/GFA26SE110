using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.AddColumn<int>(
                name: "incident_id",
                table: "evidence_images",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_incidents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    rental_device_assignment_id = table.Column<int>(type: "integer", nullable: false),
                    device_code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    reporter_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mission_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_incidents_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_incidents_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_incidents_rental_device_assignments_rental_device_as~",
                        column: x => x.rental_device_assignment_id,
                        principalTable: "rental_device_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_incidents_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_incident_id",
                table: "evidence_images",
                column: "incident_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_device_incidents_farm_id_id",
                table: "device_incidents",
                columns: new[] { "farm_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_device_incidents_mission_id",
                table: "device_incidents",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_incidents_rental_device_assignment_id",
                table: "device_incidents",
                column: "rental_device_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_incidents_reporter_id",
                table: "device_incidents",
                column: "reporter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_images_device_incidents_incident_id",
                table: "evidence_images",
                column: "incident_id",
                principalTable: "device_incidents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_images_device_incidents_incident_id",
                table: "evidence_images");

            migrationBuilder.DropTable(
                name: "device_incidents");

            migrationBuilder.DropIndex(
                name: "IX_evidence_images_incident_id",
                table: "evidence_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "evidence_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id) = 1");
        }
    }
}
