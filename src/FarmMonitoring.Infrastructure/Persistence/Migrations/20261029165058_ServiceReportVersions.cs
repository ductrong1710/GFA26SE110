using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceReportVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.AddColumn<int>(
                name: "service_report_id",
                table: "evidence_images",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    technician_id = table.Column<int>(type: "integer", nullable: false),
                    cause = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    work = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    components = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    test_results = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    connectivity_passed = table.Column<bool>(type: "boolean", nullable: false),
                    operation_passed = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_service_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_reports_service_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "service_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_reports_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_reports_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_reports_users_technician_id",
                        column: x => x.technician_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_service_report_id",
                table: "evidence_images",
                column: "service_report_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id, service_report_id) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_service_reports_confirmed_by_user_id",
                table: "service_reports",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_reports_job_id_revision",
                table: "service_reports",
                columns: new[] { "job_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_reports_reviewed_by_user_id",
                table: "service_reports",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_reports_technician_id",
                table: "service_reports",
                column: "technician_id");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_images_service_reports_service_report_id",
                table: "evidence_images",
                column: "service_report_id",
                principalTable: "service_reports",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_images_service_reports_service_report_id",
                table: "evidence_images");

            migrationBuilder.DropTable(
                name: "service_reports");

            migrationBuilder.DropIndex(
                name: "IX_evidence_images_service_report_id",
                table: "evidence_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.DropColumn(
                name: "service_report_id",
                table: "evidence_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id, incident_id) = 1");
        }
    }
}
