using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MissionNoteEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.AddColumn<int>(
                name: "mission_log_id",
                table: "evidence_images",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_mission_log_id",
                table: "evidence_images",
                column: "mission_log_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id, mission_log_id) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_evidence_images_mission_logs_mission_log_id",
                table: "evidence_images",
                column: "mission_log_id",
                principalTable: "mission_logs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_evidence_images_mission_logs_mission_log_id",
                table: "evidence_images");

            migrationBuilder.DropIndex(
                name: "IX_evidence_images_mission_log_id",
                table: "evidence_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images");

            migrationBuilder.DropColumn(
                name: "mission_log_id",
                table: "evidence_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_image_parent",
                table: "evidence_images",
                sql: "num_nonnulls(survey_id, report_id) = 1");
        }
    }
}
