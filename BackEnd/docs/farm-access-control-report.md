# Farm access control implementation report

Verified 2026-10-07 on BackEnd. The three human roles remain FarmAdministrator, FarmOwner and FarmEngineer. UavDeviceOperator has no runtime role references outside migration history. Human JWT and device headers remain separate.

## Schema and deployment

New migration: `20261007094620_AddUserFarmAssignments` (with generated designer and snapshot). `user_farms`: `id integer identity` primary key, `user_id integer NOT NULL`, `farm_id integer NOT NULL`, `created_at timestamp with time zone NOT NULL`. Both foreign keys use RESTRICT. Unique index `(user_id, farm_id)` and index `farm_id`. No farm-specific roles or automatic assignments for existing users. Prior migrations are unchanged.

Migration was applied and tested on isolated PostgreSQL databases, including upgrade from HumanUserRoles, preserved password hashes, no automatic memberships, FK/unique failures and repeat migration. The configured real database has NOT been migrated in this task. Existing owners/engineers need explicit assignment after deployment.

## APIs and permissions

Admin-only: GET/POST `/api/farms/{farmId}/members`, DELETE `/api/farms/{farmId}/members/{userId}`. POST accepts `{ "userId": 3 }`; GET returns paged members with roles/assignedAt; POST returns 201; DELETE returns 204. Invalid input400, missing404, duplicate409, ineligible target422. Only active Owner/Engineer without Administrator can be assigned. Removing a mapping leaves users, farms and roles intact and denies subsequent farm requests with the same JWT.

| Policy | Administrator | Owner | Engineer |
|---|---|---|---|
| ManageUsers | Yes | No | No |
| ManageFarms | Yes | Yes | No |
| ReadFarmData | Yes | Yes | Yes |
| ManageDevices | Yes | Yes | No |
| ConfigureThresholds | Yes | No | Yes |
| ManageMissions | Yes | Yes | No |

For farm resources, Administrator is global; Owner/Engineer additionally require current database membership. Owner creation saves farm and creator membership atomically; admin creation needs no assignment. Roles/memberships are not cached or copied into long-lived farm claims.

## Endpoint/service audit

| Surface | Access enforcement |
|---|---|
| Farms, zones | FarmService checks resource farm; farm list SQL-scoped before count/page |
| Sensor nodes/channels | SensorService checks parent farm, including original/destination zones on moves |
| Readings/history/latest/zone comparison | SensorDataService checks explicit filters and resources; repository scopes list before count/page |
| Threshold reads/writes | Channel-to-node-to-zone farm check plus existing role policies |
| Missions/details/results/waypoints/logs/attempts | Mission farm checked; list SQL-scoped; all mutation actions check farm; plan changes check source and destination |
| Human telemetry | Mission access check before latest/history |
| Alerts and actions | Channel then node then mission determines farm; unscoped alerts admin-only; lists SQL-scoped |
| Reports/dashboard | Membership restriction before aggregates/pagination; unauthorized explicit farm filter403 |
| Notifications | Recipient ownership retained; new alerts notify only admins and eligible farm members |
| Device sync/telemetry | Existing device scheme and mission/device validation; no user_farms dependency |
| Users/auth | Existing human role/token security preserved; login email/password only |
| Global catalogs | UAV/Gateway and sensor types retain existing role permissions |

Historical mission targets preserve identifiers/status, but current sensorName becomes null when a moved sensor belongs to an inaccessible farm. The regression test proves redaction, admin visibility and restoration after assignment.

UAV/Gateway entities are not directly farm-owned in the current schema. Direct per-farm device authorization requires a future explicit device-to-farm assignment model. Farm-filtered reports/dashboard include equipment through matching missions; this is association rather than ownership. No equipment farm_id was invented.

## Requirement and test evidence

Objective sections 1/19: entity/configuration/generated migration and FarmMembershipMigrationTests prove schema/upgrade. Sections 2/3/18/23: FarmAccessService, IFarmAccessRepository, ICurrentUser and SQL ForFarms implement current DB role plus assignment checks; controllers contain no EF. Sections 4–14: service/endpoint audit above and two-farm integration suite. Sections15–17: global catalog limitation, independent device tests and recipient ownership tests. Section20: HTTP status assertions. Section22: all six current documents updated; historical design docs preserved. Section24: commands/results below. Section25: this report and final response.

