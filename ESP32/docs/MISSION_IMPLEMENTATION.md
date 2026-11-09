# Mission implementation: approved scope and phase evidence

## Approved design

Source: ESP32_MISSION_PULL_AND_AUTONOMOUS_EXECUTION_PROMPT.md and the user's
2026-10-08 approval attachment (574167ae-2c80-4878-b785-3d01571b28a5).
Implement M1 through M9 on one ESP32, retaining the existing sensor pipeline.
Do not modify backend or upload firmware automatically.

The approval supersedes the proposed 2 m ceiling: Config.h must define a
40 cm maximum, 35 cm target and 20 cm minimum operating altitude. Ground-test
mode defaults enabled. Download never starts flight. Explicit mission start
is required. The Tello internal takeoff transient cannot guarantee 40 cm.

Modules: MissionBackendClient (device adapter/mock source), MissionStorage
(verified LittleFS replacement), MissionRoutePlanner (bounded local route),
MissionManager (cooperative orchestration), shared flight altitude safety.
All vertical manual and mission controls must use that safety boundary.
Fresh telemetry is required for ascent and post-takeoff stabilization.
Mission landing requires positive ground telemetry, not telemetry loss.

## M1: backend contract review

Reviewed backend sources:
- Api/Controllers/MissionsController.cs: /api/missions uses human access
  policies; start/complete/fail/cancel require ManageMissions.
- Application/Features/Missions/MissionContracts.cs: waypoint LocalX/LocalY
  and AltitudeM are nullable; target response has SensorNodeId and optional
  WaypointId/SequenceNo, but no DeviceCode.
- Domain/Entities/Mission.cs: SCHEDULED transitions to RUNNING/CANCELLED;
  RUNNING transitions to COMPLETED/FAILED/CANCELLED.
- Api/Authorization/DeviceAuthenticationHandler.cs: device scheme DeviceKey
  authenticates X-Gateway-Code plus X-Api-Key and yields gateway_id.
- Device-authenticated routes found: gateway sensor sync and telemetry ingest.
  No device mission pull/result endpoint was found in current backend sources.

Therefore live mission integration is unavailable in the current backend.
ESP32 must not call human mission endpoints or store a human JWT.
The configurable proposed device contract is:

GET /api/device/gateways/{gatewayId}/missions/next

Return 204 when no eligible mission exists; otherwise return one assigned,
due SCHEDULED mission. Device identity must match the route gateway ID.
Payload must include mission ID, name, gatewayId, uavId, status, bounded
waypoints (id, sequenceNo, localX, localY, altitudeM, actionType,
plannedHoldSeconds), targets (sensorNodeId, deviceCode, waypointId).
Coordinates use meters, origin at takeoff, +X forward and +Y right relative
to initial heading. GPS-only payloads are rejected. Target mapping is explicit.

Proposed result endpoint (not currently implemented):
POST /api/device/gateways/{gatewayId}/missions/{missionId}/result

Require idempotent acceptance by mission ID/execution ID, authorization of
assignment, and reconciliation of offline RUNNING then terminal status. A
mission executed offline may still be SCHEDULED on the server; existing human
state transitions cannot simply be reused for terminal upload. Server must
return an explicit acknowledgement before local result is considered uploaded.
Mock acknowledgement must be visibly distinguished from backend acceptance.

## Ordered implementation and verification

- [x] M1: contract report and baseline PlatformIO build.
- [x] M2: bounded mission model, state/result files, verified replacement,
  reboot recovery tests. Never overwrite valid data after a failed download.
- [x] M3: Internet-only adapter, device headers, mock source, polling/backoff;
  no replacement of READY or unsynced result.
- [x] M4: local planner and shared safety configuration; test 35/40 accepted,
  50 rejected, altered ceiling, invalid config, duplicate sequence, GPS-only,
  bounds and movement splitting. No command emission from planning.
- [x] M5: explicit start, ground mode, command ownership, stabilization,
  abort/land/recovery; add six local mission routes.
- [x] M6: targeted collection through existing CollectionManager; preserve
  persistence-before-ACK and expose target success/failure evidence.
