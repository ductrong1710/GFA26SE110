using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedOperationalRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve retired role assignments for audit; current role validation denies this role.

            migrationBuilder.Sql("INSERT INTO roles (id, name) VALUES (6, 'Technician') ON CONFLICT (id) DO NOTHING");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never discard a historical assignment during rollback.
            migrationBuilder.Sql("DELETE FROM roles WHERE id = 6 AND NOT EXISTS (SELECT 1 FROM user_roles WHERE role_id = 6)");
        }
    }
}
