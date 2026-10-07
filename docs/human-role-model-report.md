# Human role model change — 2026-10-07

Implemented on BackEnd. The only human account roles are FarmAdministrator, FarmOwner and FarmEngineer. UavDeviceOperator is no longer supported by runtime role validation, authentication, token claims or authorization policies.

## Authorization

| Policy | FarmAdministrator | FarmOwner | FarmEngineer |
|---|---|---|---|
| ManageUsers | Yes | No | No |
| ManageFarms | Yes | Yes | No |
| ReadFarmData | Yes | Yes | Yes |
| ManageDevices | Yes | Yes | No |
| ConfigureThresholds | Yes | No | Yes |
| ManageMissions | Yes | Yes | No |

All existing controller policy assignments were reviewed. Sensor reports now use ReadFarmData, and alert reports no longer apply the obsolete operator-only category filter. Alert recipients include all three supported roles; private notifications retain recipient ownership checks.

Human users authenticate using JWT Bearer with email/password login. Database roles determine login/me/JWT roles. Current-role policy checks prevent stale JWT claims from retaining removed privileges. Users with no supported role cannot log in, refresh or get /api/auth/me. Refresh rotation and hash-only storage remain intact.

Device Authentication continues independently with X-Api-Key + X-Gateway-Code. Tests verify telemetry and offline synchronization reject JWT-only requests for each human role, accept valid device headers, and do not let device credentials access human farm endpoints. DevelopmentAdminSeeder remains administrator-only and its environment-variable bootstrap is unchanged.

## Migration

New migration: `20261007041338_HumanUserRoles` (plus generated designer and model snapshot).

It deletes deprecated user_roles assignments and the deprecated role by name, then inserts missing FarmOwner/FarmEngineer records. It preserves users, password hashes and existing supported memberships; no user is automatically assigned a replacement role. Tests cover the standard old database and a database with a different deprecated-role ID and preexisting target roles. Removed grants cannot be restored by rollback.

Historical migrations are unchanged. The migration was applied successfully on isolated PostgreSQL test databases, including upgrades with existing accounts. It has NOT been applied to the normal development/application database in this task. Apply it with the existing configured connection using:

```powershell
dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
```

## Verification

- Full solution restore/build succeeded: 0 warnings, 0 errors.
- 48 unit tests and 83 integration tests passed (131 total); no skipped tests.
- New HumanRoleTests cover login/me/JWT, all six authorization policies, deprecated/unknown role rejection, role removal and the device authentication boundary.
- New RoleMigrationTests cover both upgrade scenarios, preserved passwords/accounts/grants, denied legacy login/refresh and repeat migration application.
- UserValidationTests cover all three accepted role names and deprecated/unknown rejection; existing uniqueness/email/password rules and security tests remain.
- Existing integration tests now use Owner/Engineer according to their intended privilege boundary; report tests verify expanded read permissions.
- EF reports no pending model changes; idempotent SQL migration generation succeeded.
- Repository scan found deprecated-role names only in migration cleanup/rollback, historical migration/design records, migration documentation and negative regression tests; no remaining active runtime role references.
- Independent read-only code review found no critical or important findings; a stale test name was corrected.

## Remaining limitation

FarmOwner and FarmEngineer permissions are currently role-based globally. Per-farm authorization requires a future user-to-farm membership/ownership model.

No ownership/membership tables or unrelated APIs were added. External UAV Operator actors and operational `operatorNotes` fields remain valid concepts, not login roles. Current documentation was updated; historical design records were preserved.

## Files changed

- `AGENTS.md`
- `API.md`
- `ARCHITECTURE.md`
- `BUSINESS_RULES.md`
- `CODEX_MASTER_PROMPT.md`
- `DATABASE.md`
- `docs/human-role-model-report.md`
- `docs/superpowers/plans/2026-10-07-human-role-model.md`
- `README.md`
- `src/FarmMonitoring.Api/Controllers/ReportsController.cs`
- `src/FarmMonitoring.Api/Extensions/ApiServiceExtensions.cs`
- `src/FarmMonitoring.Application/Features/Auth/AuthService.cs`
- `src/FarmMonitoring.Application/Features/Reports/ReportService.cs`
- `src/FarmMonitoring.Application/Features/Users/UserValidators.cs`
- `src/FarmMonitoring.Application/Interfaces/IReportRepository.cs`
- `src/FarmMonitoring.Domain/Constants/RoleNames.cs`
- `src/FarmMonitoring.Infrastructure/Authentication/JwtTokenService.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261007041338_HumanUserRoles.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261007041338_HumanUserRoles.Designer.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/AlertRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/AuthRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/ReportRepository.cs`
- `tests/FarmMonitoring.IntegrationTests/AccountLifecycleTests.cs`
- `tests/FarmMonitoring.IntegrationTests/AdminSeedTests.cs`
- `tests/FarmMonitoring.IntegrationTests/AlertTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ApiFactory.cs`
- `tests/FarmMonitoring.IntegrationTests/AuthSecurityTests.cs`
- `tests/FarmMonitoring.IntegrationTests/EquipmentTests.cs`
- `tests/FarmMonitoring.IntegrationTests/FarmZoneTests.cs`
- `tests/FarmMonitoring.IntegrationTests/HumanRoleTests.cs`
- `tests/FarmMonitoring.IntegrationTests/MissionTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ReportTests.cs`
- `tests/FarmMonitoring.IntegrationTests/RoleMigrationTests.cs`
- `tests/FarmMonitoring.IntegrationTests/SensorManagementTests.cs`
- `tests/FarmMonitoring.IntegrationTests/SyncTests.cs`
- `tests/FarmMonitoring.IntegrationTests/TelemetryTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ThresholdTests.cs`
- `tests/FarmMonitoring.IntegrationTests/UserManagementTests.cs`
- `tests/FarmMonitoring.UnitTests/UserValidationTests.cs`
