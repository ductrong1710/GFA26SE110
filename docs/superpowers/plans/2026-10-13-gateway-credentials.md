# Gateway Credential Management Implementation Plan

**Goal:** Persist and manage ESP32 gateway API credentials from the existing Devices UI.
**Architecture:** Human Controller -> Application Service -> Repository -> PostgreSQL. DeviceKey retains its existing headers but authenticates database hashes only. Gateway row locks serialize credential creation, rotation, revocation and authentication; a partial unique index enforces one active credential.
**Spec:** C:/Users/ductr/.codex/attachments/5cf25a34-4c78-47ad-924d-3d3c47381c6f/pasted-text-1.txt
**Tech Stack:** Existing .NET 10 / EF Core / PostgreSQL, React / Vite.

## Constraints and rulings
- Work in the existing separate BackEnd and fe checkouts, preserving the user's branch layout. No merge, commits or pushes.
- Keep ManageDevices unchanged (FarmAdministrator and FarmOwner), and preserve the DeviceKey HTTP contract.
- No configuration fallback: it would allow a revoked key to authenticate. Existing configured gateways must receive a database credential through the new UI/API.
- Generate 32 random bytes server-side, store SHA-256 hex only, return raw key only once. Responses use Cache-Control: no-store.
- At most one active credential per gateway. Revoke retains history; create after revoke is allowed. Rotate without an active credential is 409; inactive gateways cannot create/rotate (422).
- Status contains metadata only. All four routes require ManageDevices.
- Dialog local state owns the raw key; closing/unmounting/account change discards it. Existing device operations and mission locks remain intact.

## Task 1: Backend model, service, repository and device authentication
- [x] Add GatewayCredential entity, configuration, DbSet and migration with restrictive FKs, hash/state constraints, one-active partial unique index and history index.
- [x] Add IGatewayCredentialRepository, response records and GatewayCredentialService.
- [x] Implement atomic create/rotate/revoke with gateway row locking and role-independent repository operations.
- [x] Add four management endpoints; add no-store response headers, metadata-only status and one-time create/rotate DTO.
- [x] Replace configuration authentication with active database credential lookup, constant-time hash comparison, atomic LastUsedAt update.

## Task 2: Backend verification (parallel test agent)
- [x] Write API tests first: create, valid/wrong key/code, revoke, rotate old/new, metadata secrecy, human authorization, inactive gateway.
- [x] Cover existing telemetry/sync header contract, concurrency, index enforcement, history and non-secret persisted fields.
- [x] Migrate old test-only device credential fixtures to database hashes, preserving their assertions.
- [x] Run dotnet restore, build, all unit/integration tests using isolated PostgreSQL. Apply migration via test setup and check model consistency.

## Task 3: Frontend (parallel frontend agent)
- [x] Add API client wrappers for status/create/rotate/revoke.
- [x] Add GatewayCredentialDialog using existing UI: empty, metadata, one-time key, copy actions, help, errors, pending states.
- [x] Add permission-gated action to gateway rows only; no generated demo keys.
- [x] Behavioral tests for all nine requested cases and close/in-flight/session lifecycle; lint/build/full tests.

## Task 4: Review and documentation
- [x] Review backend/frontend together against each of the 11 spec sections; fix issues and rerun affected/full verification.
- [x] Update DATABASE, API, ARCHITECTURE, BUSINESS_RULES, README and integration documentation with actual behavior and configuration transition.
- [x] Report changed files, migration, four APIs, UI/security behavior, verified tests, and GW-ESP32-01 / ESP32 example steps.

