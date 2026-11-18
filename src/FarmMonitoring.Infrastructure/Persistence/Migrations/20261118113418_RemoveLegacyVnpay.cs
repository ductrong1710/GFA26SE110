using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyVnpay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM site_visit_bills AS bill
                USING site_visit_payments AS payment
                WHERE bill."ProposalId" = payment."ProposalId"
                    AND payment."PayOsTransactionId" IS NULL
                    AND (payment."Source" = 'VNPAY' OR payment."VnpayTxnRef" IS NOT NULL);

                DELETE FROM site_visit_payments
                WHERE "PayOsTransactionId" IS NULL
                    AND ("Source" = 'VNPAY' OR "VnpayTxnRef" IS NOT NULL);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_site_visit_payments_vnpay_transactions_VnpayTxnRef",
                table: "site_visit_payments");

            migrationBuilder.DropTable(
                name: "vnpay_transactions");

            migrationBuilder.DropIndex(
                name: "IX_site_visit_payments_VnpayTxnRef",
                table: "site_visit_payments");

            migrationBuilder.DropColumn(
                name: "VnpayTxnRef",
                table: "site_visit_payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VnpayTxnRef",
                table: "site_visit_payments",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vnpay_transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: false),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    ProviderTransactionNo = table.Column<string>(type: "text", nullable: true),
                    PublicToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseCode = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TmnCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TransactionStatus = table.Column<string>(type: "text", nullable: true),
                    TxnRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vnpay_transactions", x => x.Id);
                    table.UniqueConstraint("AK_vnpay_transactions_TxnRef", x => x.TxnRef);
                    table.ForeignKey(
                        name: "FK_vnpay_transactions_site_visit_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "site_visit_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vnpay_transactions_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_visit_payments_VnpayTxnRef",
                table: "site_visit_payments",
                column: "VnpayTxnRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vnpay_transactions_CreatedBy",
                table: "vnpay_transactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_vnpay_transactions_ProposalId",
                table: "vnpay_transactions",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_vnpay_transactions_PublicToken",
                table: "vnpay_transactions",
                column: "PublicToken",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_site_visit_payments_vnpay_transactions_VnpayTxnRef",
                table: "site_visit_payments",
                column: "VnpayTxnRef",
                principalTable: "vnpay_transactions",
                principalColumn: "TxnRef",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
