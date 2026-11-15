using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FarmMonitoring.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261115090000_ConsultationIntakeCropType")]
public sealed class ConsultationIntakeCropType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "crop_type", table: "consultations",
            type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "preferred_channel", table: "consultations",
            type: "character varying(10)", maxLength: 10, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "garden_address", table: "site_visit_proposals",
            type: "character varying(500)", maxLength: 500, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "garden_address", table: "site_visit_proposals");
        migrationBuilder.DropColumn(name: "preferred_channel", table: "consultations");
        migrationBuilder.DropColumn(name: "crop_type", table: "consultations");
    }
}
