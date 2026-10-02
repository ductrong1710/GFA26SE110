# ESP32 gateway: phases 5–8

## Implementation and safety

Phase 5 stores one JSON measurement per SHA-256 identity filename in `/pending`.
Identity is `deviceCode + sequence`; `recordId` must be `deviceCode-sequence`.
A staging file is flushed, closed, read back byte-for-byte, renamed, and checked
again before a record is eligible for ACK. Failed saves are never ACKed.
An atomic rename to `/synced` records backend confirmation. Directory location
is authoritative. Cleanup first commits a verified compact `/receipts` entry,
then removes the synced payload. Receipts preserve deduplication after cleanup
and reboot. Corrupt entries are retained and cannot authorize an ACK.

`MAX_LOCAL_RECORDS=1000` counts identities including receipts. This deliberately
imposes a lifetime capacity: cleanup frees payload space but does not reclaim an
identity slot. At capacity, new measurements stay on the sensor; no pending data
or deduplication history is silently discarded. Flash can fill before this limit.
Never run uploadfs on a filesystem containing data you need to retain.

Phase 6 uses SensorNodeClient for authenticated HTTP and CollectionManager for a
cooperative state machine: info → optional time push → fetch → validate/store →
ACK. Only online authenticated registry nodes are contacted, using their actual
registration connection IP. Batches contain at most 10 records; at most 3 batches
per node per cycle, with 10 seconds between cycles. Invalid records are skipped
individually. Lost ACKs cause retransmission and idempotent storage.

Phase 7 retains WIFI_AP_STA and the existing AP. STA retries every 15 seconds.
A TCP probe to 1.1.1.1:53 every 30 seconds supplies an actual reachability signal;
it is not proof that every Internet service is available. NTP starts asynchronously.
Valid time continues during a temporary outage, but does not survive power loss
as a reliable epoch without a fresh sync. Unsynced collection timestamps are
uptime seconds with collectedTimeSynced=false.

Phase 8 reads at most 2 measurements, maps their non-null values to at most 14
backend channel readings, and uses bounded retry intervals of 5–60 seconds.
Only explicit ACCEPTED/DUPLICATE outcomes for every channel of a measurement
permit marking it synced. HTTP 200 alone is insufficient. Failed or rejected
records remain pending. Pending scans rotate so an unexportable record cannot
permanently starve records behind it. A private backend is attempted when STA
is connected even if the public Internet probe is blocked.

## Configuration

Edit `include/Secrets.h` (ignored by Git; template: Secrets.example.h):

```cpp
INTERNET_SSID = "YOUR_HOTSPOT_SSID";
INTERNET_PASSWORD = "YOUR_HOTSPOT_PASSWORD";
BACKEND_BASE_URL = "http://192.168.1.100:5000/api";
BACKEND_API_KEY = "YOUR_PROVISIONED_GATEWAY_API_KEY";
```

These are existing constexpr char arrays: change their string values, retaining
the declarations. Empty Internet SSID disables STA attempts; empty backend URL/key
disables uploads. Keep the existing AP password and trusted-device secrets.
The prototype SENSOR-001 tests use `sensor-secret-001`; align any changed secret
in the mock environment and eventual sensor firmware.

Edit `include/Config.h`:

* GATEWAY_CODE: provisioned backend gateway code (default GATEWAY-001).
* BACKEND_GATEWAY_ID: provisioned positive numeric backend ID (default 0 disables).
* BACKEND_CHANNEL_CODES: exact backend channel codes, in temperature, humidity,
  soilMoisture, lightIntensity, ph, waterLevel, batteryVoltage order.
* SENSOR_NODE_HTTP_PORT: 80 by default; change if laptop port 80 is occupied.
* Other limits/timeouts are centralized here. Request/response maximum is 12 KiB;
  registration bodies remain limited to 1024 bytes.

Use a hotspot subnet different from the AP's 192.168.4.0/24. For laptop mocks,
the laptop can use Wi-Fi on UAV_GATEWAY and Ethernet on the backend LAN, or use
a second computer on the hotspot network for the backend. Backend base URL must
be reachable through the ESP32 STA interface. Do not use localhost in that URL.

