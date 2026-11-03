using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalConsultationDossier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deposit_bills_json",
                table: "device_proposals",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dossier_review_note",
                table: "device_proposals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dossier_submitted_at",
                table: "device_proposals",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM device_proposals WHERE deposit_bills_json IS NOT NULL OR dossier_submitted_at IS NOT NULL OR dossier_review_note IS NOT NULL)
                    THEN RAISE EXCEPTION 'Cannot discard retained installation dossier evidence.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "deposit_bills_json",
                table: "device_proposals");

            migrationBuilder.DropColumn(
                name: "dossier_review_note",
                table: "device_proposals");

            migrationBuilder.DropColumn(
                name: "dossier_submitted_at",
                table: "device_proposals");
        }
    }
}
