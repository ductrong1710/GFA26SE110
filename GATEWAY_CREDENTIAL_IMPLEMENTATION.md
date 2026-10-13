# Gateway Credential Management

## Delivered behavior

The Mobile Gateways tab at `/app/devices` has a Credential action for users with DEVICES_MANAGE. UAV rows have no credential action. The existing ManageDevices policy permits FarmAdministrator and FarmOwner; FarmEngineer remains read-only for this feature.

The dialog loads metadata, generates a credential, and displays the raw key only after creation/rotation. It offers Copy Gateway Code, Copy API Key and Copy ESP32 Configuration, plus the existing device sync headers. Rotation/revocation have explicit confirmation. Closing, unmounting, changing account/role/session or losing permission removes secret state and aborts outstanding requests. Demo mode does not invent credentials. Keys never enter browser storage, URLs, shared context or mock data.

## APIs and migration

| Method | Path | Response |
|---|---|---|
| GET | /api/gateways/{gatewayId}/credential/status | Metadata only |
| POST | /api/gateways/{gatewayId}/credential | 201, raw key once |
| POST | /api/gateways/{gatewayId}/credential/rotate | 200, replacement key once |
| DELETE | /api/gateways/{gatewayId}/credential | 204, soft revoke |

Migration: `20261013021208_GatewayCredentials`. The new table has integer identity, restrictive gateway/user FKs, metadata timestamps, SHA-256 hex hash, prefix, state checks and a filtered unique active-key index. It does not provision any keys or alter existing gateways.

Apply with `dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api` using the API connection configuration (or the optional `ConnectionStrings__DefaultConnection` override). Restart the backend after updating its binaries. Existing DeviceAuthentication configuration credentials must be replaced through the new API/UI: there is deliberately no fallback that could bypass revocation.

## Security evidence

- Server generates 32 random bytes with RandomNumberGenerator; only SHA-256 hex and an 8-character prefix are persisted.
- Only creation/rotation DTOs contain apiKey. Metadata/catalog DTOs never expose raw keys or hashes. Credential responses are no-store.
- DeviceAuthenticator uses database credentials, active gateway checks and FixedTimeEquals. Gateway row transactions serialize authentication with mutations; valid authentication updates LastUsedAt.
- Revoke and rotate preserve audit history and immediately invalidate the old key for subsequent authentication. A partial unique index protects the one-active-key invariant under concurrency.
- DeviceAuthenticationHandler and device sync/telemetry routes retain DeviceKey with X-Gateway-Code + X-Api-Key. Human JWT and device credentials do not substitute for each other.
- No direct UAV flight-control behavior was added.

## Requirement audit

| Spec section | Evidence |
|---|---|
| 1 Database | GatewayCredential, configuration, generated migration and snapshot; PostgreSQL upgrade/index tests |
| 2 Device auth | Database-only DeviceAuthenticator; sync/telemetry, wrong key/code, inactive and LastUsedAt tests |
| 3 Management APIs | GatewayCredentialsController -> GatewayCredentialService -> GatewayCredentialRepository; authorization/conflict/404 tests |
| 4 Devices page | Gateway-only action, existing DEVICES_MANAGE check; rendered React tests for admin/owner/read-only/UAV rows |
| 5 Dialog | Empty/metadata/one-time states; copy, close/reopen, late response, account/session and clipboard failure tests |
| 6 API integration | gatewayCredentials.js uses the existing authenticated api.request; AbortSignal forwarded |
| 7 ESP32 help | Dialog includes physical ESP32 help and exact sync route/header example |
| 8 Existing behavior | Existing equipment actions retained; original frontend and backend suites rerun |
| 9 Security | Cryptographic generation/hash comparison, local secret lifetime, server policy, no-store and database invariant reviewed |
| 10 Tests | GatewayCredentialTests plus migrated test-only device fixtures; gateway-credentials.test.jsx |
| 11 Verification | See final verification results below |

## Example: GW-ESP32-01

