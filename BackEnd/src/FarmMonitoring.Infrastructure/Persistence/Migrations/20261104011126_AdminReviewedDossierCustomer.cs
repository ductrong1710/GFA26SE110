using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminReviewedDossierCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dossier_customer_email",
                table: "device_proposals",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dossier_customer_name",
                table: "device_proposals",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM device_proposals WHERE dossier_customer_email IS NOT NULL OR dossier_customer_name IS NOT NULL) THEN
                    RAISE EXCEPTION 'Downgrade would discard submitted customer details.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "dossier_customer_email",
                table: "device_proposals");

            migrationBuilder.DropColumn(
                name: "dossier_customer_name",
                table: "device_proposals");
        }
    }
}
