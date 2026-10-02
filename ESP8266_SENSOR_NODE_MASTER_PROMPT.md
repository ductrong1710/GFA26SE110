# ESP8266 Sensor Node — Master Coding Prompt

You are a senior Embedded/IoT engineer.

I am building a university capstone project named:

**UAV-Assisted IoT Platform for Farm Monitoring and Sensor Data Collection**

This document is the **single source of truth for the ESP8266 Sensor Node firmware**.

Do NOT implement ESP32 gateway firmware here.
Do NOT implement UAV flight control.
Do NOT connect the ESP8266 directly to PostgreSQL.

The ESP8266 communicates with the ESP32 UAV Gateway over local Wi-Fi.

---

# 1. PROJECT ROLE

The ESP8266 is installed in a farm zone and acts as a **Sensor Node**.

Its responsibilities are:

- Periodically read environmental sensors.
- Create one measurement record per sampling cycle.
- Persist measurements locally in LittleFS.
- Continue recording when the UAV is absent.
- Detect and connect to the ESP32 gateway Wi-Fi when the UAV is nearby.
- Register itself with the gateway.
- Authenticate using a configured device identity and secret.
- Expose stored measurements to the gateway through HTTP.
- Keep measurements until the gateway explicitly ACKs them.
- Support retransmission after communication failure.
- Accept time synchronization from the gateway.
- Expose health/status information.
- Recover pending measurements after reboot.
- Support DEMO_MODE when physical sensors are unavailable.

The sensor node should be able to operate for long periods without Internet access.

---

# 2. SYSTEM ARCHITECTURE

```text
Physical Sensors
      |
      v
ESP8266 Sensor Node
      |
      | local persistent storage
      v
LittleFS Pending Measurements
      |
      | UAV approaches
      v
ESP32 UAV Gateway
      |
      | Internet when available
      v
.NET Backend API
      |
      v
PostgreSQL
```

The ESP8266 communicates only with the ESP32 gateway in the initial prototype.

---

# 3. CRITICAL DATA-SAFETY RULE

```text
Read sensors
    |
    v
Create measurement
    |
    v
Save to ESP8266 LittleFS
    |
    v
Wait for UAV Gateway
    |
    v
Gateway requests record
    |
    v
ESP8266 sends record
    |
    v
ESP32 stores record successfully
    |
    v
ESP32 sends ACK
    |
    v
ESP8266 marks that record collected
```

Never remove or mark a record collected just because it was sent.
Only change its local state after a valid gateway ACK.

This allows safe retransmission after Wi-Fi disconnect, UAV moving out of range, lost HTTP response, gateway reboot, or ACK loss.

---

# 4. DEVELOPMENT ENVIRONMENT

Use:

- ESP8266
- PlatformIO
- Arduino Framework
- C++
- LittleFS
- ArduinoJson
- ESP8266WiFi
- ESP8266WebServer
- ESP8266HTTPClient

Target folder:

```text
esp8266-sensor-node/
```

The project must compile using PlatformIO.
Do not put all logic inside `main.cpp`.

---

# 5. PROJECT STRUCTURE

```text
esp8266-sensor-node/
├── platformio.ini
├── .gitignore
├── README.md
├── data/
├── include/
│   ├── Config.h
│   ├── Secrets.h
│   ├── Secrets.example.h
│   ├── Models.h
│   ├── ErrorCodes.h
│   └── Logger.h
└── src/
    ├── main.cpp
    ├── SensorManager.h
    ├── SensorManager.cpp
    ├── StorageManager.h
    ├── StorageManager.cpp
    ├── MeasurementQueue.h
    ├── MeasurementQueue.cpp
    ├── WiFiManager.h
    ├── WiFiManager.cpp
    ├── GatewayClient.h
    ├── GatewayClient.cpp
    ├── ApiServer.h
    ├── ApiServer.cpp
    ├── TimeManager.h
    └── TimeManager.cpp
```

You may improve this layout if necessary, but keep it simple and suitable for a student capstone.

---

# 6. DEVICE IDENTITY

Each ESP8266 sensor node has:

