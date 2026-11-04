using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FarmMaintenanceSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_maintenance_schedule_scope",
                table: "maintenance_schedules");

            migrationBuilder.AddColumn<int>(
                name: "farm_id",
                table: "maintenance_schedules",
                type: "integer",
                nullable: true);

            // Never silently broaden an old device/type schedule to every device at a farm.
            // Retain old scope and occurrence history; an administrator must explicitly choose a farm.
            migrationBuilder.Sql("UPDATE maintenance_schedules SET enabled = FALSE, revision = CASE WHEN revision < 2147483647 THEN revision + 1 ELSE revision END, updated_at = CURRENT_TIMESTAMP WHERE enabled");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_farm_id",
                table: "maintenance_schedules",
                column: "farm_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_maintenance_schedule_scope",
                table: "maintenance_schedules",
                sql: "(farm_id IS NOT NULL AND rental_device_assignment_id IS NULL AND device_type IS NULL) OR (farm_id IS NULL AND NOT enabled)");

            migrationBuilder.AddForeignKey(
                name: "FK_maintenance_schedules_farms_farm_id",
                table: "maintenance_schedules",
                column: "farm_id",
                principalTable: "farms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM maintenance_schedules WHERE farm_id IS NOT NULL) THEN RAISE EXCEPTION 'Cannot discard farm maintenance schedule scope.'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_maintenance_schedules_farms_farm_id",
                table: "maintenance_schedules");

            migrationBuilder.DropIndex(
                name: "IX_maintenance_schedules_farm_id",
                table: "maintenance_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_maintenance_schedule_scope",
                table: "maintenance_schedules");

            migrationBuilder.DropColumn(
                name: "farm_id",
                table: "maintenance_schedules");

            migrationBuilder.AddCheckConstraint(
                name: "ck_maintenance_schedule_scope",
                table: "maintenance_schedules",
                sql: "(rental_device_assignment_id IS NOT NULL AND device_type IS NULL) OR (rental_device_assignment_id IS NULL AND device_type IS NOT NULL AND device_type IN ('SENSOR','GATEWAY','UAV'))");
        }
    }
}
