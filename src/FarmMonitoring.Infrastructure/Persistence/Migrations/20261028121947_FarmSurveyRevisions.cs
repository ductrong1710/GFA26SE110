using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FarmSurveyRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "farm_surveys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    consultation_id = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    engineer_id = table.Column<int>(type: "integer", nullable: false),
                    farm_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    farm_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    farm_area = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    monitoring_needs = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farm_surveys", x => x.id);
                    table.ForeignKey(
                        name: "FK_farm_surveys_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_farm_surveys_users_engineer_id",
                        column: x => x.engineer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_zones",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    survey_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    crop = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    crop_age_days = table.Column<int>(type: "integer", nullable: true),
                    symptoms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    installation_location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_survey_zones_farm_surveys_survey_id",
                        column: x => x.survey_id,
                        principalTable: "farm_surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_farm_surveys_consultation_id_revision",
                table: "farm_surveys",
                columns: new[] { "consultation_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_farm_surveys_engineer_id",
                table: "farm_surveys",
                column: "engineer_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_zones_survey_id_name",
                table: "survey_zones",
                columns: new[] { "survey_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_zones");

            migrationBuilder.DropTable(
                name: "farm_surveys");
        }
    }
}
