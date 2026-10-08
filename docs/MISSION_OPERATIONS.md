# Mission pull and autonomous Tello execution

One ESP32 retains UAV_GATEWAY at 192.168.4.1 while STA switches Internet/Tello.
Downloading never starts flight. `POST /api/mission/start` explicitly authorizes
execution. No firmware upload, real flight, physical altitude-control or
power-interruption validation was performed for this change.

## Configuration and safety

Central values in `include/Config.h`:

```cpp
constexpr int TELLO_MAX_ALTITUDE_CM = 40;
constexpr int TELLO_TARGET_ALTITUDE_CM = 35;
constexpr int TELLO_MIN_OPERATION_ALTITUDE_CM = 20;
#define GATEWAY_GROUND_TEST_MODE 1
```

Changing the maximum to 60 or 100 requires no mission execution edits. Values
must satisfy 0 < minimum <= target <= maximum. Invalid configuration logs an
error and blocks start. Ground mode blocks takeoff/nonzero motion at the UDP
boundary while allowing pull, planning, SDK, battery and telemetry checks.

Limits: 16 waypoints, 8 targets, 128 route steps, 100 cm movement segments,
20 s polling, 30 s sensor wait, 30 s maximum waypoint hold, 8 KiB files and
bounded 16 KiB JSON documents. Set BACKEND_GATEWAY_ID to a positive provisioned
ID; default 0 intentionally disables mission pull.

Use ignored `Secrets.h` for Internet/Tello SSID/password and BACKEND_API_KEY.
BACKEND_BASE_URL includes `/api`, e.g. `http://192.168.1.100:5000/api`.
Existing Secrets.example.h documents those fields. No human credentials/JWT
are used, and mission objects/logs/responses contain no device keys.

MOCK_MISSION_BACKEND=false uses the HTTP adapter. Set true explicitly for an
internal single short HOLD mission. Internal mock acceptance sets
mockResultUpload=true and never contacts a backend. The HTTP mock below permits
editable missions and sensor collection tests through the real HTTP adapter.

**Tello's internal takeoff controls its own initial ascent: firmware cannot
guarantee a strict 40 cm ceiling during that transient.** After takeoff ACK,
mission waits for new telemetry, descends in bounded 20 cm commands with ACK
and new-sample waits, and uses slow bounded RC pulses for finer adjustment.
RC returns to neutral before another sample is assessed. Horizontal route
movement waits for stable altitude in range and near the target.

Positive RC is reduced at maximum-minus-10 and maximum-minus-5 cm, then blocked
at the maximum. Thresholds cannot become negative. Stale altitude blocks ascent.
Manual up/down and RC pass through the same safety controller. Timeout stops
route execution and requests normal landing, never infers landing from loss.

## Backend gap and proposed device contract

The current .NET backend `/api/missions` uses human authorization policies.
Device authentication (`X-Gateway-Code`, `X-Api-Key`) currently covers sensor
sync and telemetry, **not mission pull/result**. No backend files were modified.
The following configurable endpoints still need real backend implementation:

- GET `/api/device/gateways/{gatewayId}/missions/next`: one assigned, due
  SCHEDULED mission, or 204. Selection/scheduling remains server-controlled.
- POST `/api/device/gateways/{gatewayId}/missions/{missionId}/result`: idempotent
  offline execution reconciliation, including RUNNING then terminal state.

The authenticated gateway_id must match the route/assignment. The device
response is a plain object, not the human API envelope:

```json
{
  "id": 102, "name": "SENSOR-MISSION", "gatewayId": 1, "uavId": 1,
  "status": "SCHEDULED",
  "waypoints": [
    {"id": 1002, "sequenceNo": 1, "localX": 0.2, "localY": 0,
     "altitudeM": 0.35, "actionType": "COLLECT", "plannedHoldSeconds": 2}
  ],
  "targets": [{"sensorNodeId": 5, "deviceCode": "SENSOR-001", "waypointId": 1002}]
}
```

