using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceProposalRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_proposals",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    consultation_id = table.Column<int>(type: "integer", nullable: false),
                    survey_id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    rental_months = table.Column<int>(type: "integer", nullable: false),
                    requested_installation_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    plan_confirmed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    plan_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    plan_confirmation_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deposit_confirmed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    deposit_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deposit_confirmation_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_proposals_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_proposals_farm_surveys_survey_id",
                        column: x => x.survey_id,
                        principalTable: "farm_surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_proposals_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_proposals_users_deposit_confirmed_by_user_id",
                        column: x => x.deposit_confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_device_proposals_users_plan_confirmed_by_user_id",
                        column: x => x.plan_confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installation_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    farm_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    submitted_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installation_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_installation_requests_device_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "device_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_requests_farms_farm_id",
                        column: x => x.farm_id,
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_requests_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installation_requests_users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_lines",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proposal_id = table.Column<int>(type: "integer", nullable: false),
                    survey_zone_id = table.Column<int>(type: "integer", nullable: false),
                    device_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    installation_location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_proposal_lines_device_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "device_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposal_lines_survey_zones_survey_zone_id",
                        column: x => x.survey_zone_id,
                        principalTable: "survey_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_proposals_consultation_id_revision",
                table: "device_proposals",
                columns: new[] { "consultation_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_proposals_created_by_user_id",
                table: "device_proposals",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_proposals_deposit_confirmed_by_user_id",
                table: "device_proposals",
                column: "deposit_confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_proposals_plan_confirmed_by_user_id",
                table: "device_proposals",
                column: "plan_confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_proposals_survey_id",
                table: "device_proposals",
                column: "survey_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_requests_farm_id",
                table: "installation_requests",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_requests_owner_id",
                table: "installation_requests",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_installation_requests_proposal_id",
                table: "installation_requests",
                column: "proposal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installation_requests_status_submitted_at",
                table: "installation_requests",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_installation_requests_submitted_by_user_id",
                table: "installation_requests",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_lines_proposal_id",
                table: "proposal_lines",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_lines_survey_zone_id",
                table: "proposal_lines",
                column: "survey_zone_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installation_requests");

            migrationBuilder.DropTable(
                name: "proposal_lines");

            migrationBuilder.DropTable(
                name: "device_proposals");
        }
    }
}
