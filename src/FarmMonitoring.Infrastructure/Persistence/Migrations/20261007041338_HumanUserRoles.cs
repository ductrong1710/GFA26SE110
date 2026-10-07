using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmMonitoring.Infrastructure.Persistence.Migrations;

public partial class HumanUserRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Resolve by name: existing installations may use different role IDs.
        // Remove grants without changing users, passwords, or assigning replacement roles.
        migrationBuilder.Sql("""
            DELETE FROM user_roles ur USING roles r
            WHERE ur.role_id = r.id AND r.name = 'UavDeviceOperator';
            DELETE FROM roles WHERE name = 'UavDeviceOperator';
            SELECT setval(pg_get_serial_sequence('roles', 'id'),
                GREATEST(2, COALESCE((SELECT MAX(id) FROM roles), 0)), true);
            INSERT INTO roles (name) VALUES ('FarmOwner'), ('FarmEngineer')
            ON CONFLICT (name) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A rollback restores the old role definition, never previously removed grants.
        migrationBuilder.Sql("""
            DELETE FROM user_roles ur USING roles r
            WHERE ur.role_id = r.id AND r.name IN ('FarmOwner', 'FarmEngineer');
            DELETE FROM roles WHERE name IN ('FarmOwner', 'FarmEngineer');
            SELECT setval(pg_get_serial_sequence('roles', 'id'),
                GREATEST(1, COALESCE((SELECT MAX(id) FROM roles), 0)), true);
            INSERT INTO roles (name) VALUES ('UavDeviceOperator')
            ON CONFLICT (name) DO NOTHING;
            """);
    }
}
