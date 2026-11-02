using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsultationCustomerFarm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "farm_id",
                table: "consultations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "owner_id",
                table: "consultations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_consultations_farm_id",
                table: "consultations",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_owner_id_farm_id",
                table: "consultations",
                columns: new[] { "owner_id", "farm_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_consultations_farms_farm_id",
                table: "consultations",
                column: "farm_id",
                principalTable: "farms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consultations_users_owner_id",
                table: "consultations",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_consultations_farms_farm_id",
                table: "consultations");

            migrationBuilder.DropForeignKey(
                name: "FK_consultations_users_owner_id",
                table: "consultations");

            migrationBuilder.DropIndex(
                name: "IX_consultations_farm_id",
                table: "consultations");

            migrationBuilder.DropIndex(
                name: "IX_consultations_owner_id_farm_id",
                table: "consultations");

            migrationBuilder.DropColumn(
                name: "farm_id",
                table: "consultations");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "consultations");
        }
    }
}
