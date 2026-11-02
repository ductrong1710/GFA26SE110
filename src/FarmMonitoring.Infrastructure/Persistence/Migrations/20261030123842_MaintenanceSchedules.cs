using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maintenance_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rental_device_assignment_id = table.Column<int>(type: "integer", nullable: true),
                    device_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    policy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    next_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    technician_id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: false),
                    updated_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_schedules", x => x.id);
                    table.CheckConstraint("ck_maintenance_schedule_interval", "interval_days BETWEEN 1 AND 3650");
                    table.CheckConstraint("ck_maintenance_schedule_scope", "(rental_device_assignment_id IS NOT NULL AND device_type IS NULL) OR (rental_device_assignment_id IS NULL AND device_type IS NOT NULL AND device_type IN ('SENSOR','GATEWAY','UAV'))");
                    table.ForeignKey(
                        name: "FK_maintenance_schedules_rental_device_assignments_rental_devi~",
                        column: x => x.rental_device_assignment_id,
                        principalTable: "rental_device_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_schedules_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_schedules_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_schedules_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_occurrences",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    schedule_id = table.Column<int>(type: "integer", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rental_device_assignment_id = table.Column<int>(type: "integer", nullable: false),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    policy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "FK_maintenance_occurrences_maintenance_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "maintenance_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_occurrences_rental_device_assignments_rental_de~",
                        column: x => x.rental_device_assignment_id,
                        principalTable: "rental_device_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_occurrences_service_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "service_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_occurrences_job_id",
                table: "maintenance_occurrences",
                column: "job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_occurrences_rental_device_assignment_id",
                table: "maintenance_occurrences",
                column: "rental_device_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_occurrences_schedule_id_due_at_rental_device_as~",
                table: "maintenance_occurrences",
                columns: new[] { "schedule_id", "due_at", "rental_device_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_created_by_id",
                table: "maintenance_schedules",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_enabled_next_due_at",
                table: "maintenance_schedules",
                columns: new[] { "enabled", "next_due_at" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_rental_device_assignment_id",
                table: "maintenance_schedules",
                column: "rental_device_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_technician_id",
                table: "maintenance_schedules",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_updated_by_id",
                table: "maintenance_schedules",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_occurrences");

            migrationBuilder.DropTable(
                name: "maintenance_schedules");
        }
    }
}
