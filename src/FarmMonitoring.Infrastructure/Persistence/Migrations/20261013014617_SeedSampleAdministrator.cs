using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations;

public partial class SeedSampleAdministrator : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Identity V3 hash of the explicitly requested sample password (123456).
        // Grant only to the newly inserted account; never promote an existing email.
        migrationBuilder.Sql("""
            WITH inserted_user AS (
                INSERT INTO users (email, password_hash, full_name, is_active, created_at)
                VALUES ('admin@sep.local',
                    'AQAAAAIAAYagAAAAECxWbNUvGto7JqfSBXrfDu64uuCjgolV0oJoG3g3JYqdDzFrElz8Cw6Uo4mJiNwEqQ==',
                    'Sample Administrator', TRUE, CURRENT_TIMESTAMP)
                ON CONFLICT (email) DO NOTHING
                RETURNING id
            )
            INSERT INTO user_roles (user_id, role_id)
            SELECT u.id, r.id FROM inserted_user u
            CROSS JOIN roles r WHERE r.name = 'FarmAdministrator';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve the account and any data or password changes made after seeding.
        // Reapplying Up skips the existing email without resetting credentials.
    }
}
