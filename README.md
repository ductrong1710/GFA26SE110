# ESP8266 Sensor Node

Firmware for **UAV-Assisted IoT Platform for Farm Monitoring and Sensor Data Collection**.
The node samples sensors, stores measurements in LittleFS, joins the ESP32 UAV_GATEWAY
AP when available, and exposes records for collection. Only an authenticated exact-ID
ACK transitions a measurement out of PENDING. A GET, registration, timeout or lost
connection never ACKs data.

Default: **DEMO_MODE=true**. No board was flashed and no hardware tests were performed
during implementation. See [verification evidence](docs/VERIFICATION.md) and the
[hardware acceptance procedures](docs/TESTING.md).

## Phase implementation

| Phase | Implementation |
|---|---|
| 1 — Bootstrap + LittleFS | Pinned PlatformIO ESP8266 project, serial logger, configuration and ignored credentials, mount without auto-format. |
| 2 — SensorManager + DEMO_MODE | Periodic sampling, simulated temperature/humidity/soil values, isolated DHT22 and analog input drivers, unavailable fields are null. |
| 3 — Persistent queue | Checksummed per-record files, atomic publication and ACK rename, persistent block-reserved sequences, bounded sorted index, reboot recovery and storage-full protection. |
| 4 — Local REST API | All five routes, device authentication, strict bounded JSON, oldest-first batches, idempotent exact-ID ACKs and real health data. |
| 5 — Gateway Wi-Fi | STA association/reconnect state machine; acquisition continues offline; credentials are not repeatedly written to SDK flash. |
| 6 — Registration | Real ESP32 headers/body, response validation, reconnect registration, retry/heartbeat and bounded HTTP transport. |
| 7 — Time integration | Gateway Unix time plus wrap-safe elapsed milliseconds, fractional remainder, unsynchronized zero timestamps, no direct NTP. |
| 8 — Reliability | Interrupted-write cleanup, missing/corrupt metadata fail-closed behavior, exhaustion protection, pre-allocation HTTP bounds, host fault tests, physical-mode build and final clean build. |

Every phase was built successfully before implementation continued to the next.
The master prompt was read completely before changes. ESP32 sources were inspected
before the API/Wi-Fi phases; [contract differences](docs/ESP32_CONTRACT.md) are recorded.

## Architecture and data safety

`main.cpp` only calls `NodeApp.begin()` and `NodeApp.update()`. NodeApp coordinates
SensorManager, MeasurementQueue, StorageManager, ApiServer, WiFiManager,
GatewayClient and TimeManager. Only StorageManager knows LittleFS. The same
measurement, queue, network, registration and ACK code serves both sensor modes.

Persistent layout on the device:

```text
/identity                          device identity marker
/sequence0, /sequence1             checksummed reserved sequence high-water marks
/pending/0000000001.rec             checksum + immutable measurement JSON
/acked/0000000001.rec               same payload; directory denotes ACKED state
```

Each file begins with eight hexadecimal CRC32 digits and a newline, followed by
JSON. A new file is written to `.tmp`, flushed, closed, re-read and checked, then
atomically renamed. Pending records remain untouched during GET. An exact-ID ACK
renames that one file into `/acked`; maintenance removes one ACKED file per second,
then canonical uncommitted record `.tmp` files. The payload's original PENDING
field is immutable; the ACKED directory is the authoritative collected state.

Sequence identity is `DEVICE_CODE-sequence` with a uint32 sequence to match ESP32.
The node commits a reservation of 64 IDs before using any of them. Reboot skips the
unused portion: after record 1, a reboot normally starts at 65. This reduces sequence
metadata writes to one per 64 samples during continuous operation. Each sample still
needs its durable record write, each ACK one rename, and cleanup one deletion.
At uint32 exhaustion, new samples stop being stored rather than reusing IDs.

There are two metadata slots and an identity marker. Corrupt committed metadata,
missing established slots, invalid records or a changed DEVICE_CODE fail closed;
files are preserved for manual recovery. A low-space reserve allows metadata/ACK
operations. Full storage refuses new samples, logs the failure, and exposes
`STORAGE_FULL`; it never evicts PENDING records. Filesystem failures latch unhealthy
until reboot/recovery; they do not reboot the firmware automatically.

**Do not run uploadfs, erase flash, delete metadata or restore an old filesystem
backup on an established identity.** Those operations can erase pending data or
roll back historical IDs already stored at the gateway. If intentionally starting
fresh, provision a new deviceCode and matching gateway credential entry.

## Board and configuration

The exact board was not supplied. `nodemcuv2` is a **provisional compile target** for
NodeMCU 1.0 / ESP-12E with 4 MB flash and a 1 MB LittleFS partition. Confirm your
actual board and flash size before upload; adjust board and linker partition in
platformio.ini if necessary. This is not a claim that your hardware is NodeMCU.

