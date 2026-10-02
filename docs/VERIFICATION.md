# Verification record — 2026-10-01

Scope: local source review, Windows host tests and PlatformIO compilation. **No
ESP8266/ESP32 hardware was flashed or exercised.** See TESTING.md for outstanding
hardware validation. No hardware success is implied by the table below.

## Sequential phase gates

All eight phases were implemented in order and each gate succeeded before the next
phase was started. Initial compilation errors were fixed within their own phases.

| Gate | Result | Static RAM | Flash |
|---|---|---:|---:|
| 1 Bootstrap + LittleFS | SUCCESS | 28,680 | 294,291 |
| 2 SensorManager + DEMO | SUCCESS | 28,860 | 295,119 |
| 3 Persistent queue | SUCCESS | 30,672 | 310,559 |
| 4 Node REST API | SUCCESS | 33,184 | 350,275 |
| 5 Gateway Wi-Fi | SUCCESS | 33,448 | 352,323 |
| 6 Gateway registration | SUCCESS | 33,532 | 352,639 |
| 7 Time integration | SUCCESS | 33,532 | 352,831 |
| 8 Final, nonempty synthetic credentials | SUCCESS | 33,952 (41.4%) | 359,823 (34.5%) |
| Physical mode, nonempty synthetic credentials | SUCCESS | 34,044 (41.6%) | 361,659 (34.6%) |
| Final clean DEMO build, restored empty secrets | SUCCESS | 33,752 (41.2%) | 352,047 (33.7%) |

Available static RAM: 81,920 bytes. App flash region: 1,044,464 bytes. These figures
exclude dynamic heap/stack use. Nonempty fake compile credentials retained the
network paths that empty defaults can let the compiler eliminate. Those fake values
were replaced with the empty example afterward; no real credential was needed.

Earlier gates were observed directly in command output. Final configured, physical,
clean and default build logs are retained locally under ignored `build-logs/`.
Final logs were scanned for `warning:` and `error:` with no matches.
`pio run -e nodemcuv2 -t buildfs` also succeeded, producing a LittleFS image from
data/bootstrap.txt. No filesystem image was uploaded to any device.

Compilation fixes: phase 2 retained Arduino.h's C-linkage setup/loop declarations;
phase 4 used ESP8266WebServer's variadic collectHeaders API and removed misleading
indentation. No ESP32-only HTTP timeout API remains in ESP8266 code.

## Automated host tests

Command: `python tests/run_host.py` using PlatformIO Python and its Windows GCC
toolchain. Each program compiles with C++17, -Wall -Wextra -Werror. All seven pass:

| Program | Behaviors exercised |
|---|---|
| gateway_test | Actual registration payload fields and exact success/authenticated/deviceCode response checks. |
| http_bounds_test | Content-Length overflow/invalid input, maximum request line, malformed line endings, bounded stalled reads. |
| json_test | Object-only JSON, trailing garbage, embedded NUL, mixed ACK types, count clamp, bad limits and timestamp range/type. |
| queue_test | Save/read/reboot, stable identity and skipped reservations, nonmutating GET, exact/duplicate/unknown ACK, failed ACK rename, failed writes, full queue and low space, repeated interrupted writes/cleanup, record/metadata corruption, missing newest/both metadata slots, uint32 exhaustion. |
| retry_test | Immediate first attempt, interval boundary, rollover and reconnect reset. |
| sensor_test | Dry/wet/midpoint, reversed calibration, clamping, invalid rails and equal endpoints. |
| time_test | Unsynced zero, synchronization, fractional accumulation, millis wrap, invalid sync preservation, timestamp maximum and reboot reset. |

Tests exercise production C++ queue/policies/clock with a MemoryStore at the physical
storage boundary. They do not verify Arduino LittleFS internals, Wi-Fi radio behavior,
real HTTP socket timing, physical sensor accuracy or power-cut atomicity. A live-node
acceptance client is provided; only its --help and Python syntax checks were run.

## Compatibility and final review

- PlatformIO Core 6.2.0; espressif8266 4.2.1; Arduino ESP8266 core 3.1.2;
  xtensa GCC 10.3.0. NodeMCU v2 is explicitly provisional hardware.
- ArduinoJson 6.21.5 uses bounded StaticJsonDocument/DynamicJsonDocument and checked
  deserialization/overflow. All seven host tests use this same downloaded library.
- DHT 1.4.6 and Adafruit Unified Sensor 1.1.15; physical mode compiles and links.
- Framework LittleFS: no auto-format; open/write/flush/close/re-read/rename and directory
  APIs inspected and compiled. Per-record files are checksum protected.
- Framework ESP8266WebServer: generated build-local bounded copy is selected by LDF;
  original shared package is untouched. 256-byte lines, 20 headers, 2048 aggregate
  header bytes, 1024-byte bodies and bounded read/send waits. Multipart and chunked
  requests are rejected; data responses have explicit Content-Length for ESP32.
- ESP8266HTTPClient: explicit WiFiClient lifetime, connect/request/total deadlines,
  2048-byte total inbound response budget, <512-byte response body, HTTP/1.0,
  auth headers and cleanup. No ESP32 setConnectTimeout dependency.
- All header/implementation pairs compile in demo and real sensor mode. main.cpp
  has only includes, NodeApp instance, setup and loop.
- Git check-ignore confirms include/Secrets.h and generated library are ignored.
  Final local Secrets.h byte-for-byte matches the empty example.
- All serial/log call sites inspected: no passwords, device tokens, gateway tokens,
  request bodies or response bodies are logged. Upstream HTTP debug logging stays off.
- All application while loops inspected: they are bounded by file/directory size,
  byte count or timeout and yield where required. No delay-based sampling or Wi-Fi wait loop.
- Queue RAM holds at most 256 uint32 sequence numbers (1024 bytes), not all records.
  API uses one 1536-byte heap JSON document plus one record output buffer; data streams
  records incrementally. Runtime heap/stack headroom remains a hardware test.
- ESP32 contract reviewed before network implementation; auth/health/sequence/time/
  framing differences documented in ESP32_CONTRACT.md. No ESP32/backend changes.
- Independent read-only review found interrupted .tmp retention; a failing regression
  test was added, canonical incremental cleanup fixed it, and the test now passes.
  Its response-header bounds recommendation was also implemented after inspecting
  the installed HTTPClient code. Additional metadata-loss tests drove fail-closed fixes.

## Delivery audit

README.md contains every phase summary, architecture/storage tradeoff, complete
project file tree (all new files), configuration/secret values, exact build/upload/
monitor commands and known limitations. TESTING.md contains DEMO/local API, ESP32,
ACK-loss, reboot/full-storage, DHT22 and soil tests, expected log patterns and remaining
hardware TODOs. Existing user deletions and master prompt remain unchanged.

Runtime/hardware acceptance remains explicitly unperformed, as requested; the
deliverable is implemented, build-verified firmware plus executable/manual procedures.
