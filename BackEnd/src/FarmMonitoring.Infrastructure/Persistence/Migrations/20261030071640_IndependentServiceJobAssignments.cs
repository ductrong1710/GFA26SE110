using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndependentServiceJobAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "incident_id",
                table: "service_jobs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "rental_device_assignment_id",
                table: "service_jobs",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE service_jobs AS j SET rental_device_assignment_id = i.rental_device_assignment_id FROM device_incidents AS i WHERE j.incident_id = i.id");
            migrationBuilder.AlterColumn<int>(
                name: "rental_device_assignment_id",
                table: "service_jobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_jobs_rental_device_assignment_id",
                table: "service_jobs",
                column: "rental_device_assignment_id");

            migrationBuilder.AddForeignKey(
                name: "FK_service_jobs_rental_device_assignments_rental_device_assign~",
                table: "service_jobs",
                column: "rental_device_assignment_id",
                principalTable: "rental_device_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM service_jobs WHERE incident_id IS NULL) THEN RAISE EXCEPTION 'Cannot downgrade while independent service jobs exist'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_service_jobs_rental_device_assignments_rental_device_assign~",
                table: "service_jobs");

            migrationBuilder.DropIndex(
                name: "IX_service_jobs_rental_device_assignment_id",
                table: "service_jobs");

            migrationBuilder.DropColumn(
                name: "rental_device_assignment_id",
                table: "service_jobs");

            migrationBuilder.AlterColumn<int>(
                name: "incident_id",
                table: "service_jobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
