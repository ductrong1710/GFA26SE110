# One ESP32: Tello missions and post-land synchronization

## What changed

The reviewed branch was ESP32 at ef9f6a6. It already had a cooperative collection
state machine, authenticated RAM registry, verified LittleFS writes with 22-byte
Base64URL identity filenames, backend channel adapter, WebServer body limit and
optional Internet STA. StorageManager, measurement codec, NodeRegistry,
SensorNodeClient, CollectionManager and BackendAdapter are preserved.

GatewayWiFiManager now owns one explicit STA target: NONE, TELLO or INTERNET.
Boot defaults to Internet for the existing gateway behavior, but does not infer
aircraft flight state. Preparing is an explicit request. A network transition
disconnects STA and waits for an observable disconnect before connecting again.
UAV_GATEWAY at 192.168.4.1 remains enabled. AP/STA share a radio, so AP clients may
need to reconnect when the STA channel changes; registry entries are retained.

TelloController binds command UDP8889 and state UDP8890 to the current STA IP.
It accepts command replies only from configured Tello IP/command port; telemetry
must come from that IP. SDK readiness requires `command` → `ok`. One normal
command may be pending, with bounded packet work and millis-based deadlines.
Only SDK/battery reads retry automatically, once. Flight commands never replay
automatically. Battery unknown is -1, not an invented percentage.

FlightStateManager follows:

```text
IDLE_INTERNET → CONNECTING_TELLO → ENTERING_SDK → QUERYING_BATTERY
 → LANDED_READY → [explicit takeoff] → TAKING_OFF → FLYING
 → [explicit land] → LANDING → LANDED_CONFIRMED
 → SWITCHING_TO_INTERNET → INTERNET_CONNECTING → SYNCING → SYNC_COMPLETE
```

ERROR retains Tello target when flight is uncertain. SYNC_BLOCKED means backend
configuration or storage is unavailable. SYNCING includes waiting for backend
retry; it does not imply a successful upload. New measurements can return
SYNC_COMPLETE to SYNCING. `prepare` begins the next mission without takeoff.

Takeoff additionally requires known battery >=25%, SDK readiness, no pending
command and fresh low-height/near-zero-vertical-speed telemetry. LANDED_READY
means preparation finished; it alone is not physical proof of landing.

Landing requires an actual `land` ACK, then fresh low/stable height (h, or tof
when h is unavailable) and near-zero vertical velocity for >=10 distinct observed
samples spanning >=5 seconds. Repeated polls of the same packet do not count.
Height threshold is 15 cm, per-sample variation <=3 cm, vertical speed <=5 SDK
units. Missing fields never borrow old values for landing confirmation.

If no fresh telemetry is available, a clean land ACK may confirm after 20 seconds,
with explicit `LAND_ACK_AND_CONSERVATIVE_TIMEOUT` logging/status. That fallback is
disabled after command ambiguity/retries, RC traffic, flight error/reconnection,
or contradictory post-ACK telemetry. Telemetry disappearing while flying never
confirms landing. Insufficient landing evidence after 60 seconds leaves ERROR
and retains Tello target. Link recovery restarts SDK only, with bounded retry;
it never automatically replays takeoff/movement. Request land explicitly once
sdkReady returns true.

Only after confirmation are UDP sockets stopped and STA switched to Internet.
Backend readiness requires INTERNET target, matching configured SSID, connected
STA and nonzero IP. Public Internet probe need not succeed for a LAN backend.
NTP stops before leaving Internet and restarts on Internet readiness; previously
synchronized system time continues offline. BackendAdapter endpoint, headers,
record identity, ACK ordering and retry/backoff remain unchanged.

## Files

