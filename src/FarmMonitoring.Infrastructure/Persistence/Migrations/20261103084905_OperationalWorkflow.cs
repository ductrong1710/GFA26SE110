using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "paperwork_json",
                table: "service_reports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_end_at",
                table: "service_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_end_at",
                table: "service_job_history",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<decimal>(
                name: "replacement_price_vnd",
                table: "public_content",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_rent_vnd",
                table: "proposal_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "price_revision",
                table: "proposal_lines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                table: "proposal_lines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "public_content_id",
                table: "proposal_lines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "installation_reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paperwork_json",
                table: "installation_reports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_end_at",
                table: "installation_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_end_at",
                table: "installation_job_history",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AlterColumn<int>(
                name: "rental_device_assignment_id",
                table: "device_incidents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "public_price_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_content_id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    monthly_rent_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    replacement_price_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_price_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_public_price_versions_public_content_public_content_id",
                        column: x => x.public_content_id,
                        principalTable: "public_content",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_plans",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    old_assignment_id = table.Column<int>(type: "integer", nullable: false),
                    sensor_node_id = table.Column<int>(type: "integer", nullable: true),
                    uav_id = table.Column<int>(type: "integer", nullable: true),
                    gateway_id = table.Column<int>(type: "integer", nullable: true),
                    technician_id = table.Column<int>(type: "integer", nullable: false),
                    reviewer_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price_lines_json = table.Column<string>(type: "jsonb", nullable: false),
                    total_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    evidence_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_plans_gateways_gateway_id",
                        column: x => x.gateway_id,
                        principalTable: "gateways",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_plans_rental_device_assignments_old_assignment_id",
                        column: x => x.old_assignment_id,
                        principalTable: "rental_device_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_plans_sensor_nodes_sensor_node_id",
                        column: x => x.sensor_node_id,
                        principalTable: "sensor_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_plans_service_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "service_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_plans_uavs_uav_id",
                        column: x => x.uav_id,
                        principalTable: "uavs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_plans_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_plans_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_lines_public_content_id",
                table: "proposal_lines",
                column: "public_content_id");

            migrationBuilder.CreateIndex(
                name: "IX_public_price_versions_public_content_id_revision",
                table: "public_price_versions",
                columns: new[] { "public_content_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_gateway_id",
                table: "service_plans",
                column: "gateway_id",
                unique: true,
                filter: "status IN ('PROPOSED','APPROVED','EXECUTED') AND gateway_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_job_id",
                table: "service_plans",
                column: "job_id",
                unique: true,
                filter: "status IN ('PROPOSED','APPROVED','EXECUTED')");

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_old_assignment_id",
                table: "service_plans",
                column: "old_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_reviewer_id",
                table: "service_plans",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_sensor_node_id",
                table: "service_plans",
                column: "sensor_node_id",
                unique: true,
                filter: "status IN ('PROPOSED','APPROVED','EXECUTED') AND sensor_node_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_technician_id",
                table: "service_plans",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_plans_uav_id",
                table: "service_plans",
                column: "uav_id",
                unique: true,
                filter: "status IN ('PROPOSED','APPROVED','EXECUTED') AND uav_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_proposal_lines_public_content_public_content_id",
                table: "proposal_lines",
                column: "public_content_id",
                principalTable: "public_content",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("UPDATE installation_jobs SET scheduled_end_at = scheduled_at + interval '2 hours'");
            migrationBuilder.Sql("UPDATE service_jobs SET scheduled_end_at = scheduled_at + interval '2 hours'");
            migrationBuilder.Sql("UPDATE installation_job_history SET scheduled_end_at = scheduled_at + interval '2 hours'");
            migrationBuilder.Sql("UPDATE service_job_history SET scheduled_end_at = scheduled_at + interval '2 hours'");
            migrationBuilder.Sql("INSERT INTO public_price_versions (public_content_id,revision,title,monthly_rent_vnd,replacement_price_vnd,recorded_at,published) SELECT id,revision,title,estimated_monthly_rent_vnd,replacement_price_vnd,CURRENT_TIMESTAMP,published FROM public_content");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM service_plans) OR EXISTS (SELECT 1 FROM device_incidents WHERE rental_device_assignment_id IS NULL) OR EXISTS (SELECT 1 FROM installation_reports WHERE paperwork_json IS NOT NULL) OR EXISTS (SELECT 1 FROM service_reports WHERE paperwork_json IS NOT NULL) THEN RAISE EXCEPTION 'Operational workflow downgrade would discard retained paper/plan/incident evidence'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_proposal_lines_public_content_public_content_id",
                table: "proposal_lines");

            migrationBuilder.DropTable(
                name: "public_price_versions");

            migrationBuilder.DropTable(
                name: "service_plans");

            migrationBuilder.DropIndex(
                name: "IX_proposal_lines_public_content_id",
                table: "proposal_lines");

            migrationBuilder.DropColumn(
                name: "paperwork_json",
                table: "service_reports");

            migrationBuilder.DropColumn(
                name: "scheduled_end_at",
                table: "service_jobs");

            migrationBuilder.DropColumn(
                name: "scheduled_end_at",
                table: "service_job_history");

            migrationBuilder.DropColumn(
                name: "replacement_price_vnd",
                table: "public_content");

            migrationBuilder.DropColumn(
                name: "monthly_rent_vnd",
                table: "proposal_lines");

            migrationBuilder.DropColumn(
                name: "price_revision",
                table: "proposal_lines");

            migrationBuilder.DropColumn(
                name: "product_name",
                table: "proposal_lines");

            migrationBuilder.DropColumn(
                name: "public_content_id",
                table: "proposal_lines");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "installation_reports");

            migrationBuilder.DropColumn(
                name: "paperwork_json",
                table: "installation_reports");

            migrationBuilder.DropColumn(
                name: "scheduled_end_at",
                table: "installation_jobs");

            migrationBuilder.DropColumn(
                name: "scheduled_end_at",
                table: "installation_job_history");

            migrationBuilder.AlterColumn<int>(
                name: "rental_device_assignment_id",
                table: "device_incidents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
