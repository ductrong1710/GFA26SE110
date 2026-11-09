using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SensorReadingsAndSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_attempts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<int>(type: "integer", nullable: false),
                    mission_target_id = table.Column<int>(type: "integer", nullable: false),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    records_received = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_attempts", x => x.id);
                    table.CheckConstraint("CK_collection_attempts_values", "attempt_no > 0 AND records_received >= 0 AND finished_at >= started_at");
                    table.ForeignKey(
                        name: "FK_collection_attempts_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_attempts_mission_targets_mission_target_id",
                        column: x => x.mission_target_id,
                        principalTable: "mission_targets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_attempts_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sensor_channel_id = table.Column<int>(type: "integer", nullable: false),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    source_record_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quality_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    validation_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_readings", x => x.id);
                    table.CheckConstraint("CK_sensor_readings_time", "measured_at <= collected_at");
                    table.ForeignKey(
                        name: "FK_sensor_readings_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_readings_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_readings_sensor_channels_sensor_channel_id",
                        column: x => x.sensor_channel_id,
                        principalTable: "sensor_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_batches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gateway_id = table.Column<int>(type: "integer", nullable: false),
                    mission_id = table.Column<int>(type: "integer", nullable: true),
                    batch_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    record_count = table.Column<int>(type: "integer", nullable: false),
                    accepted_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_count = table.Column<int>(type: "integer", nullable: false),
                    rejected_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_batches", x => x.id);
                    table.CheckConstraint("CK_sync_batches_counts", "record_count >= 0 AND accepted_count >= 0 AND duplicate_count >= 0 AND rejected_count >= 0 AND record_count = accepted_count + duplicate_count + rejected_count");
                    table.ForeignKey(
                        name: "FK_sync_batches_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sync_batches_missions_mission_id",
                        column: x => x.mission_id,
                        principalTable: "missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_attempts_gateway_id",
                table: "collection_attempts",
                column: "gateway_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_attempts_mission_id",
                table: "collection_attempts",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_attempts_mission_target_id_attempt_no",
                table: "collection_attempts",
                columns: new[] { "mission_target_id", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_gateway_id",
                table: "sensor_readings",
                column: "gateway_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_mission_id",
                table: "sensor_readings",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_sensor_channel_id_measured_at",
                table: "sensor_readings",
                columns: new[] { "sensor_channel_id", "measured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_sensor_channel_id_source_record_key",
                table: "sensor_readings",
                columns: new[] { "sensor_channel_id", "source_record_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_batches_gateway_id_batch_key",
                table: "sync_batches",
                columns: new[] { "gateway_id", "batch_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_batches_mission_id",
                table: "sync_batches",
                column: "mission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_attempts");

            migrationBuilder.DropTable(
                name: "sensor_readings");

            migrationBuilder.DropTable(
                name: "sync_batches");
        }
    }
}