```text
nodeId
deviceCode
farmId
zoneId
deviceSecret
firmwareVersion
deviceType
```

Example:

```text
nodeId = 1
deviceCode = SENSOR-001
farmId = 1
zoneId = 1
deviceSecret = sensor-secret-001
firmwareVersion = 1.0.0
deviceType = ESP8266_SENSOR
```

Rules:

- `deviceCode` is the logical unique identity.
- Do NOT use IP address as permanent identity.
- IP address may change.
- Do NOT print secrets in logs.
- Keep secrets in `include/Secrets.h`.
- Provide `include/Secrets.example.h`.
- Add `Secrets.h` to `.gitignore`.

---

# 7. SENSOR TYPES

The architecture must support:

- soil moisture
- air temperature
- air humidity
- light intensity
- pH
- water level

For the first physical implementation:

- DHT22 for temperature/humidity.
- Analog soil-moisture sensor.

Other sensor types can remain as clean placeholder interfaces until exact hardware is selected.
Not every node needs every sensor.
Unavailable values must be represented as null/NAN appropriately.

---

# 8. SENSOR MANAGER

Create `SensorManager`.

Responsibilities:

- Initialize physical sensors.
- Read installed sensors.
- Detect read failure.
- Return a complete `SensorReading`.
- Support DEMO_MODE.
- Keep sensor-specific logic isolated from networking/storage.

Suggested API:

```cpp
bool begin();
SensorReading readSensors();
```

Do not tightly couple the complete firmware to DHT22.

---

# 9. DEMO MODE

Create a compile-time option such as:

```cpp
#define DEMO_MODE true
```

When enabled:

- Do not require physical sensors.
- Generate realistic simulated values.
- Use the same storage, API, ACK, Wi-Fi, and registration flow as production mode.

Suggested ranges:

```text
temperature: 25-35 °C
humidity: 50-90 %
soilMoisture: 20-80 %
```

Clearly log when DEMO_MODE is enabled.

---

# 10. MEASUREMENT MODEL

Create a measurement model with at least:

```text
recordId
deviceCode
sequence
measuredAt
timeSynced
uptimeMs
temperature
humidity
soilMoisture
lightIntensity
ph
waterLevel
batteryVoltage
state
```

Example:

```json
{
  "recordId": "SENSOR-001-12345",
  "deviceCode": "SENSOR-001",
  "sequence": 12345,
  "measuredAt": 1720000000,
  "timeSynced": true,
  "uptimeMs": 12345678,
  "temperature": 30.2,
  "humidity": 75.1,
  "soilMoisture": 48.5,
  "lightIntensity": null,
  "ph": null,
  "waterLevel": null,
  "batteryVoltage": 4.02,
  "state": "PENDING"
}
```

Logical unique key:

```text
deviceCode + sequence
```

`recordId` should be derived from the same identity, for example `SENSOR-001-12345`.

---

# 11. SEQUENCE NUMBER

`sequence` must monotonically increase and survive reboot.

Requirements:

- Never intentionally reuse an old sequence number.
- Persist the last sequence value safely.
- Avoid excessive flash rewrites.
- Ensure records created after reboot cannot collide with old pending records.

Explain the chosen flash-wear tradeoff.

---

# 12. TIME MANAGEMENT

The sensor node cannot assume Internet access.

Create `TimeManager`.

If valid synchronized Unix time exists:

```text
timeSynced = true
measuredAt = Unix timestamp
```

If real time is unavailable:

```text
timeSynced = false
measuredAt = 0
uptimeMs = millis()
```

Ordering is still guaranteed by `sequence`.

The ESP32 gateway may provide current time through:

```http
POST /api/node/time
```

Request:

```json
{
  "unixTime": 1720000100
}
```

Maintain a time reference based on received Unix timestamp + elapsed `millis()`.
Do not require direct NTP on the ESP8266 in the first prototype.

---

# 13. SENSOR SAMPLING

Use configurable `SENSOR_READ_INTERVAL_MS`, for example 60000 ms.

