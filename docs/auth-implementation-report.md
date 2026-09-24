# Authentication implementation report

## 1. Implemented scope

Authentication-only .NET 10 backend: four Clean Architecture layers, PostgreSQL/EF Core, dependency injection, centralized validation and exceptions, consistent responses, JWT authentication/role authorization, Swagger, secure refresh rotation/revocation, initial migration and optional development administrator bootstrap. No farm, sensor, UAV, gateway, mission, alert, report or user-management module was implemented. No registration endpoint exists.

## 2. Project structure affected

Four projects under `src`: `FarmMonitoring.Domain`, `FarmMonitoring.Application`, `FarmMonitoring.Infrastructure`, `FarmMonitoring.Api`. Two projects under `tests`: `FarmMonitoring.UnitTests`, `FarmMonitoring.IntegrationTests`. Solution and build settings live at the backend root.

## 3. Created files

All implementation files are new because the original repository contained documentation only. The inventory below groups files by directory; all paths are relative to `backend`.

- Root: `.gitignore`, `global.json`, `Directory.Build.props`, `FarmMonitoring.slnx`, `README.md`, `.config/dotnet-tools.json`.
- `src/FarmMonitoring.Domain`: `FarmMonitoring.Domain.csproj`, `Constants/RoleNames.cs`, `Entities/User.cs`, `Entities/Role.cs`, `Entities/UserRole.cs`, `Entities/RefreshToken.cs`.
- `src/FarmMonitoring.Application`: `FarmMonitoring.Application.csproj`, `Common/AuthException.cs`, `Features/Auth/AuthContracts.cs`, `Features/Auth/AuthValidators.cs`, `Features/Auth/AuthService.cs`, `Interfaces/IAuthRepository.cs`, `Interfaces/IPasswordService.cs`, `Interfaces/ITokenService.cs`.
- `src/FarmMonitoring.Infrastructure`: `FarmMonitoring.Infrastructure.csproj`, `DependencyInjection.cs`, `Authentication/JwtOptions.cs`, `Authentication/JwtTokenService.cs`, `Authentication/PasswordService.cs`, `Persistence/AppDbContext.cs`, `Persistence/AppDbContextFactory.cs`, `Persistence/DatabaseOptions.cs`, `Persistence/DevelopmentAdminSeeder.cs`, `Persistence/Repositories/AuthRepository.cs`, `Persistence/Configurations/UserConfiguration.cs`, `Persistence/Configurations/RoleConfiguration.cs`, `Persistence/Configurations/UserRoleConfiguration.cs`, `Persistence/Configurations/RefreshTokenConfiguration.cs`, `Persistence/Migrations/20260918045049_InitialAuth.cs`, its `.Designer.cs`, and `AppDbContextModelSnapshot.cs`.
- `src/FarmMonitoring.Api`: `FarmMonitoring.Api.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`, `Contracts/ApiResponse.cs`, `Controllers/AuthController.cs`, `Extensions/ApiServiceExtensions.cs`, `Middleware/ExceptionHandlingMiddleware.cs`.
- `tests/FarmMonitoring.UnitTests`: `FarmMonitoring.UnitTests.csproj`, `AuthValidationTests.cs`, `PasswordSecurityTests.cs`, `SecurityConfigurationTests.cs`.
- `tests/FarmMonitoring.IntegrationTests`: `FarmMonitoring.IntegrationTests.csproj`, `ApiFactory.cs`, `RoleProbeController.cs` (test-only), `AuthBoundaryTests.cs`, `AuthContractTests.cs`, `AuthFlowTests.cs`, `AuthSecurityTests.cs`, `AdminSeedTests.cs`, `AccountLifecycleTests.cs`, `ExceptionResponseTests.cs`, `TokenLockExpiryTests.cs`.
- Tooling/documentation: `scripts/Test-Postgres.ps1`, `docs/superpowers/specs/2026-09-18-auth-design.md`, `docs/superpowers/plans/2026-09-18-auth-foundation.md`, and this report.