Require positive IDs, bounded names/codes, unique waypoint IDs and sequence
numbers, explicit local coordinates/altitude and integer hold seconds.
Actions: MOVE, HOLD, COLLECT. Targets must reference COLLECT waypoints, each
COLLECT needs a target. The human target DTO lacks deviceCode: backend must
provide sensorNodeId + deviceCode explicitly. No guessed mapping is used.

Coordinates are meters at integer-centimeter precision: origin takeoff position,
+X forward from takeoff heading, +Y right. SequenceNo orders waypoints.
GPS-only and fractional-centimeter missions are rejected. 0.35 m becomes 35 cm,
0.40 becomes 40, 0.50 is rejected at the default ceiling with
MISSION_ALTITUDE_EXCEEDS_LIMIT. Nothing is silently clamped. Horizontal legs
must be zero or >=20 cm; longer legs split into bounded segments without an
undersized remainder. No route yaw/GPS navigation is used.

Result fields: missionId, gatewayId, fingerprint, finalState,
completedWaypoints, failedWaypointSequence, failureReason, collectedTargets
(sensorNodeId, deviceCode, status). Local ABORTED maps to backend CANCELLED.
The server must reply HTTP 200 with a matching acknowledgement:

```json
{"success":true,"missionId":102,"fingerprint":123456789}
```

Fingerprint is a consistency tag, not cryptographic authentication. Bare HTTP
200 is insufficient. Retries must be idempotent; an offline mission may still
be SCHEDULED server-side, so existing human transitions alone are insufficient.

## State and persistence

Live sequence: EMPTY -> READY -> PREPARING_TELLO -> TAKING_OFF -> STABILIZING ->
EXECUTING -> COLLECTING at targets -> LANDING -> LANDED -> CONNECTING_INTERNET ->
SYNCING -> COMPLETED / FAILED / ABORTED. Ground mode stops at GROUND_READY.
Failed/uncertain land enters RECOVERY_REQUIRED; explicit land/abort permits
another attempt. Ordinary abort never uses emergency motor stop.

Files: `/mission/active.json`, `/mission/state.json`, `/mission/result.json`.
Writes use verified `/mission/write.tmp` then LittleFS rename without deleting
the destination first. State/result bind to mission ID/fingerprint. Downloads
validate before replacement. READY and pending results cannot be overwritten.

READY restores after reboot. Flight/preparation uncertainty or mismatched files
enter RECOVERY_REQUIRED and select Tello before Wi-Fi startup. No flight resumes
automatically. Corrupt metadata still permits `/land` once SDK reconnects;
files remain for diagnosis. Repair only after physically confirmed landing;
never erase sensor queues to clear a mission or reboot during flight.

Mission owns prepare/takeoff/move/rotate/RC/disconnect while running (manual
API returns 409). Manual land routes through mission abort. READY abort also
checks actual aircraft state when manual flight is active.

Collection reuses NodeRegistry, SensorNodeClient, CollectionManager and
StorageManager. Only the expected authenticated online device is selected.
Verified saved/duplicate records may be ACKed; failed writes are excluded.
Failed ACK/no data cannot mark a target collected. Background collection pauses
while mission owns flight and resumes afterward. Target wait is bounded.

Landing requires ACK plus stable low h/tof and vertical velocity; conflicting
h/tof or telemetry loss cannot confirm landing. Only afterward may STA switch
to Internet. Sensor sync runs first. Mission upload waits for valid time and
zero pending sensor records, then durably stores matching acknowledgement.
Failures retain data/results with bounded retry. Invalid sensor timestamps
remain pending; no timestamps are invented to force completion.

