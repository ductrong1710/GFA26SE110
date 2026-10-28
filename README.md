# ESP32 UAV Gateway — Phases 1–8

Admin-triggered 1m/3s flight tests use the explicit [flight-test profile](docs/FLIGHT_TEST.md).

Mission pull and autonomous Tello extension: see [MISSION_OPERATIONS.md](docs/MISSION_OPERATIONS.md).
Default ground-test mode prevents takeoff; the configurable altitude ceiling is 40 cm.
For backend Tello missions, use the explicit `mission_flight` profile with a 1 m
target and 0.8–1.2 m operating range. See [mission setup and local start](docs/MISSION_FLIGHT.md).
The current backend lacks device mission endpoints; the adapter/mock does not imply live backend integration.

ESP32 Dev Module (4 MB), PlatformIO, Arduino, LittleFS, ArduinoJson 6.21.5.

Implemented: AP + optional STA, authenticated in-memory node registry, local REST
APIs, persistent measurement deduplication, save-before-ACK sensor collection,
NTP time and repository-compatible backend synchronization. No ESP8266 firmware
is included. The isolated Tello extension adds single-ESP32 flight control and
post-landing Internet synchronization; see [Tello ground and flight tests](docs/TELLO_OPERATIONS.md).

Start with [configuration, build/upload commands and complete test runbook](docs/OPERATIONS.md).
Configure Wi-Fi and backend without reflashing at `/config`: see [local configuration](docs/CONFIGURATION.md).
See [file inventory](docs/FILES.md) and [Phase 4 registration API tests](docs/PHASE4.md).

The existing AP remains UAV_GATEWAY at http://192.168.4.1. Secrets.h is ignored
by Git. Optional Internet/backend configuration is disabled until filled in.
Do not upload a filesystem image over pending measurements.

Build from this directory in PowerShell:

```powershell
.\.tools\platformio\Scripts\pio.exe run -e esp32dev
```

HTTP APIs:

- GET /api/gateway/status
- GET /api/gateway/nodes
- GET /api/gateway/time
- POST /api/gateway/nodes/register (X-Device-Code + X-Device-Token required)

Phases 5–8 have build and host mock verification; real hardware integration and
power-interruption tests remain to be performed using the runbook. Earlier
hardware verification does not establish validation of these new phases.

## Audit remediation (2026-10-28)

All local Tello and mission POST controls, including land/abort, require access through
UAV_GATEWAY, HTTP Digest authentication (`admin`, configured AP password), and the
current `X-Config-Token` returned as `csrfToken` by authenticated GET `/api/config`.
Basic authentication and requests arriving through STA are rejected. The token changes
at reboot. Existing unauthenticated curl/PowerShell examples must supply these credentials.
Public status GETs and node registration retain their existing contracts.

PowerShell example (prompts for the AP password, does not put it in shell history):
```powershell
$gatewayConfig = curl.exe --silent --show-error --digest --user admin http://192.168.4.1/api/config | ConvertFrom-Json
# Run a control only when intended; this example requests landing.
curl.exe --digest --user admin -X POST -H "X-Config-Token: $($gatewayConfig.csrfToken)" http://192.168.4.1/api/tello/land
```
The manual `tests/tello_http.py` checks prompt for the password and fetch the token;
they send invalid control inputs only. Neither this documentation nor tests were used
to send live flight commands during remediation.
