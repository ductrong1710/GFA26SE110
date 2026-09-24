using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SensorManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensor_nodes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zone_id = table.Column<int>(type: "integer", nullable: false),
                    device_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    local_x = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    local_y = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    battery_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_nodes", x => x.id);
                    table.CheckConstraint("CK_sensor_nodes_battery_percent", "battery_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_sensor_nodes_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_channels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_type_id = table.Column<int>(type: "integer", nullable: false),
                    channel_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_channels", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensor_channels_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_channels_sensor_types_sensor_type_id",
                        column: x => x.sensor_type_id,
                        principalTable: "sensor_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_channels_sensor_node_id_channel_code",
                table: "sensor_channels",
                columns: new[] { "sensor_node_id", "channel_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensor_channels_sensor_type_id",
                table: "sensor_channels",
                column: "sensor_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_device_code",
                table: "sensor_nodes",
                column: "device_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_zone_id",
                table: "sensor_nodes",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_types_code",
                table: "sensor_types",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensor_channels");

            migrationBuilder.DropTable(
                name: "sensor_nodes");

            migrationBuilder.DropTable(
                name: "sensor_types");
        }
    }
}
