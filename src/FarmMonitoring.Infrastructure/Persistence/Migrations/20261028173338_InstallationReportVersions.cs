using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstallationReportVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "installation_job_history",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.CreateTable(
                name: "installation_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    technician_id = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    issues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmation_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approved = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installation_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_installation_reports_installation_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "installation_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_reports_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_reports_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_reports_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installed_device_checks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    proposal_line_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: true),
                    uav_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    installation_location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    accessories_checked = table.Column<bool>(type: "boolean", nullable: false),
                    connectivity_passed = table.Column<bool>(type: "boolean", nullable: false),
                    operation_passed = table.Column<bool>(type: "boolean", nullable: false),
                    test_results = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installed_device_checks", x => x.id);
                    table.CheckConstraint("CK_installed_device_identity", "num_nonnulls(sensor_node_id, uav_id, gateway_id) = 1");
                    table.ForeignKey(
                        name: "FK_installed_device_checks_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installed_device_checks_installation_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "installation_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installed_device_checks_proposal_lines_proposal_line_id",
                        column: x => x.proposal_line_id,
                        principalTable: "proposal_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installed_device_checks_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installed_device_checks_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_installation_reports_confirmed_by_user_id",
                table: "installation_reports",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_reports_job_id_revision",
                table: "installation_reports",
                columns: new[] { "job_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installation_reports_reviewed_by_user_id",
                table: "installation_reports",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_reports_technician_id",
                table: "installation_reports",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_gateway_id",
                table: "installed_device_checks",
                column: "gateway_id");

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_proposal_line_id",
                table: "installed_device_checks",
                column: "proposal_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_report_id_gateway_id",
                table: "installed_device_checks",
                columns: new[] { "report_id", "gateway_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_report_id_sensor_node_id",
                table: "installed_device_checks",
                columns: new[] { "report_id", "sensor_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_report_id_uav_id",
                table: "installed_device_checks",
                columns: new[] { "report_id", "uav_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_sensor_node_id",
                table: "installed_device_checks",
                column: "sensor_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_installed_device_checks_uav_id",
                table: "installed_device_checks",
                column: "uav_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installed_device_checks");

            migrationBuilder.DropTable(
                name: "installation_reports");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "installation_job_history",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);
        }
    }
}