Edit `include/Config.h`:

| Values | Default / action |
|---|---|
| DEVICE_CODE, NODE_ID, FARM_ID, ZONE_ID | SENSOR-001, 1, 1, 1. Set provisioned identity and farm/zone. Never change deviceCode over an existing queue. |
| FIRMWARE_VERSION, DEVICE_TYPE | 1.0.0, ESP8266_SENSOR; gateway metadata strings <=32 bytes. |
| DEMO_MODE | true. Physical environment overrides to false. |
| SENSOR_READ_INTERVAL_MS | 60000; use 5000 for temporary demo tests, minimum 2000. |
| GATEWAY_SSID, GATEWAY_BASE_URL | UAV_GATEWAY, http://192.168.4.1. Match the gateway. |
| GATEWAY_REGISTER_ENDPOINT, HTTP_PORT | /api/gateway/nodes/register, 80; match existing ESP32. |
| WIFI_RECONNECT_INTERVAL_MS / WIFI_CONNECT_TIMEOUT_MS | 15000 / 10000. |
| REGISTRATION_RETRY_INTERVAL_MS / REGISTRATION_HEARTBEAT_MS | 15000 / 20000; heartbeat must remain below ESP32's 30000ms node timeout. |
| HTTP_CONNECT_TIMEOUT_MS / HTTP_REQUEST_TIMEOUT_MS | 500 / 750. |
| HTTP_BODY_TIMEOUT_MS / HTTP_TOTAL_TIMEOUT_MS | 1000 / 2500; bounded synchronous registration, not fully asynchronous HTTP. |
| MAX_LOCAL_RECORDS / MAX_API_BATCH_SIZE | 256 / 10; actual usable capacity also depends on LittleFS free space. |
| MAX_API_BODY_BYTES / MAX_RECORD_BYTES | 1024 / 768; larger values require memory and gateway contract review. |
| MIN_FREE_STORAGE_BYTES / SEQUENCE_RESERVATION_SIZE | 16384 / 64. |
| REQUIRE_DEVICE_AUTH | true; matches the ESP32 client's node credentials. |
| REQUIRE_GATEWAY_AUTH / GATEWAY_CODE | false / GATEWAY-001. Leave disabled for current ESP32. |
| DHT_ENABLED / DHT_PIN | true / GPIO4 (NodeMCU D2). |
| SOIL_ENABLED / SOIL_DRY_ADC / SOIL_WET_ADC | true / 800 / 350; replace calibration with measured values. |
| SOIL_DISCONNECTED_LOW / HIGH | 2 / 1021; rail rejection is only a heuristic, not reliable unplug detection. |

Set disabled sensor flags to false for sensors not installed. Light, pH, water level
and battery fields remain null until real hardware/driver choices are made.

## Secrets

`include/Secrets.h` is ignored by Git. An empty local copy is provided; from a fresh
clone copy `include/Secrets.example.h` to `include/Secrets.h` without overwriting an
existing configured file. Builds also work without Secrets.h using empty defaults.

Provide these values inside namespace `Secrets`:

| Value | What to provide |
|---|---|
| GATEWAY_WIFI_PASSWORD | The ESP32 `Secrets::GATEWAY_AP_PASSWORD`, 8–63 characters. |
| DEVICE_SECRET | Exact secret in ESP32 `TRUSTED_DEVICES` for Config::DEVICE_CODE; use printable ASCII, preferably <=128 bytes to fit bounded HTTP headers. |
| GATEWAY_TOKEN | Empty for current gateway; configure only with REQUIRE_GATEWAY_AUTH and a gateway that sends X-Gateway-Code/Token. |

Empty Wi-Fi credentials disable association; empty device secret prevents registration
and makes authenticated node endpoints reject requests. Sampling and storage still
run. Tokens/passwords, registration response bodies and incoming request bodies
are never printed. Do not enable upstream HTTP debug logging in a secret-bearing build.

## Exact commands (PowerShell)

Use the installed PlatformIO executable below; `pio` is equivalent if it is on PATH.
Set COM_PORT to the actual port printed by `device list` (the value below is an
example, not detected hardware).

