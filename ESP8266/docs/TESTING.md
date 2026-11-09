# Hardware acceptance procedures (not yet executed)

These procedures distinguish firmware compilation and host unit tests from actual
board validation. None of the hardware results below are claimed to have passed.
Use a stable power supply, the correct board/flash layout, matching credentials and
the commands in README.md. Keep captures free of passwords/tokens.

## 1. DEMO_MODE and local API

1. Keep DEMO_MODE=true. Optionally change SENSOR_READ_INTERVAL_MS to 5000 for testing.
2. Provision LittleFS with uploadfs only on a NEW/EMPTY board. Upload normal firmware
   and open the serial monitor at 115200 baud. Subsequent firmware uploads must not
   include uploadfs. Wait one sampling interval for the first saved record.
3. For isolated API/reboot tests, turn off the ESP32 and use a temporary ordinary AP
   with the configured SSID/password. This permits HTTP access while no collector
   consumes records. Registration will fail and retry, which is expected. Find the
   node IP from serial; connect your PC to the same AP.
4. Wait for 15 samples. Run the acceptance script; it requests at most 10 records,
   checks ascending sequence/canonical IDs, null placeholder fields, repeated GET
   retention, wrong authentication, malformed JSON, bounded payloads and bad time.
   The token is entered at a hidden prompt, not placed on the command line:

   ```powershell
   & "$env:USERPROFILE\.platformio\penv\Scripts\python.exe" tools/node_acceptance.py --url http://192.168.4.2 --snapshot before-reboot.json
   ```

   Replace the IP with the actual DHCP address. Missing or bad credentials must return
   401. An oversized 1025-byte body must return 413 without exhausting RAM.
5. Reboot the node without uploading the filesystem. Verify recovered count, unchanged
   first record IDs/values and later new sequences greater than the previous high-water
   reservation. With just record 1 before reboot, the next record should be 65.

   ```powershell
   & "$env:USERPROFILE\.platformio\penv\Scripts\python.exe" tools/node_acceptance.py --url http://192.168.4.2 --compare before-reboot.json
   ```

6. With at least two DEMO records and the real collector still off, test ACKs:

   ```powershell
   & "$env:USERPROFILE\.platformio\penv\Scripts\python.exe" tools/node_acceptance.py --url http://192.168.4.2 --exercise-ack --set-time
   ```

   This explicitly ACKs one demonstration record. Only that ID disappears; a duplicate
   ACK returns ackedCount=0, unknown IDs return zero and malformed arrays change nothing.
   Do not use this option on uncollected real sensor data.
7. Before time synchronization, stored records have timeSynced=false and measuredAt=0.
   After --set-time, wait another interval and inspect a NEW record: it must have
   timeSynced=true and estimated Unix seconds. Old records must remain unchanged.

## 2. ESP32 integration

1. Stop the temporary AP. Configure the existing ESP32 AP password and a TRUSTED_DEVICES
   entry for the node's DEVICE_CODE/DEVICE_SECRET. Do not change gateway endpoints.
