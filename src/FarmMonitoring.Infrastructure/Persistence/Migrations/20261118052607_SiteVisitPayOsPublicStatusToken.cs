using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteVisitPayOsPublicStatusToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicStatusToken",
                table: "payos_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payos_transactions_PublicStatusToken",
                table: "payos_transactions",
                column: "PublicStatusToken",
                unique: true,
                filter: "\"PublicStatusToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payos_transactions_PublicStatusToken",
                table: "payos_transactions");

            migrationBuilder.DropColumn(
                name: "PublicStatusToken",
                table: "payos_transactions");
        }
    }
}
