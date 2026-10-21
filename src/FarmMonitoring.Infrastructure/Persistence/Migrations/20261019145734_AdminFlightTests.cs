using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminFlightTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flight_tests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gateway_id = table.Column<int>(type: "integer", nullable: false),
                    uav_id = table.Column<int>(type: "integer", nullable: false),
                    requested_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    request_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    gateway_connected = table.Column<bool>(type: "boolean", nullable: true),
                    uav_connected = table.Column<bool>(type: "boolean", nullable: true),
                    landed = table.Column<bool>(type: "boolean", nullable: true),
                    observed_altitude_cm = table.Column<int>(type: "integer", nullable: true),
                    held_ms = table.Column<int>(type: "integer", nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flight_tests", x => x.id);
                    table.ForeignKey(
                        name: "FK_flight_tests_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flight_tests_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flight_tests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_flight_tests_gateway_id_request_key",
                table: "flight_tests",
                columns: new[] { "gateway_id", "request_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flight_tests_gateway_id_status",
                table: "flight_tests",
                columns: new[] { "gateway_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_flight_tests_requested_by_user_id",
                table: "flight_tests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_flight_tests_uav_id_status",
                table: "flight_tests",
                columns: new[] { "uav_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flight_tests");
        }
    }
}