1. Sign in as FarmAdministrator or FarmOwner.
2. Open Devices -> Mobile Gateways -> Register Gateway. Use code `GW-ESP32-01`, type `ESP32`, and save an active gateway.
3. Choose Credential -> Generate Credential.
4. Choose Copy ESP32 Configuration and save those values on the physical device before closing the dialog.
5. Send existing device requests with X-Gateway-Code and X-Api-Key. The sync payload must follow the existing API contract.
6. To replace a key, reopen Credential -> Rotate Credential -> Confirm Rotate, then update the ESP32. Revoke disables the current key immediately.

No real/sample gateway API credentials were added to source control or issued against the user's database during implementation.
## Changed files

### Backend

- `API.md`
- `ARCHITECTURE.md`
- `BUSINESS_RULES.md`
- `DATABASE.md`
- `FRONTEND_API_INTEGRATION.md`
- `README.md`
- `src/FarmMonitoring.Api/Extensions/ApiServiceExtensions.cs`
- `src/FarmMonitoring.Infrastructure/Authentication/DeviceAuthenticator.cs`
- `src/FarmMonitoring.Infrastructure/DependencyInjection.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/AppDbContext.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `tests/FarmMonitoring.IntegrationTests/AlertTests.cs`
- `tests/FarmMonitoring.IntegrationTests/ApiFactory.cs`
- `tests/FarmMonitoring.IntegrationTests/DeviceMissionTests.cs`
- `tests/FarmMonitoring.IntegrationTests/FarmScopeSecurityTests.cs`
- `tests/FarmMonitoring.IntegrationTests/HumanRoleTests.cs`
- `tests/FarmMonitoring.IntegrationTests/SyncTests.cs`
- `tests/FarmMonitoring.IntegrationTests/TelemetryTests.cs`
- `GATEWAY_CREDENTIAL_IMPLEMENTATION.md`
- `docs/superpowers/plans/2026-10-13-gateway-credentials.md`
- `src/FarmMonitoring.Api/Controllers/GatewayCredentialsController.cs`
- `src/FarmMonitoring.Application/Features/Equipment/GatewayCredentialContracts.cs`
- `src/FarmMonitoring.Application/Features/Equipment/GatewayCredentialService.cs`
- `src/FarmMonitoring.Application/Interfaces/IGatewayCredentialRepository.cs`
- `src/FarmMonitoring.Domain/Entities/GatewayCredential.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Configurations/GatewayCredentialConfiguration.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261013021208_GatewayCredentials.Designer.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Migrations/20261013021208_GatewayCredentials.cs`
- `src/FarmMonitoring.Infrastructure/Persistence/Repositories/GatewayCredentialRepository.cs`
- `tests/FarmMonitoring.IntegrationTests/GatewayCredentialTests.cs`

### Frontend

- `package-lock.json`
- `package.json`
- `src/pages/app/DevicesPage.jsx`
- `src/styles/management.css`
- `src/components/app/GatewayCredentialDialog.jsx`
- `src/services/api/gatewayCredentials.js`
- `tests/gateway-credentials.test.jsx`

## Final verification (2026-10-13)

- `dotnet restore`: passed.
- Whole backend build: passed, zero warnings/errors.
- `scripts/Test-Postgres.ps1 -PostgresBin D:\IDECode\PostgreSQL\18\bin -NoRestore`: passed, 59 unit + 138 PostgreSQL integration tests (197 total). Migration is applied to fresh isolated databases; upgrade from the prior migration is separately tested without issuing credentials or altering the existing gateway.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: no model changes pending. Idempotent migration script generation passed.
- `npm ci`: passed; package audit found zero vulnerabilities.
- `npm run lint`: passed with no warnings after the final async status-loading adjustment.
- `npm run build`: passed.
- Original Node tests: 92 passed. React credential tests: 16 passed, rerun after the final dialog change (108 frontend tests total).
- Backend/frontend source review checked authorization, secret lifetime, concurrency, configuration fallback removal and preservation of existing equipment actions. No known outstanding functional issue.
- No physical ESP32 or live browser/device hardware smoke test was performed. Device header compatibility is exercised through real PostgreSQL-backed HTTP integration tests; UI flows are exercised as rendered React components.
- The migration was applied and checked on isolated test databases; the user's running database has not been updated by this feature task.