Use `millis()` scheduling.
Do NOT use `delay(60000)`.
The HTTP server and Wi-Fi logic must remain responsive between measurements.

---

# 14. LOCAL STORAGE

Use LittleFS.

Create:

```text
StorageManager
MeasurementQueue
```

Requirements:

- Survive reboot.
- Append new records.
- Return oldest pending records first.
- Fetch records in bounded batches.
- ACK specific records.
- Do not remove unrelated records.
- Support duplicate ACK safely.
- Limit maximum storage.
- Handle storage errors without crashing.
- Avoid loading all measurements into RAM.
- Avoid large flash rewrites where practical.

For a capstone prototype, NDJSON / JSON Lines is acceptable.

Example:

```text
/data/measurements.ndjson
```

One record per line.
A separate metadata/state file is allowed if needed.
Do NOT use SQLite.

---

# 15. MEASUREMENT STATES

At minimum:

```text
PENDING
ACKED
```

`PENDING`: stored locally and not yet confirmed by ESP32.

`ACKED`: ESP32 confirmed safe receipt.

The implementation may compact/remove old ACKED records during maintenance.
Do not rewrite the entire storage file after every ACK unless necessary.

---

# 16. STORAGE FULL POLICY

Use configurable `MAX_LOCAL_RECORDS`.

Preferred behavior:

1. Compact/remove old ACKED records first.
2. Never silently remove PENDING records.
3. If PENDING storage is full, log an error, preserve existing unsent data, and expose the failure in health/status.

Do not silently lose unsent measurements.

---

# 17. WI-FI GATEWAY CONNECTION

The ESP8266 connects to the ESP32 gateway AP:

```text
SSID: UAV_GATEWAY
```

The password comes from `Secrets.h`.

Requirements:

- Non-blocking reconnect.
- Sensor acquisition continues when gateway is absent.
- Pending measurements continue accumulating locally.
- Do not reboot because Wi-Fi is unavailable.
- Reconnect automatically when UAV returns.

Use `millis()`; do not use an infinite loop waiting for Wi-Fi.

---

# 18. GATEWAY REGISTRATION

After connecting to the gateway AP, register with:

```http
POST http://192.168.4.1/api/gateway/nodes/register
```

The gateway base address must be configurable.

Headers:

```http
X-Device-Code: SENSOR-001
X-Device-Token: <deviceSecret>
Content-Type: application/json
```

Body:

```json
{
  "deviceCode": "SENSOR-001",
  "farmId": 1,
  "zoneId": 1,
  "firmwareVersion": "1.0.0",
  "deviceType": "ESP8266_SENSOR",
  "ipAddress": "192.168.4.2"
}
```

Registration should happen after connection, after reconnect, and optionally at a reasonable heartbeat interval.
Do not continuously spam registration requests.

---

# 19. GATEWAY CLIENT

Create `GatewayClient`.

Responsibilities:

- Registration.
- HTTP request setup.
- Authentication headers.
- Retry timing.
- Gateway base URL configuration.
- HTTP timeout.
- Connection cleanup.

Do not scatter `ESP8266HTTPClient` code across unrelated classes.

---

# 20. ESP8266 LOCAL HTTP API

Use `ESP8266WebServer`.

Required endpoints:

```text
GET  /api/node/info
GET  /api/node/data?limit=N
POST /api/node/ack
GET  /api/node/health
POST /api/node/time
```

---

# 21. GET /api/node/info

Return:

```json
{
  "success": true,
  "deviceCode": "SENSOR-001",
  "farmId": 1,
  "zoneId": 1,
  "firmwareVersion": "1.0.0",
  "deviceType": "ESP8266_SENSOR",
  "pendingRecords": 15
}
```

`pendingRecords` must reflect actual local storage state.

---

# 22. GET /api/node/data

Request:

```http
GET /api/node/data?limit=20
```

Response:

```json
{
  "success": true,
  "deviceCode": "SENSOR-001",
  "count": 2,
  "records": [
    {
      "recordId": "SENSOR-001-100",
      "deviceCode": "SENSOR-001",
      "sequence": 100,
      "measuredAt": 1720000000,
      "timeSynced": true,
      "uptimeMs": 100000,
      "temperature": 30.2,
      "humidity": 75.1,
      "soilMoisture": 48.5,
      "lightIntensity": null,
      "ph": null,
      "waterLevel": null,
      "batteryVoltage": 4.02
    }
  ]
}
```

Rules:

- Return oldest PENDING records first.
- Clamp `limit`.
- Use configured maximum batch size.
- Do not mark records ACKED here.
- Do not delete records here.

---

# 23. POST /api/node/ack

Request:

```json
{
  "recordIds": [
    "SENSOR-001-100",
    "SENSOR-001-101"
  ]
}
```

Behavior:

- Validate JSON.
- Mark only matching records ACKED.
- Duplicate ACK must not corrupt storage.
- Unknown IDs must not crash firmware.

Success example:

```json
{
  "success": true,
  "ackedCount": 2
}
```

Use exact record IDs, not sequence ranges.

---

# 24. GET /api/node/health

Return real device status.

Example:

```json
{
  "success": true,
  "status": "OK",
  "uptimeMs": 123456,
  "freeHeap": 30000,
  "gatewayConnected": true,
  "wifiRSSI": -55,
  "pendingRecords": 5,
  "storageHealthy": true,
  "timeSynced": true
}
```

Do not fake values that can be read from hardware/runtime.

---

# 25. POST /api/node/time

Request:

```json
{
  "unixTime": 1720000100
}
```

Return:

```json
{
  "success": true,
  "timeSynced": true
}
```

Validate the timestamp.

---

# 26. OPTIONAL GATEWAY-TO-NODE AUTHENTICATION

If the current ESP32 implementation sends gateway credentials, support configurable headers such as:

```text
X-Gateway-Code
X-Gateway-Token
```

If it does not, keep this feature disabled/configurable rather than breaking compatibility.
Never print tokens.
Do not over-engineer cryptography for the first prototype.

---

# 27. JSON SAFETY

Use ArduinoJson.

Requirements:

- bounded JsonDocument sizes
- check `deserializeJson()` result
- clamp body size
- clamp array sizes
- no giant JSON documents
- do not trust payload lengths blindly
- return structured errors

---

# 28. ERROR RESPONSE FORMAT

Use consistent JSON:

```json
{
  "success": false,
  "errorCode": "INVALID_REQUEST",
  "message": "Invalid ACK payload"
}
```

Suggested error codes:

```text
INVALID_JSON
INVALID_REQUEST
MISSING_DEVICE_CODE
INVALID_LIMIT
RECORD_NOT_FOUND
STORAGE_ERROR
STORAGE_FULL
TIME_INVALID
UNAUTHORIZED
INTERNAL_ERROR
```

Use appropriate HTTP status codes.

---

# 29. LOGGING

Examples:

```text
[NODE] Boot
[NODE] LittleFS mounted
[SENSOR] Reading sensors
[SENSOR] Measurement created: SENSOR-001-100
[STORAGE] Saved SENSOR-001-100
[WIFI] Searching for UAV_GATEWAY
[WIFI] Connected to UAV_GATEWAY
[WIFI] IP: 192.168.4.2
[GATEWAY] Registering SENSOR-001
[GATEWAY] Registration successful
[API] GET /api/node/info
[API] GET /api/node/data
[ACK] Marked SENSOR-001-100 ACKED
[TIME] Gateway time synchronized
[WARN] Gateway unavailable
[ERROR] LittleFS write failed
```

Never log device secrets, Wi-Fi passwords, or gateway tokens.

---

# 30. NON-BLOCKING DESIGN

Avoid long delays.

Conceptual loop:

```cpp
void loop() {
    apiServer.handleClient();
    wifiManager.update();
    gatewayClient.update();
    sensorManager.update();
    storageManager.update();
    timeManager.update();
}
```

The exact API may differ.
Do not introduce unnecessary RTOS complexity.

---

# 31. MEMORY LIMITS

ESP8266 RAM is limited.

Requirements:

