using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowEvidenceImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    survey_id = table.Column<int>(type: "integer", nullable: true),
                    report_id = table.Column<int>(type: "integer", nullable: true),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_images", x => x.id);
                    table.CheckConstraint("ck_evidence_image_parent", "num_nonnulls(survey_id, report_id) = 1");
                    table.CheckConstraint("ck_evidence_image_size", "octet_length(content) BETWEEN 4 AND 2097152");
                    table.CheckConstraint("ck_evidence_image_type", "content_type IN ('image/png', 'image/jpeg')");
                    table.ForeignKey(
                        name: "FK_evidence_images_farm_surveys_survey_id",
                        column: x => x.survey_id,
                        principalTable: "farm_surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evidence_images_installation_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "installation_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_report_id",
                table: "evidence_images",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_images_survey_id",
                table: "evidence_images",
                column: "survey_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_images");
        }
    }
}
