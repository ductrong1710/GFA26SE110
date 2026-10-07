# ESP32 + DJI/Ryze Tello Control + Post-Landing Sync — Coding Prompt

You are a senior Embedded/IoT engineer.

Work on the existing **ESP32 branch** of this repository. Do not rewrite the project from scratch.

## Current project state

The current ESP32 firmware already implements:

- ESP32 Dev Module / PlatformIO / Arduino
- LittleFS persistent measurement storage
- `UAV_GATEWAY` SoftAP
- AP + STA operation
- authenticated sensor-node registry
- sensor collection from ESP8266 nodes
- save-before-ACK semantics
- duplicate prevention
- time management
- backend synchronization
- local REST API

The new requirement is to use **the same single ESP32** for all of these roles:

1. Mobile IoT gateway for ESP8266 nodes.
2. Controller for one DJI/Ryze Tello drone over the Tello Wi-Fi SDK.
3. After the Tello has safely landed, disconnect the ESP32 STA interface from the Tello network.
4. Connect that same STA interface to the configured Internet/hotspot Wi-Fi.
5. Synchronize pending sensor measurements to the existing .NET backend.
6. Before the next flight, switch the STA interface back from Internet Wi-Fi to the Tello Wi-Fi.

Do not add a second ESP32.

---

## 1. Existing architecture that must be preserved

```text
ESP8266 Sensor Nodes
       |
       | connect to ESP32 SoftAP
       v
ESP32
SoftAP = UAV_GATEWAY
       |
       +--> NodeRegistry
       +--> CollectionManager
       +--> LittleFS
       +--> BackendSyncManager
```

The existing source contains classes including:

```text
GatewayWiFiManager
ApiServer
NodeRegistry
NodeAuthenticator
SensorNodeClient
CollectionManager
StorageManager
TimeManager
BackendSyncManager
BackendAdapter
```

Preserve these responsibilities.

Do not destroy or replace working Phase 1–8 behavior.

---

## 2. One ESP32 means one STA connection at a time

The ESP32 SoftAP must remain:

```text
SSID: UAV_GATEWAY
IP:   192.168.4.1
```

for:

- ESP8266 sensor nodes
- phone/laptop local control

The ESP32 STA interface can connect to only one external AP at a time.

Therefore STA must explicitly switch between:

```text
TELLO network
```

and:

```text
Internet/hotspot network
```

Do NOT attempt to keep the ESP32 STA connected to Tello and Internet simultaneously.

---

## 3. STA target state

Refactor `GatewayWiFiManager` to use an explicit target.

Suggested:

```cpp
enum class StaTarget {
    None,
    Tello,
    Internet
};
```

### Tello mode

```text
SoftAP:
UAV_GATEWAY remains active

STA:
connected to TELLO-XXXXXX

Purpose:
Tello command + telemetry
```

### Internet mode

```text
SoftAP:
UAV_GATEWAY remains active

STA:
connected to configured hotspot/router

Purpose:
NTP + backend synchronization
```

Do not intentionally stop the SoftAP while changing STA targets.

---

## 4. AP+STA channel behavior

ESP32 AP+STA share one Wi-Fi radio/channel.

When STA connects to Tello or to an Internet hotspot, the SoftAP channel can follow the STA network channel.

Therefore:

- do not assume `UAV_GATEWAY` always stays on one channel
- ESP8266 nodes may temporarily disconnect during STA network changes
- existing ESP8266 reconnect behavior must recover
- do not permanently remove nodes because of a short target/channel transition
- log mode changes
- do not attempt to operate AP and STA on independent channels

---

## 5. Tello SDK network

Use the official Tello Wi-Fi UDP SDK.

Defaults:

```text
Tello IP:       192.168.10.1
Command UDP:    8889
State UDP:      8890
Video UDP:      11111
```

Video is NOT required.

Do NOT enable or process Tello video.

Before any normal control commands send:

```text
command
```

to:

```text
192.168.10.1:8889
```

and require `ok` before declaring SDK mode ready.

Use `WiFiUDP`.

---

## 6. Configuration

Add to `Config.h` values similar to:

```cpp
TELLO_IP
TELLO_COMMAND_PORT = 8889
TELLO_STATE_PORT = 8890

TELLO_COMMAND_TIMEOUT_MS
TELLO_CONNECT_TIMEOUT_MS
TELLO_COMMAND_RETRY_COUNT

TELLO_MIN_TAKEOFF_BATTERY_PERCENT
TELLO_LAND_SETTLE_MS
TELLO_TELEMETRY_TIMEOUT_MS
TELLO_RC_INTERVAL_MS
```

