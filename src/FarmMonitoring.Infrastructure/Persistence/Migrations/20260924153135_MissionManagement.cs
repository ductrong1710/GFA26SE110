using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MissionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "missions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    uav_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scheduled_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scheduled_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    operator_notes = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_missions", x => x.id);
                    table.CheckConstraint("CK_missions_schedule", "(scheduled_start_at IS NULL AND scheduled_end_at IS NULL) OR (scheduled_start_at IS NOT NULL AND scheduled_end_at IS NOT NULL AND scheduled_end_at > scheduled_start_at)");
                    table.CheckConstraint("CK_missions_status", "status IN ('PENDING','SCHEDULED','RUNNING','COMPLETED','FAILED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_missions_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_missions_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_missions_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_missions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mission_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    log_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_mission_logs_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mission_logs_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mission_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mission_waypoints",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    local_x = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    local_y = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    altitude_m = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    planned_hold_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_waypoints", x => x.id);
                    table.CheckConstraint("CK_mission_waypoints_coordinates", "((latitude IS NOT NULL AND longitude IS NOT NULL) OR (local_x IS NOT NULL AND local_y IS NOT NULL)) AND (latitude IS NULL) = (longitude IS NULL) AND (local_x IS NULL) = (local_y IS NULL) AND (latitude BETWEEN -90 AND 90) AND (longitude BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_mission_waypoints_hold", "planned_hold_seconds >= 0");
                    table.CheckConstraint("CK_mission_waypoints_sequence", "sequence_no > 0");
                    table.ForeignKey(
                        name: "FK_mission_waypoints_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mission_targets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: false),
                    waypoint_id = table.Column<int>(type: "integer", nullable: true),
                    sequence_no = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mission_targets", x => x.id);
                    table.CheckConstraint("CK_mission_targets_status", "status IN ('PENDING','COLLECTED','FAILED','SKIPPED')");
                    table.ForeignKey(
                        name: "FK_mission_targets_mission_waypoints_waypoint_id",
                        column: x => x.waypoint_id,
                        principalTable: "mission_waypoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mission_targets_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mission_targets_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mission_logs_gateway_id",
                table: "mission_logs",
                column: "gateway_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_logs_mission_id_created_at",
                table: "mission_logs",
                columns: new[] { "mission_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_logs_user_id",
                table: "mission_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_targets_mission_id_sensor_node_id",
                table: "mission_targets",
                columns: new[] { "mission_id", "sensor_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_targets_sensor_node_id",
                table: "mission_targets",
                column: "sensor_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_targets_waypoint_id",
                table: "mission_targets",
                column: "waypoint_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_waypoints_mission_id_sequence_no",
                table: "mission_waypoints",
                columns: new[] { "mission_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_missions_created_by_user_id",
                table: "missions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_missions_farm_id_status",
                table: "missions",
                columns: new[] { "farm_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_missions_gateway_id_status_scheduled_start_at_scheduled_end~",
                table: "missions",
                columns: new[] { "gateway_id", "status", "scheduled_start_at", "scheduled_end_at" });

            migrationBuilder.CreateIndex(
                name: "IX_missions_scheduled_start_at",
                table: "missions",
                column: "scheduled_start_at");

            migrationBuilder.CreateIndex(
                name: "IX_missions_uav_id_status_scheduled_start_at_scheduled_end_at",
                table: "missions",
                columns: new[] { "uav_id", "status", "scheduled_start_at", "scheduled_end_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mission_logs");

            migrationBuilder.DropTable(
                name: "mission_targets");

            migrationBuilder.DropTable(
                name: "mission_waypoints");

            migrationBuilder.DropTable(
                name: "missions");
        }
    }
}