```powershell
Set-Location 'D:\Learning\SEP\Code\ESP8266\esp8266-sensor-node'
$pio = "$env:USERPROFILE\.platformio\penv\Scripts\pio.exe"
& $pio device list
$COM_PORT = 'COM5' # REPLACE with the detected port

# Build DEMO_MODE=true
& $pio run -e nodemcuv2

# NEW/EMPTY board only: provision LittleFS ONCE (erases the filesystem)
& $pio run -e nodemcuv2 -t uploadfs --upload-port $COM_PORT

# Upload firmware; normal firmware updates do not upload a filesystem image
& $pio run -e nodemcuv2 -t upload --upload-port $COM_PORT

# Serial Monitor; Ctrl+C exits
& $pio device monitor --port $COM_PORT --baud 115200

# Physical DHT22/soil build and upload, only after demo integration succeeds
& $pio run -e nodemcuv2_physical
& $pio run -e nodemcuv2_physical -t upload --upload-port $COM_PORT

# Clean default build
& $pio run -e nodemcuv2 -t clean
& $pio run -e nodemcuv2

# Host tests (PlatformIO libraries must first have been downloaded by a build)
& $pio pkg install --global --tool 'platformio/toolchain-gccmingw32'
& "$env:USERPROFILE\.platformio\penv\Scripts\python.exe" tests/run_host.py
```

See [TESTING.md](docs/TESTING.md) for demo, reboot, ACK-loss, ESP32 integration,
DHT22 and soil-moisture tests, exact expected log patterns and hardware TODOs.

## Project tree and file inventory

All files below were created for this implementation. No pre-existing source file
was modified; the master prompt, ESP32 and backend were unchanged. The pre-existing
deleted root AGENTS/API/ARCHITECTURE/BUSINESS_RULES/DATABASE documents were preserved
as found, not restored. Build-generated files are excluded from this tree.

```text
esp8266-sensor-node/
├── .gitignore
├── platformio.ini
├── README.md
├── data/bootstrap.txt
├── docs/
│   ├── IMPLEMENTATION_PLAN.md
│   ├── ESP32_CONTRACT.md
│   ├── TESTING.md
│   └── VERIFICATION.md
├── include/
│   ├── Config.h
│   ├── Credentials.h
│   ├── Secrets.example.h
│   ├── Secrets.h                 local, ignored; empty values
│   ├── Models.h
│   ├── MeasurementCodec.h
│   ├── RecordStore.h
│   ├── ErrorCodes.h
│   ├── Logger.h
│   ├── SensorMath.h
│   ├── RetryTimer.h
│   ├── JsonSafety.h
│   ├── HttpBounds.h
│   └── GatewayProtocol.h
├── src/
│   ├── main.cpp
│   ├── NodeApp.h / NodeApp.cpp
│   ├── SensorManager.h / SensorManager.cpp
│   ├── StorageManager.h / StorageManager.cpp
│   ├── MeasurementQueue.h / MeasurementQueue.cpp
│   ├── ApiServer.h / ApiServer.cpp
│   ├── WiFiManager.h / WiFiManager.cpp
│   ├── GatewayClient.h / GatewayClient.cpp
│   └── TimeManager.h / TimeManager.cpp
├── scripts/bound_webserver.py
├── tools/node_acceptance.py
└── tests/
    ├── run_host.py
    └── host/
        ├── MemoryStore.h
        ├── gateway_test.cpp
        ├── http_bounds_test.cpp
        ├── json_test.cpp
        ├── queue_test.cpp
        ├── retry_test.cpp
        ├── sensor_test.cpp
        └── time_test.cpp
```

## Known limitations

- Hardware and power-cut behavior remain unverified. Host storage tests replace the
  physical I/O boundary; they do not simulate the ESP8266 flash controller or prove
  LittleFS atomicity on a real board.
- HTTP and flash operations are bounded but synchronous. DHT reads also take time.
  Sampling may run late during a request; it never tries to backfill imaginary samples.
  Wi-Fi association has no waiting loop. Measure request latency under full queues.
- Default capacity is 256 readings (about 4h16m at one-minute intervals if every
  sample fits). Size the partition/interval/capacity for the real offline duration.
  Reaching capacity loses new sampling opportunities explicitly; it preserves older
  pending data. This firmware does not promise unlimited offline retention.
- Time after synchronization drifts with the oscillator; it resets to unsynchronized
  on reboot. Historical unsynchronized records are not assigned invented timestamps.
  update() must execute at least once per millis wrap (~49.7 days), which the loop does.
- The gateway currently discards node uptimeMs and state on ingestion; sequence still
  provides order. Gateway time must already be synchronized to send time to the node.
- Soil calibration is board/sensor/soil dependent. A floating ADC may look plausible;
  rail detection alone cannot prove a disconnected sensor. Battery cannot share the
  one ADC with soil without additional hardware; the battery field stays null.
- Shared device-token HTTP on a local Wi-Fi network is the existing prototype
  contract. No TLS/HMAC, MQTT, direct backend/database access or UAV control is added.
- Web-server bounds use a reproducible build-local patch of ESP8266 Arduino 3.1.2.
  Upgrading that pinned core requires re-review; unmatched patch sites fail the build.
- CRC detects accidental corruption; it is not cryptographic authentication. Failed
  committed storage requires manual recovery; firmware never formats away evidence.