Add to `Secrets.h` / `Secrets.example.h`:

```cpp
TELLO_SSID
TELLO_PASSWORD
```

Tello password may be empty if the aircraft network is open.

Never log Wi-Fi passwords.

---

## 7. TelloController

Create:

```text
include/TelloController.h
src/TelloController.cpp
```

Responsibilities:

- start/stop UDP sockets
- enter Tello SDK mode using `command`
- send only one normal SDK command at a time
- receive command responses on UDP 8889
- receive and parse state packets on UDP 8890
- maintain last telemetry
- maintain last response
- command timeout/retry
- non-blocking updates
- basic flight-control methods

Suggested API:

```cpp
bool begin();
void update();

bool requestSdkMode();

bool takeoff();
bool land();

bool moveUp(int cm);
bool moveDown(int cm);
bool moveLeft(int cm);
bool moveRight(int cm);
bool moveForward(int cm);
bool moveBack(int cm);

bool rotateClockwise(int degrees);
bool rotateCounterClockwise(int degrees);

bool sendRc(int leftRight, int forwardBack, int upDown, int yaw);

bool isSdkReady() const;
bool hasFreshTelemetry() const;

int batteryPercent() const;
int heightCm() const;
int tofCm() const;
int flightTimeSeconds() const;

String lastResponse() const;
```

You may improve the API if needed.

---

## 8. Required basic Tello commands

At minimum support:

```text
command
takeoff
land

up x
down x
left x
right x
forward x
back x

cw x
ccw x

rc a b c d

battery?
```

Do not implement flips yet.

Do not implement swarm features.

Do not implement mission-pad features.

Do not implement video.

---

## 9. RC control

Support:

```text
rc a b c d
```

where:

```text
a = left/right
b = forward/backward
c = up/down
d = yaw
```

Validate:

```text
-100 <= each channel <= 100
```

Requirements:

- rate-limit RC requests
- do not build an unbounded RC queue
- latest RC request may replace older unsent RC values
- never execute stale RC commands later
- support neutral `rc 0 0 0 0`
- RC processing must not block sensor collection or HTTP handling

---

## 10. Tello telemetry

Listen on UDP:

```text
8890
```

Parse available fields such as:

```text
pitch
roll
yaw
vgx
vgy
vgz
templ
temph
tof
h
bat
baro
time
agx
agy
agz
```

Create a small `TelloTelemetry` model.

Store at least:

```text
battery
height
tof
flightTime
verticalVelocity
attitude
lastReceivedMs
valid
```

Ignore malformed or unknown telemetry fields safely.

Do not persist high-frequency telemetry to LittleFS.

---

## 11. Flight state machine

Create:

```text
include/FlightStateManager.h
src/FlightStateManager.cpp
```

Suggested states:

```text
IdleInternet
SwitchingToTello
ConnectingTello
EnteringSdk
LandedReady
TakingOff
Flying
Landing
LandedConfirmed
SwitchingToInternet
InternetConnecting
Syncing
SyncComplete
Error
```

Do not infer flight state only from Wi-Fi connection.

---

## 12. Prepare-flight flow

When the operator prepares the drone:

```text
POST /api/tello/prepare
        |
        v
STA target = TELLO
        |
        v
disconnect Internet STA
        |
        v
connect TELLO_SSID
        |
        v
start UDP sockets
        |
        v
send "command"
        |
        v
receive "ok"
        |
        v
query battery
        |
        v
LandedReady
```

Do NOT automatically take off just because Tello Wi-Fi connected.

Takeoff must be a separate explicit request.

---

## 13. Takeoff validation

Before sending `takeoff`, require:

- STA target is Tello
- Tello Wi-Fi is connected
- SDK mode is ready
- no conflicting command pending
- flight state is `LandedReady`
- battery is known and above configurable minimum when battery query succeeds

If battery cannot be read, return a clear state/error rather than inventing a value.

---

## 14. Landing flow

```text
POST /api/tello/land
        |
        v
send "land"
        |
        v
wait for response
        |
        v
response == ok
        |
        v
Landing
        |
        v
confirm landing
        |
        v
LandedConfirmed
```

Do NOT disconnect from Tello immediately after merely sending `land`.

---

## 15. Landing confirmation

Only release the Tello Wi-Fi link after landing is reasonably confirmed.

At minimum:

