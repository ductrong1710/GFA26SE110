# Farm Monitoring backend

ASP.NET Core backend for authentication/users, farms/zones, sensors, UAVs/gateways, missions, telemetry, offline synchronization, thresholds, alerts/notifications and reports. Current contracts are maintained in AGENTS.md, ARCHITECTURE.md, DATABASE.md, API.md and BUSINESS_RULES.md.

## Structure

```text
src/
  FarmMonitoring.Domain/          Entities and role constants
  FarmMonitoring.Application/     Business services, DTOs, validators, interfaces
  FarmMonitoring.Infrastructure/  PostgreSQL, EF mappings/migrations, JWT, hashing, seed
  FarmMonitoring.Api/             Controllers, middleware, configuration, Swagger
tests/
  FarmMonitoring.UnitTests/
  FarmMonitoring.IntegrationTests/
scripts/Test-Postgres.ps1          Isolated Windows PostgreSQL test runner
```

Domain has no external dependencies. Application references Domain. Infrastructure implements Application interfaces. API uses Infrastructure only for startup/composition.

## Prerequisites

- .NET 10 SDK (`global.json` allows installed .NET 10 feature bands).
- PostgreSQL (verified with PostgreSQL 18).
- PowerShell 7 for the optional Windows test-cluster script.

On this workstation the SDK is installed at `C:\Program Files\dotnet\dotnet.exe`. If `dotnet` is not found, add that directory to the current shell's PATH:

```powershell
$env:PATH = 'C:\Program Files\dotnet;' + $env:PATH
Set-Location D:\Learning\SEP\Code\backend
dotnet restore
dotnet tool restore
```

## PostgreSQL and secrets

Create a PostgreSQL database owned by your development account. Use that account for migration commands; no database or password is hardcoded in the application. Supply the connection through configuration:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=<host>;Port=<port>;Database=<database>;Username=<user>;Password=<password>'
$env:Jwt__SecretKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

Replace the connection placeholders with your local values. Generate the signing key once per development environment and keep it stable between restarts; changing it invalidates existing access tokens. Do not print or commit these values. Environment variables override the JSON configuration.

Alternatively, use .NET user secrets for API runtime configuration (`dotnet user-secrets set <key> <value> --project src/FarmMonitoring.Api`). Keys are `ConnectionStrings:DefaultConnection`, `Jwt:SecretKey`, `SEED_ADMIN_EMAIL`, and `SEED_ADMIN_PASSWORD`. EF's design-time factory deliberately reads **ConnectionStrings__DefaultConnection** from the environment, so set that variable when running migrations even if the API uses user secrets.

| Setting | Required/default |
|---|---|
| `ConnectionStrings__DefaultConnection` | Required connection with Host and Database; appropriate credentials for your server |
| `Jwt__SecretKey` | Required; random key, at least 32 UTF-8 bytes |
| `Jwt__Issuer` | `FarmMonitoring.Api` |
| `Jwt__Audience` | `FarmMonitoring.Web` |
| `Jwt__AccessTokenExpirationMinutes` | `15`, accepted range 1–60 |
| `Jwt__RefreshTokenExpirationDays` | `7`, accepted range 1–90 |
| `ASPNETCORE_ENVIRONMENT` | `Development` to enable Swagger and optional seed |
| `SEED_ADMIN_EMAIL` | Optional; supply together with seed password |
| `SEED_ADMIN_PASSWORD` | Optional; 12–1024 characters; no default |

Missing/invalid required configuration fails startup. Production configuration should come from deployment secrets. The blank secret and connection values in appsettings.json are intentional.

## Migration and initial administrator

```powershell
dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
```

Apply the full migration chain for the current schema. The role-model migration `20261007041338_HumanUserRoles` removes deprecated `UavDeviceOperator` memberships and that role, and inserts missing FarmOwner/FarmEngineer roles. It preserves users, passwords and any existing supported role assignments; it never converts former operators into owners or engineers. Accounts left without a supported role require an administrator to assign a role before login/refresh/me can succeed. Rollback cannot recover removed assignments. Historical migrations are unchanged. Migrations contain no account passwords or JWT secrets.

To seed the first administrator, set both `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD` via environment variables or user secrets, then start the API in Development. The seed hashes the password, normalizes email, and assigns FarmAdministrator. It runs only when no administrator exists; it never changes an existing account's password or promotes an existing email. It creates no FarmOwner or FarmEngineer accounts. Clear the two seed settings afterward. Database migrations must be applied before running the seed. Normal startup does not apply migrations automatically.

## Run

```powershell
dotnet build --no-restore
dotnet dev-certs https --trust
dotnet run --project src/FarmMonitoring.Api --launch-profile https
```

