using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace FarmMonitoring.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261115100000_SiteVisitOffices")]
public sealed class SiteVisitOffices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "site_visit_offices",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                maps_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_site_visit_offices", row => row.id));
        migrationBuilder.AddColumn<string>(
            name: "office_maps_url", table: "site_visit_proposals",
            type: "character varying(2048)", maxLength: 2048, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "office_maps_url", table: "site_visit_proposals");
        migrationBuilder.DropTable(name: "site_visit_offices");
    }
}