- [x] M7: positive landing evidence, retained Tello on uncertainty, recovery
  boot inhibits unsafe Internet switching.
- [x] M8: durable result, sensor uploads first, retry result until acknowledged.
- [x] M9: regression tests, clean build, instructions and requirement audit.

Each phase ends with `pio run -e esp32dev`, log inspection for warnings/errors,
and relevant tests before starting the next phase. Build logs go in .tools.
Tests must exercise production code with fake transport/clock/filesystem as
needed; they do not establish physical altitude control or autonomous flight.

## Safety integration findings

Current FlightStateManager lacks runtime altitude limiting; add a shared
controller and ensure direct TelloController movement cannot bypass it.
Current landing has an ACK-plus-timeout fallback: disable that fallback for
missions, where positive telemetry confirmation is required.
Current CollectionManager needs a targeted request/completion interface,
not a replacement implementation. Existing bounded sensor HTTP is synchronous;
account for loop latency and avoid claiming hard real-time control.
Reboot currently defaults STA to Internet: mission recovery must be evaluated
before enabling that default when persisted state indicates possible flight.

## Evidence

M1 baseline PlatformIO build passed (2026-10-08), log
`.tools/mission-m1-build.log`: RAM 49,444 bytes; flash 905,169 bytes.
Log search found no `warning:` or `error:` entries. No mission firmware or
hardware validation is complete yet. Remaining phase results will be recorded
below as they are executed.

M2: host tests passed for codec, short-write/rename failure preservation,
cross-file identity mismatch, missing/truncated state, airborne reboot and
retained result acknowledgement. ESP32 build passed in 43.80 s, no compiler
warnings/errors; log `.tools/mission-m2-build.log`.

M3: production client host tests passed for network/poll gating, malformed
response preservation, mock isolation and matching result acknowledgements.
ESP32 build passed in 60.06 s, no compiler warnings/errors; log
`.tools/mission-m3-build.log`. Client is not wired into main until integration.

M4: route/safety host tests passed, including changed ceiling (60 cm), 35/40
acceptance versus 50 rejection at the default ceiling, duplicate sequence,
target mapping, segmentation and stale telemetry. UDP-boundary tests passed
with both default ground mode and a separately compiled live test variant.
Existing Tello and network/NTP suites passed. ESP32 build passed in 43.90 s,
no compiler warnings/errors; log `.tools/mission-m4-build.log`.

The default build keeps `GATEWAY_GROUND_TEST_MODE=1` in Config.h. Native tests
compile a separate variant with `-DGATEWAY_GROUND_TEST_MODE=0` to verify live
command safety without hardware. This does not modify firmware configuration.
The shared controller is owned by TelloController and checks direct movement
and RC calls, including active RC when telemetry changes.

SDK reference: https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf
Movement commands have a 20 cm minimum. Therefore sub-20 cm horizontal legs
are rejected rather than silently changed. Fine altitude correction needs
bounded slow RC pulses with fresh telemetry reassessment; normal down steps
must await command completion and a new telemetry sample. A 40 cm ceiling
is a control policy, not a guarantee during internal takeoff or inertia.

M5: MissionManager connected to main and six local REST routes. Host tests
verify explicit start, durable pre-takeoff state, ground mode/abort, ACK and fresh
telemetry before repeated altitude adjustment, and reboot recovery. Build passed
in 57.45 s, no compiler warnings/errors; `.tools/mission-m5-build.log`.

M6: CollectionManager extended in place with targeted mission requests. Tests
exercise production collection sequencing with injected node HTTP/storage
boundaries: unknown/offline identity rejected, save precedes ACK, failed save
does not ACK, failed ACK cannot mark collected, cancellation stops requests.
Build passed in 17.96 s, no compiler warnings/errors; `.tools/mission-m6-build.log`.

M7: mission landing requires ACK plus stable ground telemetry; ACK-only fallback
is disabled while mission owns flight. Failed land enters RecoveryRequired and
does not automatically retry. An explicit land/abort permits a new attempt.
Conflicting h/tof cannot confirm ground. Boot recovery chooses Tello before any
Internet connection attempt. Mission and previous Tello/network suites passed.
Build passed in 19.52 s, no compiler warnings/errors; `.tools/mission-m7-build.log`.

