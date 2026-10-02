# ESP8266 Sensor Node implementation plan

Spec: ../../ESP8266_SENSOR_NODE_MASTER_PROMPT.md (read completely before implementation).
Execute inline, sequentially, with a successful PlatformIO build at every phase gate, as explicitly requested.
Keep the existing checkout and user deletions; add the firmware under esp8266-sensor-node/.

Architecture: SensorManager produces a SensorReading; MeasurementQueue obtains a reserved sequence and commits an immutable measurement through StorageManager. API reads never mutate records. An authenticated exact-ID ACK atomically renames a pending file to ACKED; incremental cleanup removes ACKED files only. TimeManager maintains a monotonic reference. WiFiManager owns STA reconnect; GatewayClient owns registration and bounded HTTP. main.cpp only wires NodeApp.

Storage alternatives considered: NDJSON plus ACK journal requires crash-safe compaction and repeated scans; per-record files cost flash blocks but offer small, atomic transitions. Use per-record files and a bounded sorted sequence index, plus two checksummed high-water reservation slots. Never format on mount failure. Fail closed on corrupt committed data/metadata. Reserve 64 sequence numbers at a time; reboot skips unused reserved values. Stop at uint32 exhaustion to match ESP32.

## Sequential gates
- [x] Phase 1: platformio.ini, Config/Credentials/Secrets.example/Logger, StorageManager mount, small main. Build nodemcuv2.
- [x] Phase 2: Models, SensorManager, periodic NodeApp sampling, demo values and physical null/failure paths. Build.
- [x] Phase 3: durable sequence reservations, atomic queue records/ACK rename, boot recovery, sorted bounded batch reads, full-queue safety. Host fault tests and build.
- [x] Phase 4: ApiServer plus strict bounded JSON parsing, device authentication, info/data/ack/health/time transport; inspect ESP32 first. API test tool and build.
- [x] Phase 5: WiFiManager nonblocking association/retry/disconnect handling; offline sampling unchanged. Build.
- [x] Phase 6: GatewayClient authenticated registration, bounded response and timeouts, retry/reconnect/20s heartbeat. Build.
- [x] Phase 7: TimeManager synchronized Unix reference, wrap-safe elapsed time and measurement integration. Host time tests and build.
- [x] Phase 8: failure-path review, test fault cases, request limits, cleanup/memory/log checks, README and contract/test documentation. Build then clean build plus real-sensor compilation.

Contract inspected: ESP32 src/ApiServer.cpp, SensorNodeClient.cpp, CollectionManager.cpp, GatewayMeasurement.cpp, BoundedHttp.cpp and include/Config.h. Requests to nodes use X-Device-Code and X-Device-Token (not X-Gateway-*); health must include deviceCode; sequence is uint32; synchronized timestamps range 1577836800..4102444800. ESP32 uses 10 records/batch, port 80, 30s node timeout. It ignores uptimeMs/state when ingesting; these remain in node storage/API.

Verification: pin ESP8266/ArduinoJson/DHT dependencies; compile every phase before next edits; exercise real portable policy/sequence/time logic and storage with host filesystem doubles if compiler available; provide hardware HTTP acceptance script without pretending to run it. Final inspect no secrets logging, bounded buffers, no Wi-Fi wait loops, library compatibility, Git ignore, and document hardware limitations.