- small JSON documents
- bounded arrays
- small batches
- no unbounded containers
- avoid excessive String copying
- avoid loading all records into RAM
- process incrementally where practical
- monitor free heap

Suggested `MAX_API_BATCH_SIZE = 10` or `20`.

---

# 32. FLASH WEAR

Avoid unnecessary writes.

Consider:

- append-oriented storage
- periodic compaction
- minimizing metadata writes
- not persisting volatile state unnecessarily

Explain the implemented strategy.

---

# 33. FAILURE HANDLING

Firmware must keep running when:

- gateway is absent
- Wi-Fi fails
- registration fails
- DHT read fails
- soil sensor is disconnected
- malformed JSON arrives
- LittleFS write fails
- ACK contains invalid IDs
- time sync fails
- gateway disconnects during transfer

A single failure must not reboot the device unless absolutely necessary.

---

# 34. CONFIGURATION

Put configurable values in `Config.h`.

Examples:

```text
DEVICE_CODE
NODE_ID
FARM_ID
ZONE_ID
FIRMWARE_VERSION
DEVICE_TYPE
SENSOR_READ_INTERVAL_MS
GATEWAY_SSID
GATEWAY_BASE_URL
GATEWAY_REGISTER_ENDPOINT
WIFI_RECONNECT_INTERVAL_MS
REGISTRATION_RETRY_INTERVAL_MS
REGISTRATION_HEARTBEAT_MS
HTTP_CONNECT_TIMEOUT_MS
HTTP_REQUEST_TIMEOUT_MS
MAX_LOCAL_RECORDS
MAX_API_BATCH_SIZE
DEMO_MODE
```

Secrets in `Secrets.h`:

```text
GATEWAY_WIFI_PASSWORD
DEVICE_SECRET
optional GATEWAY_TOKEN
```

---

# 35. PLATFORMIO

Create a valid `platformio.ini`.

Use an appropriate ESP8266 board environment.
If the exact board model is unknown, do not guess silently.
Use a default such as NodeMCU v2 only if the hardware matches.

Typical dependencies may include:

```text
ArduinoJson
DHT sensor library
Adafruit Unified Sensor
```

Use framework-provided LittleFS and ESP8266 core libraries.

---

# 36. IMPLEMENTATION PHASES

Implement sequentially and build after every phase.

## PHASE 1 — Bootstrap

- PlatformIO project
- Serial logging
- LittleFS mount
- Config/Secrets structure

Expected:

```text
[NODE] Boot
[NODE] LittleFS mounted
```

## PHASE 2 — Sensor + DEMO_MODE

- SensorManager
- DEMO_MODE
- periodic sampling
- SensorReading model
- sequence generation

## PHASE 3 — Persistent Queue

- StorageManager
- MeasurementQueue
- PENDING records
- reboot recovery
- sequence persistence
- pending count
- storage-full handling

## PHASE 4 — Local Node API

Implement:

```text
GET /api/node/info
GET /api/node/data
POST /api/node/ack
GET /api/node/health
POST /api/node/time
```

## PHASE 5 — Gateway Wi-Fi

- connect to UAV_GATEWAY
- non-blocking reconnect
- preserve sensor acquisition offline

## PHASE 6 — Gateway Registration

- GatewayClient
- authenticated registration
- retry
- heartbeat/re-registration

## PHASE 7 — Time Integration

- gateway time synchronization
- timeSynced state
- measuredAt logic

## PHASE 8 — Final Reliability Integration

- duplicate ACK
- Wi-Fi loss during request
- storage failure
- reboot recovery
- full queue behavior
- memory check
- logging cleanup
- complete README

After each phase:

1. Build with PlatformIO.
2. Fix all compilation errors.
3. Preserve previous phases.
4. Continue automatically only after successful build.

---

# 37. REQUIRED TESTS

## Storage

1. Create 1 measurement.
2. Verify PENDING count = 1.
3. Reboot.
4. Verify record remains.
5. Verify sequence continues without collision.

## Data endpoint

1. Create 15 records.
2. Request `limit=10`.
3. Verify <= 10 records returned.
4. Verify GET does not remove them.

