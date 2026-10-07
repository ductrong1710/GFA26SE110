# ESP32 UAV Gateway — Phases 1–8

ESP32 Dev Module (4 MB), PlatformIO, Arduino, LittleFS, ArduinoJson 6.21.5.

Implemented: AP + optional STA, authenticated in-memory node registry, local REST
APIs, persistent measurement deduplication, save-before-ACK sensor collection,
NTP time and repository-compatible backend synchronization. No ESP8266 firmware
is included. The isolated Tello extension adds single-ESP32 flight control and
post-landing Internet synchronization; see [Tello ground and flight tests](docs/TELLO_OPERATIONS.md).

Start with [configuration, build/upload commands and complete test runbook](docs/OPERATIONS.md).
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