All numbered integration cases in objective section21 are covered in FarmScopeSecurityTests plus existing suites:

| Required cases | Test evidence |
|---|---|
| 1–10 membership | InitializeAsync uses admin HTTP assignment for both roles; Membership_management_checks_roles_eligibility_duplicates_missing_and_immediate_removal; Concurrent_membership_assignment_creates_one_row_and_preserves_global_roles; FarmAccessTests |
| 11–18 lists/details | All_farm_scoped_read_routes_deny_other_farm_and_lists_counts_exclude_it (Owner/Engineer theory); Empty_memberships_return_empty_farm_scoped_data_but_preserve_global_equipment_catalog; existing FarmZoneTests admin listing |
| 19–22 creation | Farm_creation_assigns_owner_atomically_and_admin_requires_no_membership; real PostgreSQL trigger failure proves rollback |
| 23–25 zones | Mutations_enforce_role_and_source_destination_farms; read-route theory |
| 26–29 sensors | Read-route theory tests list totals, details, history, explicit filters and mixed zone comparison |
| 30–32 thresholds | Mutations_enforce_role_and_source_destination_farms |
| 33–38 missions | Mutation and read-route tests, existing MissionTests lifecycle, HumanRoleTests role matrix |
| 39–41 alerts | Read-route theory and mutation tests; new recipient/private notification regression |
| 42–46 reports | Read-route theory checks exact counts/averages, explicit foreign filters and unfiltered scoped results |
| 47–49 device security | Devices_work_without_human_memberships_and_jwt_cannot_replace_headers removes all memberships, uploads valid telemetry/sync and rejects all three JWT-only roles |
| 50 isolation | Fresh two-farm fixtures with distinct zones/sensors/readings/missions/alerts and both role theory runs across all tested reads |

Existing integration fixtures now explicitly assign only required users/farms. Direct application tests supply a real database administrator identity. No production bypass or weakened security assertion was added.

## Verification

`./scripts/Test-Postgres.ps1 -PostgresBin 'D:\IDECode\PostgreSQL\18\bin' -Dotnet 'C:\Program Files\dotnet\dotnet.exe'`: restore succeeded, entire solution build succeeded, zero warnings/errors; **48 unit + 95 integration = 143 passing tests**, no skipped tests. Latest integration TRX: `tests/FarmMonitoring.IntegrationTests/TestResults/ductr_DLORDSK_2026-10-07_17_01_55_net10.0.trx`. Test runner uses isolated PostgreSQL and stops it afterward. Deliberate failure-path tests generate expected error logs.

`dotnet ef migrations has-pending-model-changes --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api --no-build`: no model changes since last migration. Idempotent SQL generated at `.local/farm-access-idempotent.sql`. `git diff --check` passed. Runtime deprecated-role search excluding migrations returned no matches. Independent review found the historical sensor-name leak; regression reproduced it before the fix, and re-review found no remaining important issue in that fix.

## Changed files

The following current working-tree inventory includes preserved changes from the preceding human-role implementation as well as this farm-access task; no commits were made.

