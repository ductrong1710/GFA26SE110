using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RentalContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rental_contracts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    installation_job_id = table.Column<int>(type: "integer", nullable: false),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    warned_for_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_contracts", x => x.id);
                    table.CheckConstraint("ck_rental_dates", "end_at > start_at");
                    table.ForeignKey(
                        name: "FK_rental_contracts_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_contracts_installation_jobs_installation_job_id",
                        column: x => x.installation_job_id,
                        principalTable: "installation_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_contracts_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rental_device_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_id = table.Column<int>(type: "integer", nullable: false),
                    installation_allocation_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: true),
                    uav_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    installation_location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_device_assignments", x => x.id);
                    table.CheckConstraint("ck_rental_device_identity", "num_nonnulls(sensor_node_id,uav_id,gateway_id) = 1");
                    table.ForeignKey(
                        name: "FK_rental_device_assignments_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_device_assignments_installation_device_allocations_i~",
                        column: x => x.installation_allocation_id,
                        principalTable: "installation_device_allocations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_device_assignments_rental_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "rental_contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_device_assignments_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_device_assignments_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rental_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_rental_history_rental_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "rental_contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rental_contracts_farm_id",
                table: "rental_contracts",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_contracts_installation_job_id",
                table: "rental_contracts",
                column: "installation_job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rental_contracts_owner_id",
                table: "rental_contracts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_contracts_status_end_at",
                table: "rental_contracts",
                columns: new[] { "status", "end_at" });

            migrationBuilder.CreateIndex(
                name: "IX_rental_device_assignments_contract_id",
                table: "rental_device_assignments",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_device_assignments_gateway_id",
                table: "rental_device_assignments",
                column: "gateway_id",
                unique: true,
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rental_device_assignments_installation_allocation_id",
                table: "rental_device_assignments",
                column: "installation_allocation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rental_device_assignments_sensor_node_id",
                table: "rental_device_assignments",
                column: "sensor_node_id",
                unique: true,
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rental_device_assignments_uav_id",
                table: "rental_device_assignments",
                column: "uav_id",
                unique: true,
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rental_history_contract_id",
                table: "rental_history",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_history_user_id",
                table: "rental_history",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rental_device_assignments");

            migrationBuilder.DropTable(
                name: "rental_history");

            migrationBuilder.DropTable(
                name: "rental_contracts");
        }
    }
}
