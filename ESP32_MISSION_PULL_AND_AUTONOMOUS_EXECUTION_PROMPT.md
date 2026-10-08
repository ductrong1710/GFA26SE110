# ESP32 Mission Pull + Autonomous Tello Execution Prompt

Read the entire current ESP32 branch before making changes. Do not rewrite the project from scratch.

## Goal

Implement this workflow using ONE ESP32:

Web creates/edits/schedules a Mission -> Backend stores Mission + Waypoints -> ESP32, while connected to Internet, automatically pulls the next mission assigned to its Gateway -> validates it -> persists it in LittleFS -> becomes READY -> waits for an explicit START action -> switches STA from Internet to Tello -> enters Tello SDK mode -> autonomously executes the route -> collects ESP8266 sensor data at mission targets -> lands -> confirms landing -> switches STA back to Internet -> syncs pending sensor data -> uploads mission result/status.

Do NOT automatically take off immediately after downloading a mission.

## Existing backend contract

Inspect the BackEnd branch first.

Current backend mission model already has:
- GatewayId
- UavId
- statuses: PENDING, SCHEDULED, RUNNING, COMPLETED, FAILED, CANCELLED
- waypoints with SequenceNo, Latitude, Longitude, LocalX, LocalY, AltitudeM, ActionType, PlannedHoldSeconds
- mission targets with SensorNodeId

The human/web mission controller currently uses `/api/missions` and is user-authorized.

CRITICAL:
Do NOT store a human JWT or human credentials in ESP32.
Do NOT pretend `/api/missions` is device-accessible if it is not.

If a device-authenticated mission endpoint already exists, use it.
If not, implement the ESP32 side with a configurable adapter + MOCK mode and clearly document the backend endpoint still required.

Expected future device endpoint, only if no real endpoint exists:

GET /api/device/gateways/{gatewayId}/missions/next

The response should contain one SCHEDULED mission assigned to that gateway, with waypoints and enough sensor identity for ESP32 to resolve deviceCode.

## Architecture to add

Create clean modules:

- MissionBackendClient
- MissionStorage
- MissionRoutePlanner
- MissionManager

Do not put mission logic inside ApiServer, StorageManager, or TelloController.

MissionBackendClient:
- Internet-only backend communication
- pull next mission
- bounded JSON parsing
- gateway/device auth
- mission status/result upload
- never use backend while STA is connected to Tello

MissionStorage:
- LittleFS persistence
- `/mission/active.json`
- `/mission/state.json`
- `/mission/result.json`
- safe temp-file/replace pattern
- restore READY mission after reboot
- never overwrite a valid mission with a failed download

MissionRoutePlanner:
- validate and translate backend waypoints
- Tello prototype must use LocalX / LocalY / AltitudeM
- DO NOT execute Latitude/Longitude as GPS waypoints
- reject GPS-only missions as unsupported
- use backend coordinate convention if documented
- otherwise use and document:
  +X = forward from takeoff heading
  -X = backward
  +Y = right
  -Y = left
  origin = takeoff position

MissionManager:
- mission state machine
- explicit start
- route execution
- integrate existing TelloController
- integrate altitude/safety controller
- integrate CollectionManager
- land
- switch Internet after landing
- post-land sync
- abort / land-now handling

## Mission state machine

Use a bounded state machine similar to:

Empty
Downloading
Ready
PreparingTello
TakingOff
Executing
Collecting
Landing
Landed
ConnectingInternet
Syncing
Completed
Failed
Aborted
RecoveryRequired

Do not infer mission state only from Wi-Fi state.

## Pull rules

Automatically pull only when:

- STA target == Internet
- Internet STA connected
- Tello is not flying
- mission is not RUNNING
- no active READY mission is already stored
- no mission download already in progress

Use non-blocking polling:
MISSION_PULL_INTERVAL_MS = configurable, e.g. 10-30 seconds.

For this prototype support only ONE active mission.

Mission selection should be backend-controlled:
GatewayId == BACKEND_GATEWAY_ID
Status == SCHEDULED

Do not let ESP32 choose randomly among many missions.