1. `land` gets `ok`
2. wait a configurable settling period
3. if fresh telemetry exists, require low/stable `h` or `tof` and near-zero vertical velocity for several samples

If telemetry is not available after an `ok` landing response:

- use a conservative settling timeout
- log that landing confirmation used command ACK + timeout rather than telemetry

Never treat telemetry loss while flying as proof that the aircraft landed.

Never switch to Internet simply because telemetry disappeared.

---

## 16. Automatic post-land switch

After `LandedConfirmed`:

```text
Tello landed
      |
      v
stop Tello UDP protocol
      |
      v
GatewayWiFiManager target = Internet
      |
      v
disconnect Tello STA
      |
      v
connect INTERNET_SSID
      |
      v
wait for Internet Wi-Fi
      |
      v
NTP/time synchronization
      |
      v
BackendSyncManager uploads pending measurements
```

This switching must be automatic after confirmed landing.

---

## 17. Backend must never sync over Tello Wi-Fi

This is critical.

Existing code currently uses general STA connectivity in parts of the system.

After this change:

```text
WiFi.status() == WL_CONNECTED
```

does NOT mean backend networking is available.

The ESP32 could be connected to Tello.

Create a clear API such as:

```cpp
wifiManager.isBackendNetworkReady()
```

or equivalent.

`BackendSyncManager` may upload only when:

```text
STA target == Internet
AND Internet STA is connected/usable
```

Preserve the existing backend contract:

```text
BACKEND_BASE_URL
BACKEND_API_KEY
BACKEND_GATEWAY_ID
BACKEND_SENSOR_BATCH_ENDPOINT
BackendAdapter
X-Gateway-Code
X-Api-Key
```

Do not invent a new backend endpoint.

---

## 18. TimeManager must not use Tello STA as Internet

The existing `main.cpp` currently updates TimeManager using STA connectivity.

Change the logic so NTP attempts only occur when the STA target is Internet and the Internet network is usable.

For example:

```cpp
timeManager.update(wifiManager.isInternetNetworkReady());
```

not:

```cpp
timeManager.update(wifiManager.isStaConnected());
```

Preserve a previously synchronized time offset while flying offline.

---

## 19. Sensor collection during flight

During flight:

```text
ESP32 SoftAP:
UAV_GATEWAY

ESP8266 nodes:
connected to UAV_GATEWAY

ESP32 STA:
connected to TELLO-XXXXXX

CollectionManager:
continues collecting

StorageManager:
persists data

BackendSyncManager:
does NOT upload
```

Do not break existing data safety:

```text
ESP8266 sends
-> ESP32 validates
-> ESP32 saves LittleFS
-> ESP32 ACKs ESP8266
```

Tello command handling must not block:

- ApiServer
- NodeRegistry
- CollectionManager
- StorageManager

---

## 20. Next mission

After synchronization, ESP32 may stay in Internet mode.

For the next mission:

```text
POST /api/tello/prepare
```

must switch:

```text
Internet STA
   |
   v
Tello STA
   |
   v
SDK mode
   |
   v
LandedReady
```

Never auto-takeoff.

---

## 21. Extend existing ApiServer

Add these endpoints without breaking existing gateway endpoints:

```text
GET  /api/tello/status
POST /api/tello/prepare
POST /api/tello/takeoff
POST /api/tello/land
POST /api/tello/move
POST /api/tello/rotate
POST /api/tello/rc
```

Optional:

```text
POST /api/tello/disconnect
```

only when aircraft is safely landed.

---

## 22. GET /api/tello/status

Example:

```json
{
  "success": true,
  "staTarget": "TELLO",
  "telloWifiConnected": true,
  "sdkReady": true,
  "flightState": "FLYING",
  "battery": 78,
  "heightCm": 85,
  "tofCm": 82,
  "flightTimeSec": 34,
  "telemetryFresh": true,
  "lastResponse": "ok",
  "pendingUploadRecords": 42
}
```

Never return passwords/secrets.

---

## 23. POST /api/tello/prepare

Return quickly.

Do not block the HTTP request until Wi-Fi finishes.

Example response:

```json
{
  "success": true,
  "state": "CONNECTING_TELLO"
}
```

---

## 24. POST /api/tello/takeoff

Only accept in `LandedReady`.

Example:

```json
{
  "success": true,
  "state": "TAKING_OFF"
}
```

Actual Tello response updates state asynchronously.

---

## 25. POST /api/tello/land

Example:

```json
{
  "success": true,
  "state": "LANDING"
}
```

After landing confirmation, switch automatically to Internet.