2. Verify node GATEWAY_BASE_URL matches the AP IP (default http://192.168.4.1), HTTP_PORT=80,
   REQUIRE_DEVICE_AUTH=true and REQUIRE_GATEWAY_AUTH=false.
3. With the ESP32 off, collect at least three demo records. Confirm saved logs continue
   while Wi-Fi reports unavailable. Reboot the node once and verify recovered records.
4. Turn ESP32 on. Expect association, a DHCP address, registration success, then gateway
   requests for info/data and exact-ID ACKs. Inspect gateway storage/logs to verify the
   same deviceCode+sequence was stored before its ACK. Pending count should decrease.
5. Confirm GET /api/node/health includes deviceCode and real freeHeap/RSSI/status.
   ESP32's GET /api/gateway/nodes should show the node authenticated and online.
6. Repeat with an intentionally wrong node DEVICE_SECRET: Wi-Fi may associate but
   registration must fail safely, retries are spaced apart and local samples remain.
   Restore matching credentials and upload firmware WITHOUT uploadfs.
7. Turn gateway off and on. Node sampling must continue; reconnection must register
   again. Leave the system running beyond multiple heartbeat periods and confirm the
   node does not expire merely because pending count reached zero.
8. If gateway time is synchronized, observe POST /api/node/time and later synchronized
   measurements. If ESP32 has no valid clock, false/zero remains correct node behavior.

## 3. ACK-loss, power interruption, full queue and memory

- **Lost ACK request:** interrupt Wi-Fi after the gateway stores a batch and before
  the ACK reaches the node. The same IDs must reappear and the gateway must deduplicate.
- **Lost ACK response:** deliver an ACK but interrupt the response. Repeating the ACK
  must safely return zero for already-collected IDs, with unrelated records unchanged.
- **Transfer disconnect:** stop/restart the gateway during GET /data. Retrieving data
  again must offer pending records; sending alone never changes state.
- **Power cuts:** on a dedicated test identity, interrupt power during record publication,
  reservation update and ACK rename. Repeat at varied timing. After reboot, committed
  pending records must recover; uncommitted .tmp files are cleaned incrementally;
  no already-used sequence may be reused. Test actual flash behavior before field use.
- **Full queue:** on a dedicated DEMO identity/empty filesystem set MAX_LOCAL_RECORDS=3,
  keep collector off, and wait for four attempts. Exactly three records remain; the
  next sample is refused with STORAGE_FULL. ACK one, wait for cleanup, and verify a
  new sample can be saved. Never lower capacity below an existing pending backlog.
- **Storage failure:** a missing/corrupt filesystem must report a mount/storage error,
  never auto-format or reboot-loop. On a disposable test image, corrupt metadata/record
  bytes; recovery must refuse access without deleting evidence. Do not corrupt field data.
- **Memory/latency:** repeatedly fetch full batches, send malformed/oversized payloads,
  ACK records and reconnect for hours. Track health freeHeap; verify no downward leak,
  watchdog reset, missed responsiveness or gateway timeout at intended batch size.
  Static build RAM percentages do not establish runtime heap or stack margins.

## 4. Real DHT22 test

1. Complete the DEMO end-to-end test first. Power off before wiring. Confirm the actual
   sensor/module pin labels and supply specification.
2. Use 3.3V-compatible wiring: VCC to 3.3V, GND common with ESP8266, DATA to GPIO4
   (NodeMCU D2 with the provisional board). Add a 10k pull-up from DATA to 3.3V if the
   module has none. Bare four-pin sensor and three-pin module pinouts differ; follow
   the actual device labels. See [Adafruit DHT wiring](https://learn.adafruit.com/dht/connecting-to-a-dhtxx-sensor).
3. Set DHT_ENABLED=true. If no soil sensor is attached, set SOIL_ENABLED=false.
4. Build/upload `nodemcuv2_physical` (DEMO_MODE=false), preserving LittleFS. Prefer a
   fresh separately provisioned real-sensor identity after collecting the demonstration
   data, so the backend does not mix demo values with real observations.
5. Verify serial says Physical sensor mode. Wait for a NEW sample; fetch it with the
   gateway or an isolated test AP. Temperature/humidity should be finite and plausible;
   unavailable channels remain null. Compare against a reference instrument.
6. Disconnect DHT DATA and wait for another interval. The node must stay alive, log the
   DHT22 warning and record failed fields as null; other installed sensors still work.
7. Reconnect and verify recovery. Keep at least 2000ms between samples (default 60000ms).

## 5. Real soil-moisture test

1. Confirm your exact analog module output voltage and the board A0 divider BEFORE
   wiring. The bare ESP8266 ADC accepts 0–1.0V; some development boards add a divider.
   Do not assume all NodeMCU-style boards accept the same A0 voltage. Use an appropriate
   divider if needed, and never apply a 5V analog signal directly. See the
   [ESP8266 core ADC reference](https://arduino-esp8266.readthedocs.io/en/latest/reference.html#analog-input).
2. Connect module GND to common ground and analog output to A0 through the verified
   scaling circuit. Power the module according to its datasheet. There is one ADC;
   do not add a second direct analog battery input.
3. Set SOIL_ENABLED=true and build/upload `nodemcuv2_physical`. Set DHT_ENABLED=false
   if DHT22 is not present. Use stable power and inspect several new samples.
4. Determine dry/wet ADC readings using a temporary diagnostic measurement or debug
   inspection of analogRead(A0). Set SOIL_DRY_ADC and SOIL_WET_ADC to your measured
   endpoints. Defaults 800/350 are placeholders, not a physical calibration.
5. Check dry approaches 0%, wet approaches 100%, intermediate moisture lies between;
   values outside the calibrated range clamp to 0..100. Test both calibration directions.
6. Test disconnected/rail signals: values <=2 or >=1021 become null and log a warning.
   A floating input can still produce plausible readings; reliable disconnect detection
   needs appropriate hardware bias/diagnostics. Do not present that heuristic as proof.
7. Run collection/ACK/reboot tests with the real sensor to confirm exactly the same
   persistence and network flow. Calibrate for the actual installation soil.

## Expected serial logs

These are exact message patterns, NOT captured hardware output. Counts, sequence and
IP vary; Wi-Fi/registration may happen before the first sample. 115200 baud is the
application baud; ESP8266 ROM boot messages may use a different baud.

```text
[NODE] Boot
[NODE] LittleFS mounted
[STORAGE] Recovered 0 pending records
[SENSOR] DEMO_MODE enabled; simulated sensors use the normal data path
[API] HTTP server started on port 80
[WIFI] Searching for UAV_GATEWAY
[WIFI] Connected to UAV_GATEWAY
[WIFI] IP: 192.168.4.2
[GATEWAY] Registering SENSOR-001
[GATEWAY] Registration successful
[SENSOR] Reading sensors
[SENSOR] Measurement created: SENSOR-001-1
[STORAGE] Saved SENSOR-001-1
[API] GET /api/node/info
[API] POST /api/node/time
[TIME] Gateway time synchronized
[API] GET /api/node/data
[API] POST /api/node/ack
[ACK] Marked SENSOR-001-1 ACKED
```

Expected failure/configuration messages include:

```text
[WARN] Configure Secrets.h gateway Wi-Fi password; sampling remains active
[WARN] Configure Secrets.h device secret; registration disabled
[WARN] Gateway unavailable
[WARN] Gateway disconnected; sampling continues offline
[WARN] Registration failed; will retry
[ERROR] LittleFS mount failed; data preserved (no auto-format)
[ERROR] Queue recovery failed; stored files preserved
[ERROR] Storage full; pending records preserved, sample not stored
[ERROR] Measurement not stored; storage/sequence error
[SENSOR] Physical sensor mode
[WARN] DHT22 read failed; unavailable fields are null
[WARN] Soil ADC invalid; field is null
```

## Hardware-dependent TODOs

- Confirm board, flash size/layout, serial port, power supply and Wi-Fi range.
- Provision identities/secrets in both devices; run the real demo collection and ACK-loss tests.
- Validate physical LittleFS power-cut recovery, filesystem-full behavior and flash endurance.
- Measure free heap, maximum usable stack, full-batch latency and long-duration stability.
- Confirm DHT22 module wiring/pull-up and sensor accuracy; calibrate soil ADC and voltage scaling.
- Choose real light/pH/water-level/battery hardware before adding their drivers.
- Select sampling interval/storage capacity for the required UAV revisit interval.
- Verify gateway clock availability, clock drift and future handling of unsynchronized readings.