Open [Swagger](https://localhost:7043/swagger). API HTTPS address: `https://localhost:7043`. The HTTP listener on port 5043 redirects to HTTPS. Swagger is enabled only in Development.

## Auth endpoints and Swagger walkthrough

| Method | Route | Authentication | Request |
|---|---|---|---|
| POST | `/api/auth/login` | Anonymous | `email`, `password` |
| POST | `/api/auth/refresh` | Anonymous | `refreshToken` |
| POST | `/api/auth/logout` | Bearer JWT | `refreshToken`, owned by current user |
| GET | `/api/auth/me` | Bearer JWT | None |

1. Execute login with the seeded email/password.
2. Copy `data.accessToken` into Swagger's **Authorize** dialog. Paste only the JWT; the HTTP bearer scheme adds `Bearer `.
3. Execute `/api/auth/me` to retrieve the current user.
4. Call refresh with `data.refreshToken`. Store the new token pair, replacing the previous pair. The old refresh token now returns 401.
5. Authorize with the new JWT and call logout with the latest refresh token. That refresh token can no longer refresh.

Example login request (replace the password with the seeded value):

```json
{
  "email": "admin@example.com",
  "password": "<your-seeded-password>"
}
```

Example response (illustrative tokens and timestamp):

```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt>",
    "refreshToken": "<random-opaque-token>",
    "accessTokenExpiresAt": "2026-09-18T09:15:00+00:00",
    "expiresIn": 900,
    "user": {
      "id": 1,
      "email": "admin@example.com",
      "fullName": "Development Administrator",
      "roles": ["FarmAdministrator"]
    }
  },
  "message": null
}
```

Refresh and logout request:

```json
{ "refreshToken": "<latest-refresh-token>" }
```

Validation errors return 400, invalid credentials/tokens return 401, and insufficient role returns 403. Errors use `success`, `message`, `errors: [{field,message}]`, and `traceId`. Unexpected errors never expose exception/SQL details to clients. Auth responses disable caching.

## Token behavior

JWTs are HS256-signed and contain `sub`, `email`, `role` claims, `jti`, issuer, audience and validity times. Validation checks signature, allowed algorithm, issuer, audience and expiration with zero clock skew. Business controllers use the named policies below; each policy checks the active user and current database roles on every request.

Refresh tokens use 64 cryptographically random bytes. Only their SHA-256 hashes are persisted. During refresh, a PostgreSQL transaction locks the predecessor row, validates expiration/revocation and active user, creates the successor, revokes the predecessor, and records `replaced_by_token_id`. Both changes commit together. Competing refresh requests cannot issue multiple successors. Logout uses the same lock and verifies ownership. Optional IP columns are mapped but left null; this phase does not trust forwarded client IP headers.

Logout revokes the supplied refresh session, not an already-issued JWT. Business policy checks reject removed roles immediately despite stale JWT claims. Login, `/me` and refresh reject inactive users or users with no supported role. Token-family revocation and logout-all-sessions are not implemented.

## Human roles and device authentication

Exactly three human roles are supported: `FarmAdministrator`, `FarmOwner`, `FarmEngineer`. Login accepts only email/password; roles come from the database, appear in JWT/login/me responses, and cannot be selected at login. CreateUser/SetRoles reject unknown or deprecated names and duplicate roles.

| Policy | FarmAdministrator | FarmOwner | FarmEngineer |
|---|---|---|---|
| ManageUsers | Yes | No | No |
| ManageFarms | Yes | Yes | No |
| ReadFarmData | Yes | Yes | Yes |
| ManageDevices | Yes | Yes | No |
| ConfigureThresholds | Yes | No | Yes |
| ManageMissions | Yes | Yes | No |

ReadFarmData includes farms/zones, sensor data/history, alerts, dashboard and all existing reports. ManageDevices covers sensor and UAV/gateway metadata; ManageMissions covers the existing mission lifecycle actions. Role permissions are combined with farm assignments for farm-scoped resources. Private notifications remain scoped to the recipient.

FarmAdministrator has global farm access without membership. FarmOwner and FarmEngineer require a current `user_farms` assignment to access a farm, in addition to the existing role policy. Roles still come exclusively from `user_roles`/`roles`.

Human users -> JWT Bearer authentication. Gateway/device -> Device Authentication with both `X-Api-Key` and `X-Gateway-Code`. Existing `/api/device/telemetry` and `/api/device/gateways/{id}/sync` reject JWT-only requests. Device credentials never grant human permissions. Keep device secrets in the existing credential configuration, independently of JWT signing secrets.

UAV Operator may remain an external/use-case actor but is NOT an authenticated backend user role. Existing `operatorNotes` fields remain operational notes.

Tests cover all three login/me/JWT role responses, six-policy allow/deny behavior, role validation, role removal and stale-token denial, independent device authentication, and upgrades from old role data including nonstandard IDs and existing target roles.

## Tests

Unit tests do not need a database:

```powershell
dotnet test tests/FarmMonitoring.UnitTests
```

For a disposable local cluster using installed PostgreSQL binaries:

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin 'D:\IDECode\PostgreSQL\18\bin' -Dotnet 'C:\Program Files\dotnet\dotnet.exe'
```

The script uses a random localhost port and random test password, restricts the generated directory to the current Windows account, and stops the server in `finally`. It leaves stopped test data/logs in ignored `.local` for diagnosis and removes the temporary password file. It never connects to the normal PostgreSQL service. Test databases are created with a random `farm_auth_tests_` prefix and dropped after tests. A forcibly terminated shell may require stopping its own test cluster with `pg_ctl -D <that .local cluster's data directory> stop`.

Alternatively supply `TEST_POSTGRES_CONNECTION` pointing to a dedicated test server/database with CREATEDB permission, then run:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Integration tests intentionally fail with a setup message when `TEST_POSTGRES_CONNECTION` is missing; they are not silently skipped. Tests use real PostgreSQL and apply the actual migration, not EF InMemory or SQLite. EF/Npgsql may log a missing migration-history table probe on a brand-new database before creating it; the migration and test outcome determine success.

Verified result (2026-10-07): **143 tests passed** (48 unit + 95 integration), with zero build warnings/errors. Integration coverage includes token expiry while waiting on a real PostgreSQL row lock. An error log from the deliberately failing repository is expected in the generic-500 response test.

Migration checks:

```powershell
dotnet ef migrations has-pending-model-changes --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
dotnet ef migrations script --idempotent --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
```

The [original authentication implementation report](docs/auth-implementation-report.md) and earlier docs/superpowers designs are historical records, not the current role contract.


## Farm-level access control

ROLE AUTHORIZATION answers "What is the user allowed to do?" FARM AUTHORIZATION answers "Which farm is the user allowed to do it on?" Both checks apply to human farm-scoped endpoints; the six-role-policy matrix above remains unchanged.

- FarmAdministrator has global access, with no `user_farms` row required.
- FarmOwner and FarmEngineer access assigned farms only. No assignments means empty scoped lists and aggregates; explicit existing resources outside access return 403.
- Only FarmAdministrator manages assignments through GET/POST `/api/farms/{farmId}/members` and DELETE `/api/farms/{farmId}/members/{userId}`.
- Assignment requires an existing active FarmOwner/FarmEngineer user. Administrators (including mixed admin/owner roles) cannot be assigned. Assigning/removing membership never changes global roles.
- Owner-created farms and the creator's assignment commit in one transaction. Administrator-created farms need no assignment. Existing farms/users receive no automatic assignments during migration.
- Current database roles and memberships are checked per operation. Removing a membership immediately denies subsequent requests using the same JWT. Stale memberships cannot grant access without a permitted current role.
- Farms, zones, nodes/channels, readings/history/latest/comparison, thresholds, missions and their child data/telemetry, alerts and farm-derived dashboard/report aggregates are scoped. Lists are filtered in SQL before paging/counting/aggregation. Every explicit farm/resource filter is authorized.
- Alerts resolve their farm through SensorChannel -> SensorNode -> Zone -> Farm, otherwise SensorNode -> Zone -> Farm, otherwise Mission -> Farm. Alerts without such an association are administrator-only. Gateway/UAV identity alone does not establish a farm.
- New alert notifications target administrators and eligible members of the resolved farm. Notification APIs retain user-specific ownership checks, including historical notifications after membership removal; they never expose another user's notification.
- Historical mission target IDs, outcome/status and sequence remain part of mission history. If a target sensor has since moved outside the viewer's accessible farms, its current `sensorName` is null rather than exposing live metadata from another farm.

UAV/Gateway entities are not directly farm-owned in the current schema. Direct per-farm device authorization requires a future explicit device-to-farm assignment model.

Direct UAV/Gateway catalog APIs retain existing global role permissions. Farm-scoped device reports/dashboard counts include only equipment referenced by missions in accessible farms (or the requested authorized farm); this is a mission association, not device ownership. An administrator's unfiltered aggregate remains global. Sensor-type definitions are a global catalog.

Gateway/device authentication remains X-Api-Key + X-Gateway-Code. `user_farms` is only for human users and is never applied to device identity or required for telemetry/sync ingestion. Human telemetry reads require mission-farm access.

Response conventions: 401 without valid authentication; 403 for denied role/farm access; 404 for nonexistent resources; 409 for duplicate membership; 400 for invalid request data; 422 for ineligible membership targets.

### Farm access deployment

Apply `20261007094620_AddUserFarmAssignments` after the role-model migration. Existing users receive no automatic farm assignments; an administrator must assign eligible owners/engineers through the membership API. No additional environment variables are required. The earlier [human role-model report](docs/human-role-model-report.md) is historical; current farm-access documentation supersedes its global-permission limitation.