## Build, upload, monitor

Run in PowerShell:

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t clean
.\.tools\platformio\Scripts\pio.exe run -e esp32dev
# Only when you are ready to interact with BOOT:
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t upload --upload-port COM3
.\.tools\platformio\Scripts\pio.exe device monitor --port COM3 --baud 115200
```

Close any existing Serial Monitor before uploading. If Connecting stalls, hold
BOOT, tap EN, and release BOOT after writing starts. Exit monitor with Ctrl+C.
Firmware upload preserves the LittleFS partition with the unchanged partition
layout. On a genuinely blank disposable board ONLY, provision LittleFS with:

```powershell
.\.tools\platformio\Scripts\pio.exe run -e esp32dev -t uploadfs --upload-port COM3
```

Mount failures never trigger automatic formatting.

Expected boot logs (counts vary):

```text
[GATEWAY] Boot
[GATEWAY] LittleFS mounted
[STORAGE] Stored identities: 0, pending: 0
[GATEWAY] Starting Wi-Fi
[GATEWAY] AP started
[GATEWAY] SSID: UAV_GATEWAY
[GATEWAY] AP IP: 192.168.4.1
[API] Starting HTTP server
[API] HTTP server started on port 80
```

With configured Internet and successful NTP, subsequent logs include STA
connected, NTP started asynchronously, and Time synchronized. Collection logs
include `[COLLECT] SENSOR-001 pending=25` and ACK counts. Sync logs include pending
count, uploading batch, backend response, and marked records. Secrets are never
printed. Ordering of asynchronous network events can vary.

## Hardware storage test (Phase 5)

Use a disposable test filesystem with no existing SENSOR-001 sequences 100/101.
This environment disables collection/backend sync so the results stay stable:

```powershell
.\.tools\platformio\Scripts\pio.exe run -e storage_diagnostics
.\.tools\platformio\Scripts\pio.exe run -e storage_diagnostics -t upload --upload-port COM3
.\.tools\platformio\Scripts\pio.exe device monitor --port COM3 --baud 115200
```

1. Send `s` followed by Enter: save100=0/stored=1, duplicate100=1/stored=1,
   save101=0/stored=2. Enum 0=Saved, 1=Duplicate, 2=Failed.
2. Send `v`: exists100=1, exists101=1, stored=2, pending=2.
3. Press EN, then `v`: same values prove reboot persistence.
4. Send `m`, then `v`: mark100=1, pending=1, stored=2.
5. Send `c`, reboot, then `v`: both identities still exist, pending=1.
6. Send `s` again: duplicates do not increase counts. The receipt prevents a
   cleaned-up synced identity from being stored again.
7. Upload the normal esp32dev environment before collection tests.

On an already used filesystem compare count deltas instead of expecting zero.
Diagnostic `m` simulates backend success and is never enabled in normal firmware.

## Mock sensor tests without an ESP8266 (Phase 6)

Connect the laptop to UAV_GATEWAY. Permit incoming TCP port 80 for the Python
process on this test network in Windows Firewall if prompted. Do not expose it
on an untrusted network. Keep backend configuration disabled for storage tests.

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
$env:MOCK_DEVICE_TOKEN = 'sensor-secret-001'
.\.tools\platformio\Scripts\python.exe tools\mock_sensor.py --gateway http://192.168.4.1 --records 25 --start 1000
```

The mock registers every 5 seconds and implements the actual HTTP contract; the
ESP32 runs its production CollectionManager. A second PowerShell window:

```powershell
Invoke-RestMethod http://192.168.4.1/api/gateway/status | ConvertTo-Json
Invoke-RestMethod http://192.168.4.1/api/gateway/nodes | ConvertTo-Json -Depth 8
Invoke-RestMethod http://192.168.4.1/api/gateway/time | ConvertTo-Json
```

Browser URLs are those same three URLs. Verify authenticated node data and a
25-record count increase, collected in batches 10, 10, 5. Stop each mock with
Ctrl+C before the next case. Keep the same token environment. Run each command
with the same Python executable above and a unique sequence range:

