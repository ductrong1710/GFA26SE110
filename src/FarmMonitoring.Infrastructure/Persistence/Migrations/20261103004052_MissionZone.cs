using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MissionZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "zone_id",
                table: "missions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_missions_zone_id",
                table: "missions",
                column: "zone_id");

            migrationBuilder.AddForeignKey(
                name: "FK_missions_zones_zone_id",
                table: "missions",
                column: "zone_id",
                principalTable: "zones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_missions_zones_zone_id",
                table: "missions");

            migrationBuilder.DropIndex(
                name: "IX_missions_zone_id",
                table: "missions");

            migrationBuilder.DropColumn(
                name: "zone_id",
                table: "missions");
        }
    }
}
