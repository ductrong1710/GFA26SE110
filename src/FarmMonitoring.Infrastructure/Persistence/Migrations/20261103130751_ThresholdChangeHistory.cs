using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThresholdChangeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "threshold_changes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sensor_channel_id = table.Column<int>(type: "integer", nullable: false),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    max_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    data_timeout_minutes = table.Column<int>(type: "integer", nullable: true),
                    low_battery_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_threshold_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_threshold_changes_sensor_channels_sensor_channel_id",
                        column: x => x.sensor_channel_id,
                        principalTable: "sensor_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_threshold_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_threshold_changes_changed_by_user_id",
                table: "threshold_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_threshold_changes_sensor_channel_id_id",
                table: "threshold_changes",
                columns: new[] { "sensor_channel_id", "id" });
            migrationBuilder.Sql("""
                INSERT INTO threshold_changes (sensor_channel_id, changed_at, min_value, max_value, data_timeout_minutes, low_battery_percent, is_enabled)
                SELECT sensor_channel_id, COALESCE(updated_at, created_at), min_value, max_value, data_timeout_minutes, low_battery_percent, is_enabled
                FROM sensor_thresholds;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM threshold_changes WHERE changed_by_user_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while recorded administrator threshold changes exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "threshold_changes");
        }
    }
}