| Case | Arguments after tools\mock_sensor.py | Expected |
|---|---|---|
| Zero | --records 0 --start 2000 | No new records/ACK |
| One | --records 1 --start 2100 | One new identity |
| Duplicate | --records 2 --start 2200 --duplicate | Two unique identities |
| Malformed JSON | --records 2 --start 2300 --malformed-once | First fetch rejected; later retry succeeds |
| Mixed invalid | --records 3 --start 2400 --invalid-first | Valid records saved; invalid remains unACKed |
| Disconnect | --records 25 --start 2500 --disconnect-after 1 | First batch retained; later failure leaves source data |
| Lost ACK | --records 2 --start 2600 --ack-fail-once | Retransmitted identities do not increase stored count |
| Unsynced | --records 2 --start 2700 --unsynced | Saved/ACKed but retained pending for backend |

Default gateway URL is http://192.168.4.1. For a storage-full test on a disposable
test filesystem, set MAX_LOCAL_RECORDS=1, rebuild/upload, then run 2 fresh records.
Only the first can be ACKed; the other stays at the mock and error is logged.
Restore MAX_LOCAL_RECORDS=1000 and rebuild afterwards. Do not erase real pending
data to prepare tests. A mount failure also leaves the API running with
storageReady=false and collection disabled.

## STA/time hardware sequence (Phase 7)

1. Configure hotspot credentials, upload normal firmware, leave hotspot off.
2. Confirm AP and all local APIs still work; run a fresh mock batch and verify
   local storage increases. timeSynced=false until valid time is acquired.
3. Enable hotspot. Within the retry interval STA should connect; allow time for
   NTP. Poll status/time until staConnected=true and timeSynced=true.
4. Confirm AP clients can still call APIs and collection continues.
5. Disable hotspot: staConnected/internetConnected become false; AP stays enabled.
   Continue a new mock batch. Pending data remains intact.
6. Re-enable hotspot: verify reconnection and Internet probe status recovery.

AP and STA share one radio. STA channel changes can require an AP client to
reconnect briefly; firmware never disables the AP to reconnect STA. A blocked
TCP probe may report no Internet despite some services working.

## Backend contract and mock tests (Phase 8)

The implementation follows the existing repository SyncController/SyncContracts,
not the illustrative /sensor-readings/batch endpoint:

```text
POST {BACKEND_BASE_URL}/device/gateways/{BACKEND_GATEWAY_ID}/sync
X-Gateway-Code: GATEWAY-001
X-Api-Key: configured gateway key
Content-Type: application/json
```

```json
{"batchKey":"esp32-<SHA256-of-payload>","missionId":null,"records":[{"sensorNodeCode":"SENSOR-001","channelCode":"temperature","sourceRecordKey":"SENSOR-001-100","value":30.2,"measuredAt":"2026-09-30T01:00:00Z","collectedAt":"2026-09-30T01:01:00Z","qualityStatus":"VALID"}],"collectionResults":[]}
```

`sourceRecordKey` carries the deviceCode/sequence identity. Each non-null reading
is a separate channel row. Null readings are omitted. Stable batch hashes and
stable per-channel source keys permit safe retry even when batch membership changes.
Expected response envelope:

```json
{"success":true,"data":{"batchKey":"esp32-<same hash>","accepted":1,"duplicates":0,"rejected":0,"records":[{"index":0,"sourceRecordKey":"SENSOR-001-100","status":"ACCEPTED","error":null}],"collectionResults":[]},"message":null}
```

DUPLICATE also confirms a row. REJECTED, missing rows, mismatched identities,
invalid JSON, timeout and non-2xx do not authorize deletion. Backend gateway,
nodes and channels must already be provisioned. No direct PostgreSQL connection.

