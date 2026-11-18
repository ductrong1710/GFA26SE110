using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteVisitPayOsPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PayOsTransactionId",
                table: "site_visit_payments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payos_transactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    PaymentLinkId = table.Column<string>(type: "text", nullable: true),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    CheckoutUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    QrCode = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderPaymentLinkId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProviderAmountVnd = table.Column<long>(type: "bigint", nullable: true),
                    ProviderTransactionDateTime = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payos_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payos_transactions_site_visit_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "site_visit_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payos_transactions_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_payments_PayOsTransactionId",
                table: "site_visit_payments",
                column: "PayOsTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payos_transactions_CreatedBy",
                table: "payos_transactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_payos_transactions_OrderCode",
                table: "payos_transactions",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payos_transactions_PaymentLinkId",
                table: "payos_transactions",
                column: "PaymentLinkId",
                unique: true,
                filter: "\"PaymentLinkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payos_transactions_ProposalId",
                table: "payos_transactions",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_payos_transactions_ProposalId_Status",
                table: "payos_transactions",
                columns: new[] { "ProposalId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_site_visit_payments_payos_transactions_PayOsTransactionId",
                table: "site_visit_payments",
                column: "PayOsTransactionId",
                principalTable: "payos_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_site_visit_payments_payos_transactions_PayOsTransactionId",
                table: "site_visit_payments");

            migrationBuilder.DropTable(
                name: "payos_transactions");

            migrationBuilder.DropIndex(
                name: "IX_site_visit_payments_PayOsTransactionId",
                table: "site_visit_payments");

            migrationBuilder.DropColumn(
                name: "PayOsTransactionId",
                table: "site_visit_payments");
        }
    }
}