## Download rules

Flow:

backend response
-> parse bounded JSON
-> validate full mission
-> verify GatewayId
-> verify waypoint count <= MAX_MISSION_WAYPOINTS
-> verify unique SequenceNo
-> verify supported route representation
-> verify altitude within safety ceiling
-> verify supported ActionType
-> write LittleFS temp file
-> verify write
-> replace active mission
-> set local state READY

Download != START.

Never auto-takeoff after download.

## Local mission REST API

Extend the existing ApiServer with:

GET  /api/mission/status
GET  /api/mission/active
POST /api/mission/pull
POST /api/mission/start
POST /api/mission/abort
POST /api/mission/land

`/pull` is a manual debug trigger. Automatic polling still exists.

Example status:

{
  "success": true,
  "missionId": 15,
  "missionName": "FARM-MISSION-001",
  "state": "READY",
  "currentWaypointIndex": 0,
  "waypointCount": 5,
  "flightState": "LANDED_READY",
  "staTarget": "INTERNET",
  "pendingUploadRecords": 22,
  "lastError": null
}

## START preconditions

Reject `/api/mission/start` unless:

- valid active mission exists
- local mission state == READY
- storage healthy
- route valid
- Tello not already flying
- no conflicting command active
- safety/battery checks pass when available

On START:

READY
-> PREPARING_TELLO
-> request STA target TELLO
-> connect Tello
-> enter SDK with `command`
-> validate battery/safety
-> TAKEOFF
-> execute route

Do not depend on Internet after switching to Tello.

## Tello route rules

Reuse existing TelloController. Do not duplicate UDP code.

For the first prototype:
- sort route by SequenceNo
- navigate local waypoints
- split oversized movement into bounded segments
- wait for Tello ACK / command completion before next movement
- never queue stale movement commands
- all altitude changes must pass through existing altitude safety
- if mission requests altitude above max ceiling, reject the mission rather than silently clamping

Suggested config:
MAX_MISSION_WAYPOINTS
MAX_MISSION_TARGETS
MISSION_MAX_MOVE_SEGMENT_CM
MISSION_SENSOR_WAIT_TIMEOUT_MS
MISSION_PULL_INTERVAL_MS

## Sensor collection at waypoint

Do NOT duplicate sensor collection logic.

Reuse:
- NodeRegistry
- CollectionManager
- SensorNodeClient
- StorageManager

At COLLECT waypoint:

reach waypoint
-> hover/hold
-> wait for expected sensor node
-> require authenticated + online node
-> CollectionManager collects
-> save-before-ACK remains unchanged
-> mark mission target result
-> continue

Backend target currently uses SensorNodeId while ESP32 identifies sensor by deviceCode.

Do NOT guess this mapping.

If backend device payload does not expose deviceCode, document this as a backend contract gap and require the device mission endpoint to return both SensorNodeId and deviceCode.

Example target payload:

{
  "sensorNodeId": 5,
  "deviceCode": "SENSOR-001",
  "waypointId": 100,
  "sequenceNo": 1,
  "status": "PENDING"
}

## Landing and network switch

Never switch STA away from Tello while aircraft may still be airborne.

Required:

LAND command
-> Tello accepts land
-> landing confirmation
-> Mission state LANDED
-> stop Tello protocol
-> STA target INTERNET
-> connect hotspot/router
-> NTP allowed again
-> BackendSyncManager resumes
-> mission result/status upload resumes

Telemetry loss while flying is NOT proof of landing.

## Post-land sync order

After confirmed landing:

1. switch STA to Internet
2. reconnect Internet
3. NTP/time if needed
4. existing BackendSyncManager uploads pending sensor data
5. MissionBackendClient uploads mission result/status
6. only then local mission may become fully completed/archivable

Backend failure must never delete pending sensor data or mission result.

## Mission result persistence

Persist a small result record before backend upload, e.g.:

{
  "missionId": 15,
  "finalState": "COMPLETED",
  "completedWaypoints": 5,
  "failedWaypointSequence": null,
  "collectedTargets": [
    {
      "sensorNodeId": 5,
      "deviceCode": "SENSOR-001",
      "status": "COLLECTED"
    }
  ],
  "failureReason": null
}