---

## 26. POST /api/tello/move

Request:

```json
{
  "direction": "forward",
  "distanceCm": 50
}
```

Allowed:

```text
up
down
left
right
forward
back
```

Validate the official Tello SDK range.

Do not concatenate unchecked user input into a command.

---

## 27. POST /api/tello/rotate

Request:

```json
{
  "direction": "cw",
  "degrees": 90
}
```

Allowed:

```text
cw
ccw
```

Validate the official range.

---

## 28. POST /api/tello/rc

Request:

```json
{
  "leftRight": 0,
  "forwardBack": 25,
  "upDown": 0,
  "yaw": 0
}
```

Validate all fields within `-100..100`.

Do not queue stale values.

---

## 29. Tello command serialization

Tello command/response traffic must be serialized.

Maintain at most one pending response-requiring command.

Track:

```text
command
sentAt
retryCount
expectedResponse
result/state transition
```

A timeout must not freeze the firmware.

---

## 30. Wi-Fi manager refactor

The current `GatewayWiFiManager` automatically reconnects to `INTERNET_SSID`.

Make it target-aware.

### Target == Tello

- never start Internet reconnect
- connect only to `TELLO_SSID`
- `isInternetConnected()` must be false
- backend sync inactive

### Target == Internet

- never reconnect to Tello
- existing Internet reconnect/probe logic may operate
- backend sync may resume

### Target == None

- SoftAP remains active
- STA may be disconnected

Suggested API:

```cpp
enum class StaTarget {
    None,
    Tello,
    Internet
};

void requestStaTarget(StaTarget target);

StaTarget staTarget() const;

bool isStaConnected() const;
bool isTelloConnected() const;
bool isInternetStaConnected() const;
bool isInternetNetworkReady() const;

IPAddress getStaIp() const;
```

Do not use only `WiFi.status()` to identify the network role.

---

## 31. Network switching sequence

When changing targets:

1. stop protocol clients using the old STA network
2. disconnect old STA
3. allow disconnect state/event
4. begin connection to target network
5. keep SoftAP enabled
6. report transition state through logs/API

Do not call `WiFi.begin()` every loop.

---

## 32. Failure handling

Handle without rebooting:

### Tello unavailable
- SoftAP remains active
- return connecting/error state

### SDK `command` timeout
- SDK not ready
- do not advance to flight-ready state

### Takeoff rejected
- remain safely landed/error

### Land timeout
- do not switch to Internet
- keep Tello link when possible

### Telemetry timeout while flying
- do not interpret as landing
- do not switch network

### Internet unavailable after landing
- keep data in LittleFS
- retry Internet STA non-blockingly

### Backend unavailable
- keep data PENDING
- use current retry/backoff

### ESP32 reboot
- LittleFS survives
- do not assume drone flight state
- require explicit Tello preparation before flight control

---

## 33. Main loop must remain non-blocking

Preserve cooperative updates.

Conceptually:

```cpp
void loop() {
    StorageDiagnostics::update(storage);

    wifiManager.update();
    apiServer.handleClient();

    telloController.update();
    flightStateManager.update();

    timeManager.update(wifiManager.isInternetNetworkReady());

    nodeRegistry.update();
    collectionManager.update();
    backendSyncManager.update();

    yield();
}
```

Exact ordering can be adjusted if necessary.

No multi-second blocking loops for:

- Wi-Fi
- UDP command ACK
- landing
- NTP
- backend sync

---

## 34. Files to add

Recommended:

```text
include/TelloController.h
src/TelloController.cpp

include/TelloTelemetry.h

include/FlightStateManager.h
src/FlightStateManager.cpp
```

Do not add unnecessary architecture.

---

## 35. Existing files likely to change

```text
include/Config.h
include/Secrets.example.h

include/GatewayWiFiManager.h
src/GatewayWiFiManager.cpp

include/ApiServer.h
src/ApiServer.cpp

src/BackendSyncManager.cpp
src/main.cpp

README.md
docs/OPERATIONS.md
```

Potentially `TimeManager` if required.

Do not make unrelated changes.

---

## 36. Current build compatibility

Keep:

```text
platform = espressif32@6.10.0
board = esp32dev
framework = arduino
ArduinoJson = 6.21.5
LittleFS
```

Use framework-provided:

```text
WiFi
WiFiUDP
```

Do not add a heavy Tello dependency unless absolutely necessary.

---

## 37. First ground test — no takeoff

Before live flight:

