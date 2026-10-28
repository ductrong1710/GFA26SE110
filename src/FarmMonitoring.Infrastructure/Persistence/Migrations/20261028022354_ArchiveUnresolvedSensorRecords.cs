using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveUnresolvedSensorRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archived_sensor_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: false),
                    gateway_id = table.Column<int>(type: "integer", nullable: false),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    source_record_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archived_sensor_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_archived_sensor_records_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_archived_sensor_records_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_archived_sensor_records_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_archived_sensor_records_gateway_id",
                table: "archived_sensor_records",
                column: "gateway_id");

            migrationBuilder.CreateIndex(
                name: "IX_archived_sensor_records_mission_id",
                table: "archived_sensor_records",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_archived_sensor_records_sensor_node_id_source_record_key",
                table: "archived_sensor_records",
                columns: new[] { "sensor_node_id", "source_record_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archived_sensor_records");
        }
    }
}
