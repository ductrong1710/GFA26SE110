using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsultationSiteVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "channel",
                table: "consultations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "site_visit_proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConsultationId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    OfficeCode = table.Column<string>(type: "text", nullable: true),
                    OfficeLabel = table.Column<string>(type: "text", nullable: true),
                    DistanceKm = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    MapsUrl = table.Column<string>(type: "text", nullable: true),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    PricingMethod = table.Column<string>(type: "text", nullable: false),
                    PricingVersion = table.Column<string>(type: "text", nullable: true),
                    ManualReason = table.Column<string>(type: "text", nullable: true),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewStatus = table.Column<string>(type: "text", nullable: false),
                    ReviewReason = table.Column<string>(type: "text", nullable: true),
                    ReviewedBy = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledBy = table.Column<int>(type: "integer", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_visit_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_visit_proposals_consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_visit_proposals_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_visit_proposals_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_visit_proposals_users_ScheduledBy",
                        column: x => x.ScheduledBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_visit_payments",
                columns: table => new
                {
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    PaidAmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerifiedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_visit_payments", x => x.ProposalId);
                    table.ForeignKey(
                        name: "FK_site_visit_payments_site_visit_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "site_visit_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_visit_payments_users_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_visit_bills",
                columns: table => new
                {
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    ImagesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_visit_bills", x => x.ProposalId);
                    table.ForeignKey(
                        name: "FK_site_visit_bills_site_visit_payments_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "site_visit_payments",
                        principalColumn: "ProposalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_payments_VerifiedBy",
                table: "site_visit_payments",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_proposals_ConsultationId_Revision",
                table: "site_visit_proposals",
                columns: new[] { "ConsultationId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_proposals_CreatedBy",
                table: "site_visit_proposals",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_proposals_ReviewedBy",
                table: "site_visit_proposals",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_proposals_ScheduledBy",
                table: "site_visit_proposals",
                column: "ScheduledBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_visit_bills");

            migrationBuilder.DropTable(
                name: "site_visit_payments");

            migrationBuilder.DropTable(
                name: "site_visit_proposals");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "consultations");
        }
    }
}