Added: include/TelloController.h, src/TelloController.cpp,
include/TelloTelemetry.h, src/TelloTelemetry.cpp,
include/FlightStateManager.h, src/FlightStateManager.cpp;
tests/run_tello_host.ps1, tests/tello_http.py, tests/host/*;
docs/TELLO_IMPLEMENTATION.md and this runbook.

Modified: include/Config.h, include/Secrets.example.h, ignored include/Secrets.h,
include/GatewayWiFiManager.h, src/GatewayWiFiManager.cpp,
include/ApiServer.h, src/ApiServer.cpp,
include/BackendSyncManager.h, src/BackendSyncManager.cpp,
src/TimeManager.cpp, src/main.cpp, README.md, docs/OPERATIONS.md.

## Configuration and commands

In ignored include/Secrets.h, fill the existing array values:

```cpp
constexpr char TELLO_SSID[] = "TELLO-YOUR-AIRCRAFT";
constexpr char TELLO_PASSWORD[] = ""; // Open network, or actual configured password.
```

Keep INTERNET_SSID/INTERNET_PASSWORD, BACKEND_BASE_URL/BACKEND_API_KEY and sensor
secrets configured as described in OPERATIONS.md. Set Config::BACKEND_GATEWAY_ID
to the real provisioned positive ID and BACKEND_CHANNEL_CODES to backend channels.
Use distinct AP, Tello and hotspot subnets. No real passwords were changed.

Config.h contains all Tello timing/limits. Defaults: IP192.168.10.1, ports8889/8890,
SDK/read timeout7s, flight-command timeout20s, connect timeout30s, recovery retry15s,
telemetry freshness2s, RC send interval100ms and RC lifetime500ms. Setting
TELLO_ENABLED=false disables preparation while preserving gateway functionality.
No video, flips, mission pads, swarm or flight-controller integration is included.

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t clean
.\.tools\platformio\Scripts\pio.exe run -e esp32dev
# Run manually when ready to operate BOOT, with Serial Monitor closed:
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t upload --upload-port COM3
.\.tools\platformio\Scripts\pio.exe device monitor --port COM3 --baud 115200
```

If needed, hold BOOT while Connecting, tap EN, release BOOT once writing starts.
Do not run uploadfs over existing measurements. No upload was performed here.

## REST contract

All paths use http://192.168.4.1, application/json. Accepted actions return HTTP202
and current state; this means accepted, not completed. State conflicts return409,
invalid move/rotate/RC input400, oversized bodies413. Poll status for the result.

| Method/path | Body / behavior |
|---|---|
| GET /api/tello/status | Target, SDK/Wi-Fi readiness, flight state, battery, telemetry freshness, attitude, pending command, response/error, landing evidence and pending uploads |
| POST /api/tello/prepare | No body needed; connect Tello, SDK and battery, never takeoff |
| POST /api/tello/takeoff | No body; explicit guarded takeoff |
| POST /api/tello/land | No body; serialize behind an existing command, wait for ACK and evidence |
| POST /api/tello/move | `{"direction":"forward","distanceCm":50}`; up/down/left/right/forward/back, integer20..500 |
| POST /api/tello/rotate | `{"direction":"cw","degrees":90}`; cw/ccw, integer1..3600 |
| POST /api/tello/rc | `{"leftRight":0,"forwardBack":25,"upDown":0,"yaw":0}`; integers -100..100 |
| POST /api/tello/disconnect | Ground-only transition: LANDED_READY, no flight commanded, fresh stable ground telemetry for5s |

RC has one replaceable slot, no queue. Values older than500ms are not sent later;
expired active input triggers neutral. Neutral may be requested explicitly with
all four channels zero. RC and normal commands are separated by a quiet interval.
Do not use movement on the first flight. Ranges follow the official SDK1.3:
https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf

Existing /api/gateway/status, /nodes, /time and authenticated registration remain.
Gateway status now adds staTarget, backendNetworkReady and flightState.

## Exact ground test: no takeoff

Power the aircraft on a stable surface and keep it physically landed throughout.
Configure and manually upload firmware. Connect phone/laptop to UAV_GATEWAY.
Ensure hotspot/backend are reachable initially, and acquire NTP before collecting
records intended for the current backend. In PowerShell define:

```powershell
$gateway = 'http://192.168.4.1'
function TelloStatus { Invoke-RestMethod "$gateway/api/tello/status" -TimeoutSec 3 }
function Wait-TelloState([string]$Wanted, [int]$Seconds=60) {
    $until = (Get-Date).AddSeconds($Seconds)
    do {
        $s = $null
        try { $s = TelloStatus } catch { }
        if ($s -and $s.flightState -eq $Wanted) { return $s }
        if ($s -and $s.flightState -eq 'ERROR') { throw ($s | ConvertTo-Json -Compress) }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $until)
    throw "Timed out waiting for $Wanted; inspect status and aircraft. Do not assume landing."
}
Invoke-RestMethod "$gateway/api/gateway/status"
Invoke-RestMethod "$gateway/api/gateway/time"
Invoke-RestMethod -Method Post "$gateway/api/tello/prepare"
$s = Wait-TelloState 'LANDED_READY'
$s | ConvertTo-Json
```

Verify target TELLO, telloWifiConnected/sdkReady true, battery known and fresh
telemetry. Serial must show command/ok then battery?/numeric response. Ensure
gateway backendNetworkReady and internetConnected are false. Start a sensor/mock
batch while still on Tello; verify node ACK and local pending count increases,
with no `[SYNC] Uploading` while target is TELLO. Existing gateway APIs must work.

```powershell
Start-Sleep -Seconds 6
TelloStatus
# Proceed only while aircraft is physically on the surface with stable telemetry.
Invoke-RestMethod -Method Post "$gateway/api/tello/disconnect"
Wait-TelloState 'SYNC_COMPLETE' 120
Invoke-RestMethod "$gateway/api/gateway/status"
Invoke-RestMethod "$gateway/api/gateway/time"
```

If disconnect returns409, do not force a switch: inspect telemetry and wait for
stable ground samples. SYNC_BLOCKED means configuration/storage needs attention;
SYNCING with pending records means inspect backend response logs/timestamps.
Only after this ground flow works perform the flight test.

## Exact first flight: takeoff → hover → land → Internet → backend

Use a clear test area, charged battery and an operator observing the aircraft.
Reuse the helper functions above. Start with real backend/mock backend available
and a sensor supplying fresh records. Do not test movement on this first flight.

```powershell
Invoke-RestMethod -Method Post "$gateway/api/tello/prepare"
$s = Wait-TelloState 'LANDED_READY'
if (-not $s.sdkReady -or -not $s.telemetryFresh -or $s.battery -lt 25) {
    throw 'Ground/readiness checks failed; do not take off.'
}
Start-Sleep -Milliseconds 500
Invoke-RestMethod -Method Post "$gateway/api/tello/takeoff"
Wait-TelloState 'FLYING' 30
Start-Sleep -Seconds 5
Invoke-RestMethod -Method Post "$gateway/api/tello/land"
# Keep observing physically. LANDING is not permission to power off/disconnect.
Wait-TelloState 'SYNC_COMPLETE' 120
TelloStatus | ConvertTo-Json
Invoke-RestMethod "$gateway/api/gateway/status" | ConvertTo-Json
```

Expected: land sent, response ok, landing evidence recorded, UDP stopped, target
INTERNET, hotspot connected, NTP restarted if applicable, existing sync logs and
pendingUploadRecords decreases to0. landingEvidence remains visible after the
switch. Backend failure must leave pending data, not claim completion.
If any wait fails, inspect aircraft/status; never infer landing from the failure.
If target remains TELLO in ERROR and sdkReady recovers, explicitly request land
again. Do not reboot a gateway controlling a potentially airborne aircraft.

After landing, test the next mission with prepare and verify it stays LANDED_READY
without takeoff. After later ground verification, movement examples are:

```powershell
# These are real movement commands: use only in a later deliberate flight test.
Invoke-RestMethod -Method Post "$gateway/api/tello/move" -ContentType application/json -Body '{"direction":"forward","distanceCm":50}'
# Wait until commandPending=false before the next normal command.
Invoke-RestMethod -Method Post "$gateway/api/tello/rotate" -ContentType application/json -Body '{"direction":"cw","degrees":90}'
Invoke-RestMethod -Method Post "$gateway/api/tello/rc" -ContentType application/json -Body '{"leftRight":0,"forwardBack":0,"upDown":0,"yaw":0}'
```

## Sensor integration and fault tests

Use the existing mock_sensor.py on a laptop connected to UAV_GATEWAY, or real
ESP8266 firmware implementing the existing contract. Example in a second window:

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
$env:MOCK_DEVICE_TOKEN = 'sensor-secret-001' # Match your configured device secret.
.\.tools\platformio\Scripts\python.exe tools\mock_sensor.py --records 25 --start 10000
```

Use unused sequences per test, ensure TCP80 is allowed on the local test network.
While prepared or flying, collection must continue in batches10/10/5, with verified
LittleFS save before ACK. Backend pending grows while target is TELLO. Re-register
nodes after a channel transition as needed; offline entries are not removed.
After confirmed landing, backend should drain the queue. Repeat with backend
stopped, then restart it: pending must survive and retry must recover.

| Required fault | Test/evidence |
|---|---|
| Tello SSID unavailable | Power Tello off on ground, prepare, expect ERROR/TELLO_CONNECT_TIMEOUT after30s; AP APIs still usable. Power on, prepare again. |
| SDK timeout/rejection | Host tests inject missing replies; no SDK-ready/flight-ready transition. |
| Connected/SDK/battery/telemetry | Ground test above plus native protocol tests. |
| Invalid move/RC | POST distanceCm19 or RC101: HTTP400, no command sent. Native tests also reject injection direction strings. |
| Takeoff state/battery | Host tests and a ground request before prepare:409; never automate a valid takeoff as a negative test. |
| Land state | Before flight, land returns409; ground disconnect requires stable evidence. |
| Telemetry loss flying | Native tests: stays TELLO; loss never means landed. Do not deliberately jam a real aircraft. |
| Lost land ACK | Native tests: timeout, ERROR, no automatic flight-command resend, no Internet switch. |
| Contradictory telemetry then loss | Native tests keep TELLO despite land ACK. |
| Confirmed landing/next mission | Native tests and first-flight sequence above. |
| SDK recovery failure | Native test drops both recovery replies and restores UDP; bounded SDK retry rearms while preserving uncertainty. |
| Internet unavailable after land | Turn hotspot off while aircraft is already on ground; InternetConnecting, AP alive, pending retained; restore hotspot. |
| Backend unavailable/recovery | Stop backend on ground, collect, verify pending; restart and inspect per-record sync confirmation. |
| Reboot persistence | On ground only, record pending count with backend stopped, reboot, check same count. Follow Phase5 diagnostics in OPERATIONS.md for deeper flash checks. |

Run host suites (no aircraft/network connection):

```powershell
powershell -ExecutionPolicy Bypass -File tests\run_tello_host.ps1
.\.tools\platformio\Scripts\python.exe -m unittest discover -s tests -p test_mock_protocols.py -v
```

The native suite compiles production controller/parser/flight/Wi-Fi/time classes
with fake UDP, Wi-Fi and time, using installed PlatformIO MinGW. It exercises
state behavior and millis rollover; it does not verify ESP32 radio timing or
physical landing. Existing Python tests cover sensor/backend mock contracts.

After manual upload, API-only smoke checks send GETs and invalid control inputs
(no prepare/takeoff/land or valid movement):

```powershell
.\.tools\platformio\Scripts\python.exe tests\tello_http.py http://192.168.4.1
```

## Limits and validation still required

No hardware upload or live flight validation was performed for this extension.
Ground/flight tests above remain manual requirements. UDP SDK replies have no
transaction IDs: delayed or duplicated `ok` cannot be attributed with certainty.
Serialization, quiet intervals and uncertainty guards reduce that risk; telemetry
and operator observation remain necessary. UDP telemetry has no authentication.
HTTP controls are local prototype endpoints, without operator authentication/TLS;
only trusted users/devices should share these networks. No video is enabled.

Tello's SDK1.3 documentation specifies automatic landing after15 seconds without
commands. Firmware does not override that watchdog; keep the first hover to5s
and send explicit land. Automatic aircraft landing is not gateway confirmation.
RC expiry is processed when the cooperative loop runs. Existing sensor HTTPClient
operations and HTTP server handling can delay loop service; this is not a
hard-real-time controller. Measure latency with real sensor traffic before using
continuous RC. No new blocking UDP/landing/Wi-Fi/NTP wait loops were added.

Records collected without synchronized timestamps remain pending under the current
backend contract. Acquire NTP before a mission and sync sensor time before new
measurements; never rewrite historical timestamps by guessing. Existing storage
capacity/dedup receipt limits still apply. A reboot preserves LittleFS but loses
flight-state knowledge; preparation is required again and takeoff requires fresh
ground telemetry. Do not reboot during flight as a recovery method.
