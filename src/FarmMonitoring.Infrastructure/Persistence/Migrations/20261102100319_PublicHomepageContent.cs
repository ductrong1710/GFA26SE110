using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicHomepageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "public_content",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    estimated_monthly_rent_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    image_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    image = table.Column<byte[]>(type: "bytea", nullable: true),
                    published = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_content", x => x.id);
                    table.CheckConstraint("ck_public_content_image", "(image IS NULL AND image_type IS NULL) OR (image IS NOT NULL AND image_type IS NOT NULL AND image_type IN ('image/png', 'image/jpeg') AND octet_length(image) BETWEEN 1 AND 2097152)");
                    table.CheckConstraint("ck_public_content_rent", "estimated_monthly_rent_vnd IS NULL OR (kind = 'DEVICE' AND estimated_monthly_rent_vnd >= 0)");
                    table.CheckConstraint("ck_public_content_revision", "revision > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_public_content_published_id",
                table: "public_content",
                columns: new[] { "published", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "public_content");
        }
    }
}
