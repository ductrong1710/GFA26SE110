using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeographicBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "boundary_geo_json",
                table: "zones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "boundary_geo_json",
                table: "farms",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "boundary_geo_json",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "boundary_geo_json",
                table: "farms");
        }
    }
}