## 4. Existing files modified

None. `AGENTS.md`, `ARCHITECTURE.md`, `DATABASE.md`, `API.md`, and `BUSINESS_RULES.md` are unchanged. Work is left uncommitted in the existing `BackEnd` checkout for review.

## 5. Added dependencies

| Package/tool | Version | Purpose |
|---|---|---|
| Microsoft.EntityFrameworkCore | 10.0.12 | EF Core |
| Microsoft.EntityFrameworkCore.Relational | 10.0.12 | Consistent relational dependency across projects |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | Migration tooling, private dependency |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL provider |
| Microsoft.Extensions.Identity.Core | 10.0.12 | ASP.NET Core password hasher |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.12 | Typed configuration |
| System.IdentityModel.Tokens.Jwt | 8.22.0 | JWT creation |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 | JWT validation |
| FluentValidation | 12.1.1 | Application request validation |
| Swashbuckle.AspNetCore | 10.2.3 | Swagger/OpenAPI |
| Microsoft.NET.Test.Sdk | 18.10.1 | Test runner |
| xunit | 2.9.3 | Test framework |
| xunit.runner.visualstudio | 4.0.0 | Test adapter |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | API integration tests |
| dotnet-ef (local tool) | 10.0.12 | EF CLI |

Npgsql 10 supports this EF Core generation ([provider release notes](https://www.npgsql.org/efcore/release-notes/10.0.html)); JWT validation follows the required checks described in [Microsoft's JWT Bearer documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).

## 6–7. Database tables and migration

`20260918045049_InitialAuth` creates `users`, `roles`, `user_roles` and `refresh_tokens`, using the documented columns, integer identity keys, UTC timestamps and restricted foreign-key deletion. The migration also seeds the two role names. EF maintains `__EFMigrationsHistory`. Unique constraints cover email, role name, user/role pair, and refresh-token hash.

Only temporary test databases have had this migration applied. **The project's real database connection remains unconfigured**, and no application migration has been applied to the user's existing database service. `ConnectionStrings:DefaultConnection` remains blank in source.

## 8. Endpoints

| Endpoint | Result |
|---|---|
| POST `/api/auth/login` | Token pair plus public user DTO |
| POST `/api/auth/refresh` | Rotated token pair plus current user/roles |
| POST `/api/auth/logout` | Revoke the authenticated user's submitted refresh token |
| GET `/api/auth/me` | Current active user information |

Login/refresh/logout success is HTTP 200. Validation is 400; invalid credentials/tokens or missing authentication is 401; insufficient roles is 403. Response envelopes follow API.md. The `roles` array follows the documented many-to-many schema; `expiresIn` is retained and `accessTokenExpiresAt` is added.

## 9–10. JWT and refresh behavior

JWT uses configurable HS256 signing, issuer and audience, with 15-minute access lifetime and 7-day refresh lifetime by default. Claims include `sub`, `email`, `role` and `jti`. Signature, algorithm, issuer, audience and lifetime are validated. Startup rejects missing/invalid required settings.

Refresh tokens contain 64 random bytes and are stored only as SHA-256 hashes. Rotation locks the old row in PostgreSQL, validates it, inserts its successor, revokes the predecessor and stores the replacement ID in one transaction. Expiration is checked after acquiring the lock so time spent waiting cannot allow an expired token. Logout locks and checks ownership before revocation. Optional IP columns are mapped but remain null.

## 11–15. Configuration, seeding, running and Swagger

The [README](../README.md) provides copyable commands for PostgreSQL configuration, EF migration application, developer HTTPS, starting the API and Swagger testing.

Required secrets are `ConnectionStrings__DefaultConnection` and `Jwt__SecretKey`. Initial development seeding optionally uses `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD`; both must be supplied, and the password must be 12–1024 characters. The seed only creates the first administrator, hashes the password, and does not reset/promote existing accounts. No credentials are committed.

After configuring PostgreSQL, apply migrations with the Infrastructure project and Api startup project, start the API using the `https` launch profile, and open `https://localhost:7043/swagger`. Log in, paste the access token into Authorize, call `/me`, then refresh and logout with the latest refresh token.

## 16–17. Example login exchange

Request:

```json
{"email":"admin@example.com","password":"<your-seeded-password>"}
```

Illustrative response:

```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt>",
    "refreshToken": "<opaque-random-token>",
    "accessTokenExpiresAt": "2026-09-18T09:15:00+00:00",
    "expiresIn": 900,
    "user": {"id":1,"email":"admin@example.com","fullName":"Development Administrator","roles":["FarmAdministrator"]}
  },
  "message": null
}
```

## 18. Remaining work and proposed documentation changes

Deployment setup still requires the user's database connection and signing secret, applying the migration to that database, and supplying first-admin development seed credentials. The test database does not replace this setup.

Future scope: administrator user management, production account provisioning, token cleanup, optional logout-all-sessions/token-family revocation, deployment-specific abuse controls, and immediate account/role invalidation for future business endpoints if required. Existing JWTs remain valid until expiry after logout; `/me` and refresh check account activity. These follow-ups are not implemented in this Auth-only phase.

Proposed changes to original documentation, **not applied**:

- `API.md`: document GET `/api/auth/me`, logout's `{refreshToken}` body, `accessTokenExpiresAt` alongside `expiresIn`, and the validation/authentication error contract.
- `DATABASE.md`: identify the Auth-only initial migration, unique `token_hash` index, and the canonical trimmed/lowercase email convention. No table redesign is proposed.
- `BUSINESS_RULES.md`: document transactional row locking, single-successor concurrent rotation, ownership checks on logout, expired-token rejection after waiting, and the distinction between refresh revocation and existing access-token lifetime.
- `AGENTS.md` and `ARCHITECTURE.md`: no changes needed.

## Verification

Verified on 2026-09-18 with .NET SDK 10.0.401, runtime 10.0.12 and PostgreSQL 18.6:

- `dotnet restore`: successful.
- `dotnet build --no-restore`: successful, **0 warnings and 0 errors**.
- `dotnet test --no-build`: **53 passed, 0 failed, 0 skipped** (20 unit tests and 33 integration tests).
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Idempotent migration SQL generated and reviewed; only the four Auth tables and EF history are created. Role identity sequence is advanced past the seeded role IDs.
- `dotnet list package --vulnerable --include-transitive`: no known vulnerable packages reported by the configured NuGet source at verification time.
- Independent code review completed. Its expiry-under-lock finding was reproduced by two failing regression cases (refresh/logout returned 200), corrected, and both cases now pass with 401. Follow-up review found no remaining issue in that fix.
- Git diff and SHA-256 hashes confirm all five original documentation files are unchanged.

Coverage includes all eleven requested Auth scenarios, normalized email, hash-only token storage, eight simultaneous refresh requests with one successor, cross-user logout rejection, current-user updates, disabled accounts, password rehash, role authorization, forged/expired/wrong-issuer/wrong-audience JWTs, malformed JSON, configuration validation, safe/idempotent admin seeding, Swagger contract and internal-error suppression. The test-only role endpoint is not shipped in the API assembly.

Integration tests used an isolated PostgreSQL 18 cluster with random credentials and port, applied the actual migration and dropped their own random test databases. The temporary server was stopped after the run. The normal PostgreSQL service was not used. No claims are made about connection to the user's intended project database, which is still unconfigured.

Expected test logs include the deliberately injected exception used to verify a generic 500 response and EF's migration-history probe against a new database. These were handled as expected and did not fail tests. Local TRX results are under each test project's ignored `TestResults` directory; the combined log is `.local/test-latest.log`.