M8: durable progress/result acknowledgement and ordered synchronization tests
passed, including backend failure, retry timing and acknowledgement write failure.
Build passed in 17.50 s, no compiler warnings/errors; `.tools/mission-m8-build.log`.

M9: clean default build passed in 54.78 s, RAM 53,116 bytes / flash 932,773 bytes,
no compiler warnings/errors; `.tools/mission-m9-build.log`. Full native test
runner passed (`.tools/mission-m9-final-tests.log`), including the production
mission/flight/targeted-collection simulation. Existing Tello/network suites
passed. Four new HTTP mock tests and ten existing mock protocol tests passed.
The storage/sensor HTTP/measurement codec/backend sync production source files
remain byte-for-byte unchanged from HEAD. Secrets.h remains Git-ignored.

The storage_diagnostics environment also built successfully in 55.48 s with
no compiler warnings/errors (RAM 53,116 bytes, flash 929,589 bytes);
`.tools/mission-diagnostics-build.log`.

Independent read-only code review found a READY-abort/manual-flight bug. A
regression failed before the fix: abort could persist LANDED while flight was
possible. The fix checks actual aircraft/network state and otherwise enters
strict telemetry-confirmed LANDING. Regression and reviewer recheck passed;
reviewer reported no further concrete high-impact findings.

## Completion audit (software scope)

| Requirement | Current evidence |
| --- | --- |
| Review backend; no human JWT; no backend edits | M1 source review above; MissionHttpTransport uses existing device key headers |
| One ESP32, AP retained, Internet-only backend traffic | GatewayWiFiManager role gates; network tests; mission client gating tests |
| Automatic assigned SCHEDULED pull and explicit start | MissionBackendClient timed polling/identity checks; manager ground/live tests |
| READY not replaced, no download takeoff | Manager READY tests and outgoing packet assertions |
| Dedicated modules, bounded JSON/route/storage | MissionModel, MissionStorage, MissionBackendClient, MissionRoutePlanner, MissionManager; capacity tests |
| Verified LittleFS replacement and reboot handling | Storage fault tests; pre-command persistence and manager recovery tests |
| Local coordinates, sequence, targets and segmentation | Planner/codec tests; fixture contracts; no GPS command path |
| Configurable 40/35/20 limits, reject over-limit | Config.h; 35/40 accept, 50 reject, 60-config acceptance tests |
| Shared manual/mission altitude boundary | Direct Tello UDP tests, active RC cutoff/stale tests |
| Derived soft limits and invalid configuration | FlightSafetyController tests; startup log; takeoff/start configuration guards |
| Post-takeoff stabilization, no blind descent | ACK/new-sample tests; full mission simulation starts at 60 cm |
| Ground mode default and no movement | Separate default/live native builds; default firmware config remains enabled |
| Local mission APIs and altitude status fields | ApiServer routes/status serialization; default firmware build; manual HTTP procedures |
| Existing sensor collection and save-before-ACK | Production CollectionManager fault tests and full mission simulation |
| Explicit sensorNodeId/deviceCode mapping | Model/planner target checks; documented backend gap |
| Abort, landing confirmation, no early network switch | Landing timeout/conflicting telemetry/operator retry tests; READY-abort regression |
| Sensor uploads before mission result; durable retry | Sync fault tests and ordered full simulation |
| Preserve earlier functionality | Tello/network regressions; unchanged sensor/storage/backend implementations; builds |
| Build after each M1-M9 and inspect warnings | Nine phase logs listed above, plus final clean build |
| File list, commands, Serial, tests and limitations | MISSION_FILES.md and MISSION_OPERATIONS.md |
| No claims of actual backend/flight validation | Explicit exclusions in runbook; no upload/flight tool calls performed |

Physical validation and missing real backend endpoints are explicitly outside
the completed software verification. The runbook lists the remaining physical
checks and the exact ground/first-flight/full integration procedures. No task
scope was replaced by mock-only execution: mocks replace external boundaries,
while the same production mission, flight, planner, storage and collection
components implement the actual firmware path.
