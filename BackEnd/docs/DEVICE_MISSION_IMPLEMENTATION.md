# Device mission integration report

## Existing behavior and implementation

The original human MissionService already enforced state transitions, active
resources, farm membership, reservation overlap, target completion and mission
error alerts. SyncService already checked device ownership, rejected collection
before mission start, deduplicated batches/readings/attempts and updated target
outcomes. MissionTarget.WaypointId existed in the physical MissionManagement
migration, but MissionRequest/ApplyPlan did not populate it.

Added endpoints under `/api/device/gateways/{gatewayId}/missions`:

| Method | Suffix | Result |
| --- | --- | --- |
| GET | next | Assigned SCHEDULED mission, ordered scheduledStartAt then ID; 200/data=null if absent |
| POST | {missionId}/downloaded | One DOWNLOADED log; remains SCHEDULED |
| POST | {missionId}/start | Shared validation, RUNNING and StartedAt |
| POST | {missionId}/complete | Requires all targets resolved; COMPLETED and CompletedAt |
| POST | {missionId}/fail | Required failureReason; FAILED, CompletedAt and existing error alert |
| POST | {missionId}/cancel | Existing cancellation semantics; reports locally handled abort |

DeviceMissionService uses the existing DeviceKey scheme, route/claim equality
and mission assignment. It does not depend on FarmAccessService or CurrentUser.
Device logs have GatewayId and null UserId; human logs preserve their actor.
MissionLifecycle shares resource, mapping, overlap, state and alert rules.
SyncService/SyncRepository and sensor ingestion contracts were left unchanged.

New DTOs: MissionTargetRequest, DeviceMissionResponse,
DeviceMissionTargetResponse, DeviceMissionStateResponse,
DeviceMissionFailureRequest and DeviceMissionCancelRequest. MissionRequest adds
optional TargetAssignments, retaining legacy SensorNodeIds for drafts. If both
are supplied, their sensor sets must match. Each assignment names an existing
submitted waypoint sequence, a unique positive sensor ID and a unique positive
target sequence. ApplyPlan binds the existing Waypoint navigation; EF persists
the foreign key. Scheduling/start requires complete mappings, active targets
and the original farm/resource validations. No coordinate or identity guessing.

Repository changes: a bounded no-tracking eager next query, device-log existence
query and reload after the mission row lock. The next query uses a repeatable-read
snapshot across its split SELECTs so concurrent web updates cannot mix plan
versions. Transition writes retain the existing transaction and row-lock order.

Same-state start/complete/cancel retries do not add logs or rewrite timestamps.
Downloaded retries are deduplicated under the mission lock while SCHEDULED.
Failed retries require the same trimmed reason; incompatible terminal requests
conflict. Transition timestamps are normalized to PostgreSQL microseconds so
initial and retried response values agree.

No migration was created: the required waypoint relationship and nullable
gateway/user log actors already exist. DATABASE.md and migrations are unchanged.
Latitude/Longitude and local coordinates remain supported, with no fixed 40 cm
backend ceiling. ESP32 applies its own configurable safety limit.

## Requirement verification map

Production HTTP and PostgreSQL are exercised by DeviceMissionTests, not in-memory
repository mocks. MissionTargetValidationTests covers request boundary cases.

| Objective checks | Evidence |
| --- | --- |
| 1–6: credentials, isolation, next ordering/null, actual DeviceCode/mapping | Device_pull_requires_device_credentials_and_returns_only_assigned_plan; Next_filters_status_and_gateway_then_orders_schedule_and_id |
| 7: create/update invalid mapping rejected | Mapping_is_explicit_persisted_and_unknown_waypoint_is_rejected; unit assignment tests |
| 8–11: downloaded/start/sync/complete, retries, actor, pending targets | Download_start_sync_complete_are_idempotent_and_preserve_actor_and_outcomes |
| 12: failure reason, status and alert | Failure_requires_reason_and_retry_does_not_duplicate_alert_or_rewrite_result |
| 13: cancellation and access | Device_cancel_preserves_existing_scheduled_and_running_semantics; Every_device_action_rejects_missing_credentials_wrong_route_and_missing_mission |
| 14–17: human, sync, farm authorization and full regression | Existing MissionTests, SyncTests, FarmAccessTests, FarmScopeSecurityTests and full backend suite |
| Active equipment, sensors and reservations | Start_rechecks_active_resources_and_running_overlap_under_lock |
| Concurrent web/device state transitions | Concurrent_web_cancel_and_device_start_cannot_resurrect_cancelled_mission |
| Concurrent plan edits during pull | Pull_uses_one_snapshot_when_web_replaces_the_plan_between_selects (both variants) |
| Legacy draft migration | Legacy_draft_cannot_schedule_until_mapping_is_supplied |
| No prototype altitude restriction | Altitude_validation_uses_storage_bounds_not_esp32_ceiling |

