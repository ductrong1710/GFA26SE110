# Admin gateway/UAV flight test implementation plan

**Goal:** An admin requests a gateway/UAV connection and flight test: reach 100 cm, hold for 3000 ms, then confirm landing. No physical flight is executed during development.

**Architecture:** A persisted backend command is claimed by the assigned gateway before its radio switches to Tello. ESP32 executes the bounded sequence locally and persists progress/results across reboot. The normal mission and manual-flight paths cannot compete with this test. The user approved this design in chat.

**Stack:** Existing ASP.NET Core, EF/PostgreSQL, Arduino ESP32 and Tello UDP components.

## Shared wire contract
- Admin POST `/api/gateways/{gatewayId}/flight-tests` with `{ "requestKey": "client-generated-unique-key" }`; GET same route plus `/{id}`. Only current FarmAdministrator role may use these routes.
- Device GET `/api/device/gateways/{gatewayId}/flight-tests/next`: existing gateway API-key authentication; normal API envelope `{success:true,data:null|command}`.
- Command: `{id,gatewayId,uavId,status,altitudeCm:100,holdMs:3000,expiresAt}`. Server queue TTL 120 seconds. Status QUEUED, CLAIMED, COMPLETED, FAILED, RECOVERY_REQUIRED, EXPIRED.
- Device POST `.../flight-tests/{id}/claim` empty JSON: atomically claim before flight; envelope with command. Reject expired, inactive, reassigned or conflicting equipment. Never automatically requeue a claimed command.
- Device POST `.../flight-tests/{id}/result`: `{status,gatewayConnected,uavConnected,landed,takeoffAttempted,observedAltitudeCm,heldMs,errorCode}`; bounded typed validation, terminal replay idempotent, conflicting replay rejected. Response envelope with command/status, acknowledgment matching ID/status required. `takeoffAttempted` distinguishes connection/preparation failure without any takeoff from unconfirmed landing after takeoff.
- A CLAIMED or RECOVERY_REQUIRED test continues blocking a new test until authoritative resolution; no timeout may cause another takeoff. requestKey prevents duplicate admin retries.
- Fixed target/hold cannot be user-overridden. COMPLETED requires both connections, confirmed landing, target evidence and hold >=3000 ms.

## Work and verification
- [x] Backend: domain entity/config/migration; application service/repository; admin and device controllers; current-role policy; assignment and concurrent-command guards; API documentation.
- [x] Backend tests: PostgreSQL migrations, role rejection incl revoked role, gateway isolation, expiry, duplicate creation/claim/result, conflict and result validation.
- [x] Firmware: persistent command journal, exact envelope parsing, claim before switching, no replay after reboot, dedicated 1 m flight-test build/profile, local timing, telemetry and battery checks, bounded failure/landing path, result retry after Internet reconnect.
- [x] Firmware tests: real production sequence driven by fake UDP/time, no early landing, stale telemetry/failure/reboot/duplicate guards; default ground mode emits no takeoff.
- [x] Integrate: confirm backend/firmware wire shapes, review behavior, build both and run appropriate suites. Document commands and distinguish tests from unperformed physical acceptance.

## Constraints
No commit/push, database reset, flash erase, hardware upload or actual flight. Preserve existing workspace changes and sensor records. Never log credentials. Keep the ordinary 40 cm mission profile; dedicated test profile supports 1 m without disabling telemetry/battery/landing guards. HTTP responses mean queued/claimed, never proof of physical flight.

## Verification results (2026-10-19)

- Backend build: zero warnings/errors. `scripts/Test-Postgres.ps1 -PostgresBin 'D:\IDECode\PostgreSQL\18\bin' -NoRestore`: 65 unit tests and 145 integration tests passed against temporary PostgreSQL, including migrations, current admin authorization, device identity isolation, concurrent reservations and mission exclusion.
- EF `migrations has-pending-model-changes --no-build`: no pending model changes.
- ESP32 `tests/run_flight_test_host.ps1`: live and ground profiles passed, including continuous 3000 ms hold, intermediate telemetry excursions/overshoot, persistence failure, ambiguous claims and reboot replay guards.
- ESP32 `tests/run_tello_host.ps1` and `tests/run_mission_host.ps1`: passed, including production mission/collection simulation and landing/reboot recovery.
- PlatformIO `run -e esp32dev -e flight_test`: both builds succeeded. No firmware upload or real flight performed.
- Final code review found an intermediate telemetry packet issue; regression reproduced it, the fix passed, and follow-up review found no remaining actionable issues in that change.
- `git diff --check`: passed in both repositories. No commit/push or mutation of the user's real database.

Deployment commands and physical acceptance boundaries are documented in `../FLIGHT_TEST_RUNBOOK.md`.
