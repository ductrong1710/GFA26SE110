# Admin flight-test profile

See the backend [flight-test runbook](../../backend/docs/FLIGHT_TEST_RUNBOOK.md)
for setup, request/response, states and verification boundaries.

`pio run -e flight_test` builds the physical-test profile. It polls authenticated
backend commands while idle, with a configured gateway and ready persistent storage.
The fixed flight target is 100 cm; both height and ground-range telemetry must stay
within 97–103 cm for a continuous 3000 ms with low vertical velocity before landing.
Telemetry loss, low battery, excessive altitude or the local deadline requests
landing. Land acknowledgment alone is insufficient: stable ground telemetry is
required. Shared ordinary altitude limits remain 40 cm; only the exclusive test
executor temporarily selects the 1 m limits.

The journal `/flight-test.json` is staged, read back, atomically replaced and read
back again before claiming and before takeoff. Reboot with a possible takeoff switches
into landing recovery; a journal proving takeoff never started reports failure.
Neither path repeats takeoff. Result HTTP retries reuse the same test ID.
The backend confirms matching ID/status before the journal is marked acknowledged.
Do not erase or replace the filesystem to clear a lock.

In this profile local manual/mission mutation APIs cannot start or move the UAV.
`POST /api/tello/land` remains an emergency stop/landing request. No local start-test
endpoint exists. The ordinary `esp32dev` profile does not poll/claim these commands.

Host simulation: `./tests/run_flight_test_host.ps1`. Real hardware height, radio
switching and landing still require controlled acceptance testing; host tests do
not establish physical safety or successful flight on a real aircraft.

## Wi-Fi/Tello serial diagnostics

Open Serial Monitor at 115200 baud after uploading the updated firmware:

```powershell
.\.tools\platformio\Scripts\pio.exe device monitor -b 115200
```

`[WIFI] STA target=TELLO` marks the requested switch from Internet to Tello.
`[WIFI] CONNECT attempt=... target=TELLO` records each connection attempt.
`[WIFI] CONNECTED target=TELLO ip=...` confirms association with the configured
Tello SSID and a nonzero IP. A missing SSID reports `NOT CONFIGURED`.
Wi-Fi status is logged only when it changes: 0 idle, 1 SSID unavailable,
3 connected, 4 connection failed, 5 connection lost, 6 disconnected.
These status codes are Wi-Fi stack states, not detailed disconnect reason codes.

After Wi-Fi connects, look for `[TELLO] UDP started`, `Command: command` and
`Result: ok`, then `Command: battery?` and a numeric battery result.
`[FLIGHT] LANDED_READY` confirms preparation; `[FLIGHT] Error: ...` gives the
failure code, for example `TELLO_CONNECT_TIMEOUT` or `SDK_HANDSHAKE_FAILED`.
Wi-Fi connection alone does not prove that Tello accepted SDK commands.
Logs contain no Wi-Fi passwords or backend API keys.

If no `target=TELLO` appears, the gateway has not requested a Tello connection.
For backend flight tests, create a queued test as admin and let firmware claim
it itself; a manual claim in Postman does not trigger the aircraft.