1. Power the Tello on a stable surface.
2. Configure `TELLO_SSID`.
3. Connect phone/laptop to `UAV_GATEWAY`.
4. Call `POST /api/tello/prepare`.
5. Verify ESP32 STA connects to Tello.
6. Verify `command` -> `ok`.
7. Verify `battery?`.
8. Verify telemetry on UDP 8890.
9. Verify `/api/tello/status`.
10. While still physically landed, test transition back to Internet.
11. Verify backend synchronization.

Only after this works perform takeoff.

---

## 38. First live flight test

First live flight test should be:

```text
prepare
-> SDK ready
-> battery query
-> takeoff
-> hover only
-> land
-> landing confirmed
-> switch Internet
-> backend sync
```

Do NOT test movement on the very first live flight.

Test movement later.

---

## 39. Sensor + Tello integration

Final flow:

```text
ESP8266 nodes
      |
      | UAV_GATEWAY
      v
ESP32 SoftAP
      |
      +--> collect measurements
      |
      +--> LittleFS

ESP32 STA
      |
      +--> during mission: Tello Wi-Fi
      |       |
      |       +--> UDP control 8889
      |       +--> telemetry 8890
      |
      +--> after landing: Internet Wi-Fi
              |
              +--> NTP
              +--> backend sync
```

Data safety:

```text
Sensor measurement
      |
      v
ESP8266 storage
      |
      v
ESP32 collection
      |
      v
ESP32 LittleFS
      |
      v
Tello lands
      |
      v
Internet Wi-Fi
      |
      v
.NET backend
      |
      v
mark record SYNCED
```

---

## 40. Important scope note

The older project documentation treated direct UAV flight control as outside the initial scope.

This task intentionally changes that assumption.

Keep all Tello code isolated so flight control can be documented as an extension and disabled without breaking the sensor gateway.

Do not mix Tello-specific logic into:

```text
StorageManager
NodeRegistry
SensorNodeClient
BackendAdapter
```

unless a dependency is genuinely required.

---

## 41. Implementation order

Implement sequentially:

### T1 — Network-role refactor
- StaTarget
- preserve SoftAP
- Tello/Internet switching
- build

### T2 — Tello UDP SDK
- `command`
- response
- `battery?`
- telemetry 8890
- no flight yet
- build

### T3 — Tello status/prepare API
- build

### T4 — Flight control
- takeoff
- land
- move
- rotate
- rc
- validation
- build

### T5 — Landing -> Internet transition
- landing confirmation
- automatic STA switch
- NTP gating
- build

### T6 — Post-land backend sync
- correct backend gating
- sync state
- build

### T7 — Full integration
- sensors + Tello + LittleFS + Internet + backend
- build
- update docs

After EACH phase:

1. run PlatformIO build
2. fix all compilation errors
3. inspect important warnings
4. preserve previous behavior
5. continue only after the phase builds

---

## 42. Required tests

Provide tests for:

- Tello SSID unavailable
- Tello connected
- `command` returns `ok`
- battery query
- telemetry receive
- invalid move values
- invalid RC values
- command timeout
- takeoff state validation
- landing state validation
- telemetry loss while flying
- no Internet switch on telemetry loss
- confirmed landing
- automatic Tello -> Internet switch
- Internet unavailable after landing
- backend unavailable
- backend recovery
- pending LittleFS data survives reboot
- ESP8266 collection continues in Tello mode
- next mission switches Internet -> Tello again

---

## 43. Final review

Before implementing, inspect the ENTIRE existing ESP32 branch.

The actual working source is authoritative.

Review at least:

```text
src/main.cpp
include/Config.h
include/Secrets.example.h

GatewayWiFiManager
ApiServer
NodeRegistry
CollectionManager
SensorNodeClient
StorageManager
TimeManager
BackendSyncManager
BackendAdapter

README.md
docs/OPERATIONS.md
platformio.ini
```

Adapt to current interfaces.

Do not rewrite working modules unnecessarily.

---

## 44. Final output

When finished, report:

1. architecture found before changes
2. files added
3. files modified
4. network state machine
5. flight state machine
6. Tello UDP design
7. Tello REST endpoints
8. config/secrets required
9. exact PlatformIO build command
10. exact upload command
11. exact Serial Monitor command
12. ground/no-takeoff test
13. first takeoff-land test
14. sensor collection + flight test
15. post-land backend sync test
16. known limitations
17. hardware steps still requiring manual validation

Do not claim live flight validation unless it was actually performed.

Do not add a second ESP32.