## Build, test, manual upload

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t clean
.\.tools\platformio\Scripts\pio.exe run -e esp32dev
powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_mission_host.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tello_host.ps1
.\.tools\platformio\Scripts\python.exe -m unittest discover -s tests -p 'test_mock*.py' -v
```

Host suites compile production logic with simulated transport/clock/storage
boundaries; they are not physical validation. Python suites test mock tools.
Close existing Serial Monitor before manual upload:

```powershell
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t upload --upload-port COM3
.\.tools\platformio\Scripts\pio.exe device monitor --port COM3 --baud 115200
```

If needed, hold BOOT during Connecting, tap EN, release BOOT once writing starts.
Do not use uploadfs over stored measurements/missions.

## Local API and no-flight test

Join UAV_GATEWAY. Browser GET URLs:

- http://192.168.4.1/api/mission/status
- http://192.168.4.1/api/mission/active
- http://192.168.4.1/api/mission/active?routeOffset=0
- http://192.168.4.1/api/tello/status
- http://192.168.4.1/api/gateway/status

The active response exposes routeSteps in groups of eight; follow
nextRouteOffset. GET does not send flight commands. Both status endpoints expose
configuredMaxAltitudeCm, configuredTargetAltitudeCm, currentAltitudeCm (-1 if
unavailable), altitudeTelemetryFresh and altitudeLimitActive.

```powershell
$gateway = 'http://192.168.4.1'
Invoke-RestMethod "$gateway/api/mission/status" | ConvertTo-Json -Depth 8
Invoke-RestMethod "$gateway/api/mission/active" | ConvertTo-Json -Depth 10
Invoke-RestMethod "$gateway/api/tello/status" | ConvertTo-Json -Depth 8
curl.exe -i -X POST "$gateway/api/mission/pull"
```

Keep ground mode=1, MOCK_MISSION_BACKEND=false and gateway ID=1. Configure the
backend base to the laptop's router/hotspot address (not its UAV_GATEWAY-side
address), with a development key. Permit inbound port 5000 where necessary.
Run in a separate terminal:

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
Copy-Item tools\fixtures\mission-short.json .tools\test-mission.json
$env:MOCK_BACKEND_KEY = Read-Host 'Enter the development key configured in Secrets.h'
.\.tools\platformio\Scripts\python.exe tools\mock_mission_backend.py --mission .tools\test-mission.json --port 5000
```

Wait for automatic READY, inspect route, verify no takeoff log. While physically
grounded, press EN and verify READY restores. `/pull` while READY returns 409
ACTIVE_MISSION_RETAINED. Power on grounded Tello with its exact SSID configured:

```powershell
Invoke-RestMethod "$gateway/api/mission/start" -Method Post
Start-Sleep -Seconds 8
Invoke-RestMethod "$gateway/api/mission/status" | ConvertTo-Json -Depth 8
Invoke-RestMethod "$gateway/api/tello/status" | ConvertTo-Json -Depth 8
```

Poll until GROUND_READY, SDK ready and battery available; connection time varies.
No takeoff/motion is permitted. Wait for stable ground telemetry, then:

```powershell
Invoke-RestMethod "$gateway/api/mission/abort" -Method Post
Start-Sleep -Seconds 8
Invoke-RestMethod "$gateway/api/mission/status" | ConvertTo-Json -Depth 8
```

Expect Internet, time/sensor sync, then ABORTED and resultUploaded=true. This is
HTTP mock acceptance, not .NET backend integration. Inspect lastError and pending
count if SYNCING remains. Elapsed time never proves physical landing.

For invalid download tests, use a NEW mission ID after prior result acknowledgement.
Edit `.tools/test-mission.json` (mock reloads on GET): malformed JSON, wrong
gatewayId, missing local coordinates, duplicate sequence, >16 waypoints, or
altitudeM=0.50. Candidates must not replace the previous active file/become READY.
Restore a valid file to continue. A READY mission never attempts replacement.

## First live mission

After ground tests, manually set ground mode=0, build/upload while grounded.
Use a NEW ID and the short fixture (20 cm forward, 2 s hold):

```powershell
$m = Get-Content tools\fixtures\mission-short.json -Raw | ConvertFrom-Json
$m.id = 201
$m | ConvertTo-Json -Depth 10 | Set-Content .tools\test-mission.json -Encoding ascii
```

Wait for READY, inspect route/configuration and confirm battery >=25%.
With a clear test area and aircraft observed, start explicitly:

```powershell
Invoke-RestMethod "$gateway/api/mission/start" -Method Post
```

Expect SDK checks -> takeoff -> fresh telemetry -> stabilize near 35 cm -> short
move -> hold -> normal land -> confirmed ground -> Internet -> synchronization.
Operator stop:

```powershell
Invoke-RestMethod "$gateway/api/mission/land" -Method Post
```

Do not reboot or force a network switch while possibly airborne.

## Full integration test

Use `tools/fixtures/mission-collect.json` with a new ID and provisioned SENSOR-001.
Provide valid timestamped pending measurements at the COLLECT waypoint. Background
collection can drain data before start; provide new data at the target. A laptop
mock sensor can start after preparation begins, connected to UAV_GATEWAY:

```powershell
$env:MOCK_DEVICE_TOKEN = Read-Host 'Enter SENSOR-001 configured token'
.\.tools\platformio\Scripts\python.exe tools\mock_sensor.py --gateway http://192.168.4.1 --code SENSOR-001 --records 25 --start 1000 --port 80
```

Keep the mock backend reachable through separate Internet-side connectivity
(Ethernet/another adapter/another host). First finish ground and short-flight
tests. Verify save -> ACK -> confirmed land -> sensor upload -> mission result.
Inspect `.tools/mock-mission-results.json` and `.tools/mock-mission-sensors.json`.

Actual Web -> .NET -> ESP32 demonstration requires the missing backend endpoints.
After those exist, create/schedule through Web, assign gateway/UAV, expose
deviceCode mapping and repeat with the real backend URL/key. Mock results do
not prove that integration.

## Expected Serial excerpts

```text
[GATEWAY] Boot
[GATEWAY] LittleFS mounted
[GATEWAY] AP started
[GATEWAY] SSID: UAV_GATEWAY
[GATEWAY] AP IP: 192.168.4.1
[API] HTTP server started on port 80
[MISSION] READY
[MISSION] PREPARING_TELLO
[WIFI] STA target=TELLO; SoftAP remains enabled
[TELLO] Command: command
[TELLO] Command: battery?
[MISSION] GROUND_READY
```

Live mode replaces GROUND_READY with TAKING_OFF, STABILIZING, EXECUTING and
COLLECTING at targets; then LANDING, LANDED, CONNECTING_INTERNET, SYNCING and
terminal state. Other logs depend on actual runtime conditions.

## Limits and hardware work remaining

Measure h/tof accuracy near the floor, low-altitude ground effect, correction
rate, drift, stability, AP channel reconnection, radio loss and physical power
interruption. This firmware cannot eliminate takeoff overshoot/inertia or claim
those physical checks from simulations. Local coordinates are relative command
distances, not GPS/localization feedback. UDP replies have no transaction IDs.
Motion is not automatically retried. SDK auto-land on inactivity does not prove
landing to the gateway. Sensor HTTP uses bounded synchronous calls; the loop is
cooperative, not hard real-time. Local controls/shared-key HTTP remain prototype
security, without TLS or operator authentication.

No second ESP32, GPS navigation, SLAM, vision, video, direct database access or
backend steering during flight was added.

## Changed files

Created firmware headers: FlightSafetyController, MissionBackendClient,
MissionCollection, MissionManager, MissionModel, MissionRoutePlanner,
MissionStorage under `include/`. Created corresponding `.cpp` implementations
(except interface-only MissionCollection), plus MissionHttpTransport.cpp and
MissionLittleFS.cpp under `src/`.

Modified firmware: Config.h; ApiServer.h/.cpp; CollectionManager.h/.cpp;
FlightStateManager.h/.cpp; GatewayWiFiManager.h/.cpp; TelloController.h/.cpp;
main.cpp. Existing SensorNodeClient, measurement codec, StorageManager and
BackendSyncManager implementations are reused.

Created test/tool files: run_mission_host.ps1, host/mission_*_tests.cpp,
host/tello_safety_tests.cpp, host/MissionMemoryFiles.h, host/FS.h,
test_mock_mission.py, tools/mock_mission_backend.py and two JSON fixtures.
Updated host Arduino shim, Tello/network tests and run_tello_host.ps1.
Added MISSION_IMPLEMENTATION.md and this guide; README links the guide.
Pre-existing user prompt-file changes were preserved.
