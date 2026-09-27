using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MissionTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telemetry_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: false),
                    uav_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    local_x = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    local_y = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    altitude_m = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    battery_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    current_waypoint_no = table.Column<int>(type: "integer", nullable: true),
                    flight_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_records", x => x.id);
                    table.CheckConstraint("CK_telemetry_battery", "battery_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_telemetry_coordinates", "(latitude IS NULL) = (longitude IS NULL) AND (local_x IS NULL) = (local_y IS NULL) AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_telemetry_records_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_telemetry_records_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_telemetry_records_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_records_gateway_id",
                table: "telemetry_records",
                column: "gateway_id");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_records_mission_id_recorded_at",
                table: "telemetry_records",
                columns: new[] { "mission_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_records_uav_id",
                table: "telemetry_records",
                column: "uav_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_records");
        }
    }
}
