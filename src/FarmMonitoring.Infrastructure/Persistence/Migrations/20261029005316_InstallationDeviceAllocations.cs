using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstallationDeviceAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installation_device_allocations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    proposal_line_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: true),
                    uav_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    allocated_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    allocated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installation_device_allocations", x => x.id);
                    table.CheckConstraint("CK_installation_allocation_identity", "num_nonnulls(sensor_node_id, uav_id, gateway_id) = 1");
                    table.ForeignKey(
                        name: "FK_installation_device_allocations_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_device_allocations_installation_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "installation_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_device_allocations_proposal_lines_proposal_lin~",
                        column: x => x.proposal_line_id,
                        principalTable: "proposal_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_device_allocations_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_device_allocations_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_device_allocations_users_allocated_by_user_id",
                        column: x => x.allocated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_installation_device_allocations_allocated_by_user_id",
                table: "installation_device_allocations",
                column: "allocated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_device_allocations_gateway_id",
                table: "installation_device_allocations",
                column: "gateway_id",
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_installation_device_allocations_job_id_released_at",
                table: "installation_device_allocations",
                columns: new[] { "job_id", "released_at" });

            migrationBuilder.CreateIndex(
                name: "IX_installation_device_allocations_proposal_line_id",
                table: "installation_device_allocations",
                column: "proposal_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_device_allocations_sensor_node_id",
                table: "installation_device_allocations",
                column: "sensor_node_id",
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_installation_device_allocations_uav_id",
                table: "installation_device_allocations",
                column: "uav_id",
                unique: true,
                filter: "released_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installation_device_allocations");
        }
    }
}
