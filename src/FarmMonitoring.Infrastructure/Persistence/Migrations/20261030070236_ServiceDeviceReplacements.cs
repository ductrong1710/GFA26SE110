using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceDeviceReplacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "replacement_id",
                table: "service_reports",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_device_replacements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    old_assignment_id = table.Column<int>(type: "integer", nullable: false),
                    new_assignment_id = table.Column<int>(type: "integer", nullable: false),
                    old_device_code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    new_device_code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    technician_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_device_replacements", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_device_replacements_rental_device_assignments_new_a~",
                        column: x => x.new_assignment_id,
                        principalTable: "rental_device_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_device_replacements_rental_device_assignments_old_a~",
                        column: x => x.old_assignment_id,
                        principalTable: "rental_device_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_device_replacements_service_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "service_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_device_replacements_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_reports_replacement_id",
                table: "service_reports",
                column: "replacement_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_device_replacements_job_id_id",
                table: "service_device_replacements",
                columns: new[] { "job_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_device_replacements_new_assignment_id",
                table: "service_device_replacements",
                column: "new_assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_device_replacements_old_assignment_id",
                table: "service_device_replacements",
                column: "old_assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_device_replacements_technician_id",
                table: "service_device_replacements",
                column: "technician_id");

            migrationBuilder.AddForeignKey(
                name: "FK_service_reports_service_device_replacements_replacement_id",
                table: "service_reports",
                column: "replacement_id",
                principalTable: "service_device_replacements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_reports_service_device_replacements_replacement_id",
                table: "service_reports");

            migrationBuilder.DropTable(
                name: "service_device_replacements");

            migrationBuilder.DropIndex(
                name: "IX_service_reports_replacement_id",
                table: "service_reports");

            migrationBuilder.DropColumn(
                name: "replacement_id",
                table: "service_reports");
        }
    }
}