Mock backend, on the STA-reachable laptop/computer:

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
$env:MOCK_BACKEND_KEY = 'local-test-key'
.\.tools\platformio\Scripts\python.exe tools\mock_backend.py --port 5000 --gateway-id 1 --gateway-code GATEWAY-001
```

Set firmware BACKEND_GATEWAY_ID=1, BACKEND_BASE_URL to that computer's LAN IPv4
plus :5000/api, BACKEND_API_KEY=local-test-key. Permit TCP5000 on the test network.
The mock persists deduplication state in .tools/mock-backend-state.json.

Execute A–H with fresh sequence ranges:

A. First obtain valid gateway NTP, then disable hotspot without rebooting. Run
   `mock_sensor.py --records 10 --start 5000`; observe +10 pending.
B. Restart ESP32 with hotspot still off: those 10 remain pending with their
   original valid collected timestamps.
C. Enable hotspot but keep backend stopped: pending remains, retry logs appear.
D. Start mock backend: records upload in batches of up to 2 measurements.
E. Confirm pending drops as all channel outcomes succeed; stored identity count
   stays constant, even after minute-based cleanup.
F. Stop backend, start with `--drop-once`, collect a fresh batch starting6000.
   Connection closes after the backend commits; ESP32 must retain pending until
   a later confirmed retry.
G. Observe retry response ACCEPTED from cached batch or DUPLICATE by source key,
   eventual pending decrease, and no extra gateway identities.
H. Stop backend again, collect starting7000: local collection/ACK continues.

Additional failures: backend `--fail-first 2` returns two 500 responses;
`--reject-channel temperature` rejects that channel so its measurements remain
pending. Stop/restart with the same --state path to exercise persisted backend
deduplication. Cached rejected batch outcomes can remain rejected; a real backend
operator must resolve/reprocess such failures rather than the gateway inventing
acceptance.

If records were collected before any valid gateway/sensor time, the existing
backend contract cannot represent them faithfully: they remain pending. Later
NTP does not rewrite old timestamps. All-null measurements likewise remain
pending. A backend contract extension or externally verified time reconstruction
is required to export these records; guessing wall-clock dates is unsafe.

Host-only mock protocol regression tests (no board needed):

```powershell
.\.tools\platformio\Scripts\python.exe -m unittest discover -s tests -p test_mock_protocols.py -v
```

These exercise mock HTTP contracts, not ESP32 firmware or flash power-loss safety.

## Integration when real ESP8266 firmware exists

1. Provision a unique trusted code/secret; keep a persistent monotonically
   increasing sequence at the sensor. Never reuse it after reboot/reset.
2. Connect sensor to UAV_GATEWAY and register using Phase 4 matching JSON/header
   deviceCode and X-Device-Token. Re-register after IP change or gateway restart.
3. Serve info, data?limit=10, ack, health and time routes matching mock_sensor.py.
   Verify X-Device-Code and X-Device-Token on gateway requests. Return bounded
   JSON with Content-Length or HTTP/1.0 connection-close framing; no chunking.
4. Keep measurements in sensor persistent storage until their exact recordIds
   appear in a successful ACK request. ACK handling must itself be idempotent.
5. Use Unix seconds only when timeSynced=true; accept POST time without making
   data collection depend on its success. Return identity in info/data/health.
6. Repeat reboot, disconnect, lost-ACK, full-storage and backend-outage tests
   before connecting a provisioned real .NET backend.

## Limitations and remaining validation

This is a shared-secret HTTP prototype, without TLS, HMAC, encrypted flash or
production credential provisioning. Outbound transport currently accepts http://
only. RSSI=0 means unknown; it is not a measured signal strength. Registry remains
in RAM; measurements/receipts persist. Large JSON and growing RAM queues are
avoided, but HTTPClient operations are synchronous and bounded by short timeouts;
this is not hard real-time firmware. DNS/upstream HTTP header handling can add
latency. JSON docs reserve heap beyond static linker RAM usage.

Real ESP8266 behavior, real backend provisioning/network/authentication, AP+STA
radio continuity, flash power interruption and prolonged full-storage behavior
still require hardware integration testing. No hardware validation or upload was
performed for phases 5–8 during this implementation. Backend endpoint already
exists in the repository; no new endpoint or ESP8266 firmware was created.