## ACK

1. ACK one record.
2. Verify only that record becomes ACKED.
3. ACK same record again.
4. Verify no corruption.
5. ACK unknown record.
6. Verify firmware remains stable.

## Offline

1. Turn off ESP32 gateway.
2. ESP8266 continues recording.
3. Pending count grows.
4. Turn gateway on.
5. ESP8266 reconnects.

## Registration

1. Valid credentials -> success.
2. Wrong secret -> fails safely.
3. Gateway absent -> retry later.
4. Reconnect -> register again.

## Time

1. No gateway time -> timeSynced=false.
2. Receive valid timestamp -> timeSynced=true.
3. New measurements use estimated Unix time.

## Sensor failure

1. Disconnect DHT22.
2. Firmware stays alive.
3. Other available data may still be recorded.
4. Failed fields become null/NAN.

## Storage full

1. Reach configured maximum.
2. ACKED records can be compacted.
3. PENDING records are never silently removed.

---

# 38. ESP32 API COMPATIBILITY

The ESP8266 must remain compatible with the actual ESP32 Gateway implementation.

Gateway registration:

```text
POST /api/gateway/nodes/register
```

Headers:

```text
X-Device-Code
X-Device-Token
```

Node API expected by ESP32:

```text
GET  /api/node/info
GET  /api/node/data?limit=N
POST /api/node/ack
GET  /api/node/health
POST /api/node/time
```

Do not silently rename endpoints.

Before implementing network integration, inspect the existing ESP32 project and compare its actual API contract with this document.
If there is a conflict, adapt the ESP8266 to the actual ESP32 contract and document the difference.

---

# 39. FIRST END-TO-END TEST

Before connecting physical sensors, use `DEMO_MODE = true`.

Required test:

```text
ESP8266 boots
    |
    v
LittleFS mounts
    |
    v
fake measurement generated
    |
    v
measurement saved
    |
    v
ESP8266 connects to UAV_GATEWAY
    |
    v
registers with ESP32
    |
    v
ESP32 requests /api/node/data
    |
    v
ESP32 stores measurement
    |
    v
ESP32 POSTs ACK
    |
    v
ESP8266 marks record ACKED
```

Only after this works should physical sensors be integrated.

---

# 40. DO NOT IMPLEMENT

Do NOT implement:

- ESP32 gateway firmware
- direct UAV flight control
- motor control
- autopilot
- camera AI
- crop-image analysis
- PostgreSQL client on ESP8266
- direct backend synchronization from ESP8266
- complicated mesh networking
- MQTT unless explicitly requested later
- TLS/HMAC unless the current gateway contract requires it

---

# 41. FINAL BUILD REQUIREMENTS

Before declaring completion:

1. Clean build.
2. Fix all compilation errors.
3. Inspect warnings.
4. Verify ESP8266-specific APIs only.
5. Verify no ESP32-only API is used.
6. Verify LittleFS APIs.
7. Verify ArduinoJson compatibility.
8. Verify ESP8266HTTPClient APIs.
9. Verify ESP8266WebServer APIs.
10. Verify DHT dependency.
11. Verify Secrets.h is ignored by Git.
12. Verify all headers match implementations.
13. Verify no giant blocking loops.
14. Verify no secret values are logged.
15. Verify node API matches ESP32 expectations.

Do not claim hardware validation unless it was actually performed.

---

# 42. REQUIRED FINAL OUTPUT

When implementation is complete, provide:

1. Architecture summary.
2. Final project tree.
3. Files created.
4. Files modified.
5. Configuration values I must edit.
6. Secrets I must configure.
7. Exact PlatformIO build command.
8. Exact upload command.
9. Exact Serial Monitor command.
10. DEMO_MODE test procedure.
11. Real DHT22 + soil-sensor test procedure.
12. ESP32 integration test procedure.
13. Exact expected Serial logs.
14. Known limitations.
15. Remaining TODOs caused by unknown hardware or missing ESP32 behavior.

Do not implement anything outside the documented ESP8266 sensor-node scope.
