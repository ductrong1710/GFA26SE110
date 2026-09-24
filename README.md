# Farm Monitoring — Authentication backend

Phase 1 implements only login, refresh, logout and current user, plus the shared ASP.NET Core foundation. The five original project documents remain unchanged.

## Structure

```text
src/
  FarmMonitoring.Domain/          Entities and role constants
  FarmMonitoring.Application/     Auth service, DTOs, validators, interfaces
  FarmMonitoring.Infrastructure/  PostgreSQL, EF mappings/migrations, JWT, hashing, seed
  FarmMonitoring.Api/             Controllers, middleware, configuration, Swagger
tests/
  FarmMonitoring.UnitTests/
  FarmMonitoring.IntegrationTests/
scripts/Test-Postgres.ps1          Isolated Windows PostgreSQL test runner
```

Domain has no external dependencies. Application references Domain. Infrastructure implements Application interfaces. API uses Infrastructure only for startup/composition. No user-management or farm/device endpoints are included.

## Prerequisites

- .NET 10 SDK (`global.json` allows installed .NET 10 feature bands).
- PostgreSQL (verified with PostgreSQL 18).
- PowerShell 7 for the optional Windows test-cluster script.

On this workstation the SDK is installed at `C:\Program Files (x86)\dotnet\dotnet.exe`. If `dotnet` is not found, add that directory to the current shell's PATH:

```powershell
$env:PATH = 'C:\Program Files (x86)\dotnet;' + $env:PATH
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

Migration `20260918045049_InitialAuth` creates only `users`, `roles`, `user_roles`, `refresh_tokens`, plus EF migration history. It inserts `FarmAdministrator` and `UavDeviceOperator` role records. It contains no account passwords or JWT secrets.

To seed the first administrator, set both `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD` via environment variables or user secrets, then start the API in Development. The seed hashes the password, normalizes email, and assigns FarmAdministrator. It runs only when no administrator exists; it never changes an existing account's password or promotes an existing email. Clear the two seed settings afterward. Database migrations must be applied before running the seed. Normal startup does not apply migrations automatically.

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

JWTs are HS256-signed and contain `sub`, `email`, `role` claims, `jti`, issuer, audience and validity times. Validation checks signature, allowed algorithm, issuer, audience and expiration with zero clock skew. Future controllers can use `[Authorize]` or `[Authorize(Roles = RoleNames.FarmAdministrator)]`.

Refresh tokens use 64 cryptographically random bytes. Only their SHA-256 hashes are persisted. During refresh, a PostgreSQL transaction locks the predecessor row, validates expiration/revocation and active user, creates the successor, revokes the predecessor, and records `replaced_by_token_id`. Both changes commit together. Competing refresh requests cannot issue multiple successors. Logout uses the same lock and verifies ownership. Optional IP columns are mapped but left null; this phase does not trust forwarded client IP headers.

Logout revokes the supplied refresh session, not an already-issued JWT. JWTs remain usable until their short expiry; `/me` and refresh additionally reject inactive users. Future protected business endpoints must consider account/role changes if immediate invalidation is required. Token-family revocation and logout-all-sessions are not implemented.

## Tests

Unit tests do not need a database:

```powershell
dotnet test tests/FarmMonitoring.UnitTests
```

For a disposable local cluster using installed PostgreSQL binaries:

```powershell
./scripts/Test-Postgres.ps1 -PostgresBin 'D:\IDECode\PostgreSQL\18\bin' -Dotnet 'C:\Program Files (x86)\dotnet\dotnet.exe'
```

The script uses a random localhost port and random test password, restricts the generated directory to the current Windows account, and stops the server in `finally`. It leaves stopped test data/logs in ignored `.local` for diagnosis and removes the temporary password file. It never connects to the normal PostgreSQL service. Test databases are created with a random `farm_auth_tests_` prefix and dropped after tests. A forcibly terminated shell may require stopping its own test cluster with `pg_ctl -D <that .local cluster's data directory> stop`.

Alternatively supply `TEST_POSTGRES_CONNECTION` pointing to a dedicated test server/database with CREATEDB permission, then run:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Integration tests intentionally fail with a setup message when `TEST_POSTGRES_CONNECTION` is missing; they are not silently skipped. Tests use real PostgreSQL and apply the actual migration, not EF InMemory or SQLite. EF/Npgsql may log a missing migration-history table probe on a brand-new database before creating it; the migration and test outcome determine success.

Verified result: **53 tests passed** (20 unit + 33 integration), with zero build warnings/errors. Integration coverage includes token expiry while waiting on a real PostgreSQL row lock. An error log from the deliberately failing repository is expected in the generic-500 response test.

Migration checks:

```powershell
dotnet ef migrations has-pending-model-changes --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
dotnet ef migrations script --idempotent --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
```

See [implementation report](docs/auth-implementation-report.md) for the file/package inventory, verification results, remaining scope, and proposed documentation additions.
