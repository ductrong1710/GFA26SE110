# Device mission integration implementation plan

**Goal:** Extend existing Mission and Sync architecture for device-authenticated offline mission execution.

**Spec:** User objective in `C:/Users/ductr/.codex/attachments/af7f0a18-8df9-4cd7-820f-24b5f41cba85/goal-objective.md`.

**Architecture:** Dedicated DeviceMissionsController and DeviceMissionService reuse DeviceKey authentication. Extract mission resource validation and transition mutation into MissionLifecycle, shared by human and device services. Each write locks the mission within the existing repository transaction before ownership/state/idempotency decisions. Sync remains the sole ingestion path.

**Tech stack:** Existing .NET 10, EF Core/Npgsql/PostgreSQL, FluentValidation, xUnit; no new dependencies.

## Constraints and decisions

- Backend only; do not modify frontend or ESP32 firmware.
- Keep the six existing mission states, GPS/local coordinates, configurable device-side altitude ceiling, existing farm authorization and alert behavior.
- Add optional TargetAssignments (SensorNodeId, WaypointSequenceNo, SequenceNo); retain nullable legacy SensorNodeIds for drafts. If both are supplied their sets must match. Scheduling/start require complete mappings; no guessed mappings. Existing test plans that schedule must supply explicit mappings.
- Use existing MissionTarget.Waypoint relationship. Physical migration already includes waypoint_id; no migration needed.
- Next returns standard success/data envelope, null when absent, ordered ScheduledStartAt then Id, at most one assigned SCHEDULED mission. No added due-time filter.
- Download acknowledgment stays SCHEDULED; one gateway-origin DOWNLOADED log under mission lock.
- Same-state start/complete retries succeed without extra logs/timestamps. Failed retry requires the same trimmed failure reason. Cancel follows existing transition semantics. Different terminal states conflict.
- Device payload projects actual SensorNode.DeviceCode and plan fields only. No human access service or credentials in DTOs.

## Execution checklist

- [x] Read repository rules, architecture, database, API and business rules; inspect Mission and Sync source and migrations.
- [x] Run unchanged baseline using `pwsh -File scripts/Test-Postgres.ps1 -PostgresBin 'D:\IDECode\PostgreSQL\18\bin'`.
- [x] Add integration tests that fail on absent device route and missing target mapping; use real API/PostgreSQL through ApiFactory.
- [x] Add TargetAssignments contract/validation and bind target.Waypoint to submitted waypoint by sequence. Reject duplicates, nonpositive IDs/sequences, mismatched sets and unknown waypoint; preserve farm/resource validation. Test create/update persistence and rollback.
- [x] Extract MissionLifecycle from MissionService: ValidateResourcesAsync, ValidateFutureSchedule, CheckOverlapAsync, ChangeStatusAsync and actor-aware log creation. Preserve human authorization in MissionService and transaction boundary at callers.
- [x] Extend IMissionRepository/MissionRepository with no-tracking next query and device-log existence check; reload locked mission before loading graph. Keep lock ordering mission -> UAV -> gateway -> sensors.
- [x] Add device DTOs/service/controller and DI registration. Test next isolation/order/null, downloaded idempotency, start timestamps/log actor, completion outcomes, compatible/incompatible failure retry, alert preservation, cancellation and wrong gateway on every route.
- [x] Test concurrent device retries and human/device transitions on real PostgreSQL; verify only one committed transition/log and no bypass of overlap/resource checks.
- [x] Test full device pull -> downloaded -> start -> existing sync -> complete flow; assert readings/collection attempts persist once, target status updated, no extra ingestion implementation.
- [x] Update existing scheduling test fixtures with explicit target mappings; run human, sync, farm security and full suite.
- [x] Update API.md, BUSINESS_RULES.md and ARCHITECTURE.md to actual behavior; keep DATABASE.md unchanged unless schema changes.
- [x] Run dotnet restore, dotnet build, dotnet test; inspect results and review diff. Record files, test counts, API examples and firmware integration assumptions. Do not claim ESP32 integration has been changed or tested.

## Completion evidence

Record commands and results here as each gate finishes. Test logs are kept under ignored `.local/`; tests must execute against PostgreSQL, never silently skip integration tests.

Final verification 2026-10-11: dotnet restore succeeded; build 0 warnings/errors; 59 unit and 108 PostgreSQL integration tests passed (167 total). Review snapshot finding reproduced and fixed using RepeatableRead; timestamp retry mismatch fixed at PostgreSQL precision. Full report: docs/DEVICE_MISSION_IMPLEMENTATION.md. Logs: .local/device-mission-final.log and .local/test-latest.log. No firmware/frontend/schema changes.