The first three new integration tests failed against the original implementation
(missing device routes; invalid assignment accepted). The snapshot regressions
failed before the read transaction fix (new waypoint ID / missing children).
The retry test exposed timestamp precision loss before normalization. Independent
read-only review reported the snapshot issue, then confirmed the fix and found
no remaining concrete issues in its recheck.

## Commands and integration assumptions

Final verification on 2026-10-11: restore succeeded; build succeeded with zero
warnings/errors; all **167 tests passed** (59 unit, 108 PostgreSQL integration,
zero failed/skipped). This adds 11 unit and 13 integration cases over the original
48/95 baseline. The final runner exited 0 and shut down its temporary PostgreSQL
cluster. Evidence: `.local/device-mission-final.log`, `.local/test-latest.log`
and the generated TestResults/TRX files. `git diff --check` passed. Sync source,
DATABASE.md and migration files remain unchanged from the original HEAD.

See API.md, section "Device mission execution", for full JSON and PowerShell API
examples. Run the entire suite with a disposable local PostgreSQL cluster:

```powershell
Set-Location D:\Learning\SEP\Code\backend
pwsh -NoProfile -File scripts/Test-Postgres.ps1 -PostgresBin 'D:\IDECode\PostgreSQL\18\bin'
```

That script runs `dotnet restore`, `dotnet build --no-restore` and
`dotnet test --no-build`, using an isolated PostgreSQL cluster and actual
migrations. With an explicitly configured isolated TEST_POSTGRES_CONNECTION,
the equivalent direct commands are `dotnet restore`, `dotnet build`, `dotnet test`.

No frontend or ESP32 firmware was modified. Firmware still needs to consume the
standard success/data envelope, acknowledge downloaded only after verified
LittleFS persistence, await device /start before leaving Internet, submit both
mission-associated readings and CollectionResults through existing /sync, inspect
per-item results, and call /complete, /fail or /cancel after reconnection.
The earlier generic /result/fingerprint firmware contract is not this API.
Backend does not verify local disk persistence or physical landing. It does not
enforce a mission plan revision handshake across separate pull/start requests;
operators should avoid editing a downloaded plan before execution. No hardware
flight or full Web/ESP32/Tello integration was performed by this backend task.

## Files created

- docs/DEVICE_MISSION_IMPLEMENTATION.md
- docs/superpowers/plans/2026-10-08-device-missions.md
- src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs
- src/FarmMonitoring.Application/Features/Missions/DeviceMissionContracts.cs
- src/FarmMonitoring.Application/Features/Missions/DeviceMissionService.cs
- src/FarmMonitoring.Application/Features/Missions/MissionLifecycle.cs
- tests/FarmMonitoring.IntegrationTests/DeviceMissionTests.cs
- tests/FarmMonitoring.UnitTests/MissionTargetValidationTests.cs

## Files modified

- API.md
- ARCHITECTURE.md
- BUSINESS_RULES.md
- src/FarmMonitoring.Api/Extensions/ApiServiceExtensions.cs
- src/FarmMonitoring.Application/Features/Missions/MissionContracts.cs
- src/FarmMonitoring.Application/Features/Missions/MissionService.cs
- src/FarmMonitoring.Application/Features/Missions/MissionValidators.cs
- src/FarmMonitoring.Application/Interfaces/IMissionRepository.cs
- src/FarmMonitoring.Infrastructure/Persistence/Repositories/MissionRepository.cs
- tests/FarmMonitoring.IntegrationTests/FarmScopeSecurityTests.cs
- tests/FarmMonitoring.IntegrationTests/MissionTests.cs