- `AGENTS.md`
- `API.md`
- `ARCHITECTURE.md`
- `BUSINESS_RULES.md`
- `CODEX_MASTER_PROMPT.md`
- `DATABASE.md`
- `README.md`
- `src/FarmMonitoring.Api/Controllers/ReportsController.cs`
- `src/FarmMonitoring.Api/Extensions/ApiServiceExtensions.cs`
- `src/FarmMonitoring.Application/Features/Alerts/AlertService.cs`
- `src/FarmMonitoring.Application/Features/Auth/AuthService.cs`
- `src/FarmMonitoring.Application/Features/Farms/FarmService.cs`
- `src/FarmMonitoring.Application/Features/Missions/MissionContracts.cs`
- `src/FarmMonitoring.Application/Features/Missions/MissionService.cs`
- `src/FarmMonitoring.Application/Features/Reports/ReportContracts.cs`
- `src/FarmMonitoring.Application/Features/Reports/ReportService.cs`
- `src/FarmMonitoring.Application/Features/SensorData/SensorDataService.cs`
- `src/FarmMonitoring.Application/Features/Sensors/SensorService.cs`
- `src/FarmMonitoring.Application/Features/Telemetry/TelemetryService.cs`
- `src/FarmMonitoring.Application/Features/Thresholds/ThresholdService.cs`
- `src/FarmMonitoring.Application/Features/Users/UserValidators.cs`
- `src/FarmMonitoring.Application/Interfaces/IAlertRepository.cs`
- `src/FarmMonitoring.Application/Interfaces/IFarmRepository.cs`
- `src/FarmMonitoring.Application/Interfaces/IMissionRepository.cs`
- `src/FarmMonitoring.Application/Interfaces/IReportRepository.cs`
- `src/FarmMonitoring.Application/Interfaces/ISensorDataRepository.cs`
- `src/FarmMonitoring.Application/Interfaces/ISensorRepository.cs`
- `src/FarmMonitoring.Domain/Constants/RoleNames.cs`
- `src/FarmMonitoring.Domain/Entities/Farm.cs`
- `src/FarmMonitoring.Domain/Entities/User.cs`
- `src/FarmMonitoring.Infrastructure/Authentication/JwtTokenService.cs`
- `src/FarmMonitoring.Infrastructure/DependencyInjection.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/AppDbContext.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/AlertRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/AuthRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/FarmRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/MissionRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/ReportRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/SensorDataRepository.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/SensorRepository.cs`
- `tests/FarmMonitoring.IntegrationTests/AccountLifecycleTests.cs`
- `tests/FarmMonitoring.IntegrationTests/AdminSeedTests.cs`
- `tests/FarmMonitoring.IntegrationTests/AlertTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ApiFactory.cs`
- `tests/FarmMonitoring.IntegrationTests/AuthSecurityTests.cs`
- `tests/FarmMonitoring.IntegrationTests/EquipmentTests.cs`
- `tests/FarmMonitoring.IntegrationTests/FarmZoneTests.cs`
- `tests/FarmMonitoring.IntegrationTests/MissionTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ReportTests.cs`
- `tests/FarmMonitoring.IntegrationTests/SensorDataIntegrityTests.cs`
- `tests/FarmMonitoring.IntegrationTests/SensorManagementTests.cs`
- `tests/FarmMonitoring.IntegrationTests/SyncTests.cs`
- `tests/FarmMonitoring.IntegrationTests/TelemetryTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ThresholdTests.cs`
- `tests/FarmMonitoring.IntegrationTests/UserManagementTests.cs`
- `tests/FarmMonitoring.UnitTests/UserValidationTests.cs`
- `docs/human-role-model-report.md`
- `docs/superpowers/plans/2026-10-07-farm-access-control.md`
- `docs/superpowers/plans/2026-10-07-human-role-model.md`
- `src/FarmMonitoring.Api/Authorization/CurrentUser.cs`
- `src/FarmMonitoring.Api/Controllers/FarmMembersController.cs`
- `src/FarmMonitoring.Application/Features/Farms/FarmAccessService.cs`
- `src/FarmMonitoring.Application/Features/Farms/FarmMembershipService.cs`
- `src/FarmMonitoring.Application/Interfaces/ICurrentUser.cs`
- `src/FarmMonitoring.Application/Interfaces/IFarmAccessRepository.cs`
- `src/FarmMonitoring.Domain/Entities/UserFarm.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Configurations/UserFarmConfiguration.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/FarmAccessQuery.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261007041338_HumanUserRoles.Designer.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261007041338_HumanUserRoles.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261007094620_AddUserFarmAssignments.Designer.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261007094620_AddUserFarmAssignments.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/FarmAccessRepository.cs`
- `tests/FarmMonitoring.IntegrationTests/FarmAccessTests.cs`
- `tests/FarmMonitoring.IntegrationTests/FarmMembershipMigrationTests.cs`
- `tests/FarmMonitoring.IntegrationTests/FarmScopeSecurityTests.cs`
- `tests/FarmMonitoring.IntegrationTests/HumanRoleTests.cs`
- `tests/FarmMonitoring.IntegrationTests/RoleMigrationTests.cs`
- `docs/farm-access-control-report.md`