If current backend device result endpoint does not exist, do not pretend it exists. Document the required endpoint.

## Reboot safety

If ESP32 reboots with a mission in READY:
- restore READY

If it reboots while state says TakingOff / Executing / Collecting / Landing:
- DO NOT blindly resume flight
- enter RecoveryRequired
- require explicit operator decision
- preserve mission, result and sensor data

## Abort

POST /api/mission/abort

If landed:
- mark aborted locally

If possibly flying:
- stop future route steps
- send neutral RC if applicable
- request normal LAND
- wait for landing confirmation
- then switch Internet
- sync result

Do NOT use Tello `emergency` for ordinary abort.

## Non-blocking loop

Keep cooperative updates.

Conceptually:

void loop() {
    StorageDiagnostics::update(storage);
    wifiManager.update();
    apiServer.handleClient();

    telloController.update();
    flightSafetyController.update();
    flightStateManager.update();

    missionBackendClient.update();
    missionManager.update();

    timeManager.update(wifiManager.isInternetNetworkReady());

    nodeRegistry.update();
    collectionManager.update();
    backendSyncManager.update();

    yield();
}

No long blocking waits.

## MOCK mode

If backend device mission endpoint is not implemented yet, add a development-only mission source abstraction / MOCK_MISSION_BACKEND.

Mock mode must feed the SAME MissionManager, MissionStorage, MissionRoutePlanner, TelloController and CollectionManager.

Only the mission source may be mocked.

## Implementation phases

M1 - inspect backend mission contract and document device API gaps
M2 - mission models + MissionStorage
M3 - MissionBackendClient + mock source
M4 - route validation + MissionRoutePlanner
M5 - MissionManager + local start/abort API
M6 - sensor target integration
M7 - landing + Tello -> Internet transition
M8 - mission result + existing sensor sync
M9 - full integration and docs

After EACH phase:
1. PlatformIO build
2. fix all compile errors
3. inspect warnings
4. preserve previous working behavior
5. continue only after build succeeds

## Required tests

Without flight:
- no Internet -> no pull
- Internet -> pull attempt
- invalid JSON -> old mission preserved
- wrong GatewayId -> reject
- too many waypoints -> reject
- duplicate SequenceNo -> reject
- GPS-only mission -> reject
- LocalX/LocalY mission -> accept
- reboot after download -> READY restored
- download does not auto-start
- route planner can be inspected without sending flight commands

Ground/Tello:
- mission READY
- START
- switch to Tello
- SDK ready
- ground-test mode prevents takeoff
- abort safely
- switch back to Internet while known landed

First live mission:
- takeoff
- stabilize
- one short local move
- hold
- land
- reconnect Internet
- report result

Final demo:
1. Web creates/schedules mission.
2. Assign Gateway/UAV.
3. ESP32 on Internet pulls mission.
4. Validate + persist.
5. READY.
6. Operator START.
7. Switch to Tello.
8. Takeoff.
9. Execute route.
10. Collect expected sensor.
11. Save measurement before ACK.
12. Land.
13. Confirm landed.
14. Switch to Internet.
15. Sync sensor data.
16. Sync mission result.
17. Mission ends COMPLETED/FAILED.

## Do not implement

- second ESP32
- Raspberry Pi
- direct PostgreSQL from ESP32
- fake GPS navigation
- SLAM
- computer vision
- video streaming
- swarm control
- human JWT credentials stored in ESP32
- direct backend steering during flight

## Final report

When finished report:
- actual backend mission API discovered
- device-auth gaps
- required future device mission endpoint if missing
- files created/modified
- mission state machine
- route coordinate convention
- LittleFS mission files
- local mission API
- build/upload/monitor commands
- no-flight test
- first live mission test
- full integration test
- known Tello limitations
- backend TODOs
- hardware TODOs

Do not claim backend mission pull works unless a compatible device-authenticated backend endpoint actually exists.
Do not claim autonomous flight validation unless physically tested.
