# Phase 4 — Node registry and shared-token authentication

Scope stops at authenticated registration. LittleFS mounting, AP mode, status,
time, JSON errors, and request-body protection from Phases 1–3 remain in place.
No collection, measurements, persistent registry, NTP, or backend logic is added.
Firmware is built but not automatically uploaded because this board needs BOOT.

## Architecture

- `NodeAuthenticator` receives a read-only credential array and handles lookup
  and exact token matching. The two requested prototype credentials are in the
  ignored local `Secrets.h`. The committed example contains replacement values.
- `NodeRegistry` owns a fixed array of 20 nodes (`MAX_SENSOR_NODES`). It finds and
  updates by deviceCode, never by IP. Full registries still allow updates to an
  existing node; adding another node returns 503. Offline slots are retained,
  not automatically evicted. Reboot clears the RAM registry.
- `SensorNode` contains bounded-by-validation metadata, actual remote IPv4,
  authentication/online flags, millis-based lastSeen, and RSSI=0 (unknown).
  It contains no credentials. Farm/zone must be positive integers; metadata
  strings are 1–32 printable ASCII characters, and code is at most 64 bytes of
  non-space printable ASCII. Optional JSON IP must be valid IPv4, but is never
  authoritative. To change stored IP, reconnect from a different actual IP.
- `ApiServer` receives references to the single registry and authenticator.
  Registration validates the body, fields, and credentials before any mutation.
  Nodes JSON streams one bounded document at a time, avoiding a giant JSON array
  or response buffer. Read endpoints do not refresh node lastSeen.
- `update()` runs each loop without delays and marks online nodes offline when
  unsigned `now-lastSeenMs > NODE_TIMEOUT_MS` (30,000). Unsigned subtraction works
  across millis rollover. `touchNode()` is available to future authenticated
  communication handlers; it must not be called for unauthenticated traffic.
- Unknown RSSI is isolated in `Config::UNKNOWN_NODE_RSSI`. No IDF RSSI code added.

Registration flow: bounded body → JSON/object validation → required metadata →
header presence → header/body match → trusted code → exact token → registry update
→ HTTP 200. Missing credentials, unknown device, and wrong token return distinct
401 error codes; mismatch returns 400 `DEVICE_CODE_MISMATCH`.

## Build and hardware validation

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
.\.tools\platformio\Scripts\pio.exe run
```

Upload firmware manually with your usual BOOT procedure; do not uploadfs or erase
the already provisioned filesystem. Reset to clear the RAM registry, connect to
UAV_GATEWAY, then run all nine requested tests plus malformed-request tests:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\phase4.ps1 -Token 'sensor-secret-001'
```

The script prints PASS 1 through PASS 9, checks status codes and JSON, waits 32
seconds for timeout, checks re-registration, and tests missing credentials,
missing code, empty/malformed bodies, malformed IPv4, oversized bodies, status,
time, and 404. It requires an initially empty registry and no other sensor sending
registration requests. It does not upload firmware or print secrets.

For individual requests in PowerShell (5.1 or later), use these exact commands:

```powershell
Set-Location D:\Learning\SEP\Code\ESP32
$base = 'http://192.168.4.1'
$payload = @{ deviceCode='SENSOR-001'; farmId=1; zoneId=1; firmwareVersion='1.0.0'; deviceType='ESP8266_SENSOR'; ipAddress='192.168.4.250' }
function Save-TestBody { param($Value)
    [IO.File]::WriteAllText((Join-Path $PWD '.pio\phase4-request.json'), ($Value | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
}
Save-TestBody $payload

# TEST 1: 200, authenticated=true
curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-001' -H 'X-Device-Token: sensor-secret-001' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register"

# TEST 2: 401 AUTHENTICATION_FAILED
curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-001' -H 'X-Device-Token: incorrect' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register"

# TEST 3: 401 UNKNOWN_DEVICE
$payload.deviceCode='SENSOR-999'; Save-TestBody $payload
curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-999' -H 'X-Device-Token: invalid' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register"

# TEST 4: 400 DEVICE_CODE_MISMATCH
$payload.deviceCode='SENSOR-002'; Save-TestBody $payload
curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-001' -H 'X-Device-Token: sensor-secret-001' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register"

# TEST 5: repeat twice; nodes count stays 1 after starting from an empty registry
$payload.deviceCode='SENSOR-001'; Save-TestBody $payload
1..2 | ForEach-Object { curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-001' -H 'X-Device-Token: sensor-secret-001' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register" }
curl.exe --noproxy '*' -i "$base/api/gateway/nodes"

# TEST 6: update existing metadata; changing claimed JSON IP does NOT spoof actual IP
$payload.firmwareVersion='1.0.1'; $payload.farmId=2; $payload.zoneId=3; $payload.ipAddress='192.168.4.249'; Save-TestBody $payload
curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-001' -H 'X-Device-Token: sensor-secret-001' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register"

# TEST 7: actual node data, updated fields, authoritative client IP
curl.exe --noproxy '*' -i "$base/api/gateway/nodes"

# TEST 8: offline, count still 1
Start-Sleep -Seconds 32
curl.exe --noproxy '*' -i "$base/api/gateway/nodes"

# TEST 9: online again, same registry entry
curl.exe --noproxy '*' -i -H 'Content-Type: application/json' -H 'X-Device-Code: SENSOR-001' -H 'X-Device-Token: sensor-secret-001' --data-binary '@.pio/phase4-request.json' "$base/api/gateway/nodes/register"
curl.exe --noproxy '*' -i "$base/api/gateway/nodes"
```

For actual IP-change verification in Test 6, repeat the successful POST from a
second laptop connected to UAV_GATEWAY. The stored IP must become that laptop's
IP while count stays 1. Do not change the gateway AP IP or spoof JSON IP to test it.

### Registry-full test

Only two trusted devices are configured, so the default 20-entry registry cannot
be filled by those devices. To exercise the full branch without adding fake
trusted devices, temporarily set `MAX_SENSOR_NODES = 1` in Config.h, build, and
manually upload that test firmware. Reset, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\phase4.ps1 -Token 'sensor-secret-001' -TestCapacity -Token2 'sensor-secret-002'
```

It fills the one slot with SENSOR-001, verifies SENSOR-002 returns 503, and verifies
updating SENSOR-001 still succeeds. Restore `MAX_SENSOR_NODES = 20`, rebuild, and
manually upload the normal firmware afterwards. No such test configuration change
or upload was performed automatically.

## Expected Serial output

Startup remains:

```text
[GATEWAY] Boot
[GATEWAY] LittleFS mounted
[GATEWAY] Starting Wi-Fi
[GATEWAY] AP started
[GATEWAY] SSID: UAV_GATEWAY
[GATEWAY] AP IP: 192.168.4.1
[API] Starting HTTP server
[API] HTTP server started on port 80
```

For a valid registration, repeat registration, then 30 seconds without activity:

```text
[API] POST /api/gateway/nodes/register
[NODE] Registration request: SENSOR-001
[AUTH] SENSOR-001 authenticated
[NODE] SENSOR-001 added to registry
[API] POST /api/gateway/nodes/register
[NODE] Registration request: SENSOR-001
[AUTH] SENSOR-001 authenticated
[NODE] SENSOR-001 registration updated
[NODE] SENSOR-001 marked offline
```

Failed credentials log `[WARN] Authentication failed for SENSOR-001`; unknown
devices log `[WARN] Unknown device attempted registration: SENSOR-999`.

## Prototype security limitations

Shared tokens over local HTTP are not production-grade security: no TLS, replay
protection, HMAC, provisioning, rate limiting, or constant-time token comparison.
Anyone possessing a valid token can impersonate that device. Read APIs are local
and unauthenticated. Farm/zone metadata is self-reported, not authorization data.
No secrets are stored in registry objects or sent in API responses. Secrets.h
stays ignored. Framework verbose diagnostics are disabled with CORE_DEBUG_LEVEL=0
to prevent raw header/body diagnostics; do not enable them with real credentials.

Phase 3's parsing-only test script is historical and expects 202; use phase4.ps1.
This implementation has been built, but these HTTP tests require manual upload
and have not been claimed as hardware-verified.
