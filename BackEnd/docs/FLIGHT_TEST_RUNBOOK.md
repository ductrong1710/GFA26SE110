# Admin gateway/UAV flight test

This feature queues a physical flight test. Use a clear test area and a grounded
UAV. Development verification uses simulated UDP/time; it does not fly hardware.

## Backend reachable from ESP32

Apply the new migration using your configured PostgreSQL connection:

```powershell
dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
dotnet run --project src/FarmMonitoring.Api --launch-profile device-http
```

The opt-in `device-http` development profile listens on port 5043 on the computer's
network interfaces. This is for a trusted local network: firmware currently uses
HTTP and cannot use the default HTTPS development endpoint. Allow this port on the
intended local network, and configure the ESP32 backend base URL to
`http://<computer-LAN-IP>:5043/api`. Do not use `localhost` from the ESP32.
Do not configure `ASPNETCORE_HTTPS_PORT` for this HTTP-only process; a redirect to
HTTPS will prevent firmware communication. The default `https` profile is unchanged.

## API workflow

First upload the dedicated firmware from `Code/ESP32`:

```powershell
.\.tools\platformio\Scripts\pio.exe run -e flight_test -t upload
```

This explicitly enables physical flight from admin backend commands. The normal
`esp32dev` build stays in ground mode and does not claim flight tests. The
`flight_test` profile blocks local manual/mission start and movement APIs; local
`POST /api/tello/land` remains available to stop the test. Do not erase flash or
upload a filesystem image. Configure the Tello SSID/password using the existing
firmware setup, and the backend gateway identity/key through `/config`.

While a test is active or its result is awaiting acknowledgment, configuration
saves and ordinary collection/mission processing are locked. Local
`GET /api/gateway/status` exposes `flightTestId`, `flightTestLocked`, and
`flightTestError` for diagnosis without exposing credentials.

Authenticate as a current `FarmAdministrator`; the gateway must have an active
credential and an assigned active UAV. Each deliberate test gets a new `requestKey`.
Reuse the same key when retrying an uncertain HTTP response, so retries cannot
create another flight.

```http
POST /api/gateways/1/flight-tests
Authorization: Bearer <admin-access-token>
Content-Type: application/json

{"requestKey":"bench-test-20261019-001"}
```

Record the returned `data.id` and read progress with:

```http
GET /api/gateways/1/flight-tests/<id>
Authorization: Bearer <admin-access-token>
```

QUEUED means the backend accepted the request, not that either device is connected.
CLAIMED means the gateway received and claimed it. COMPLETED requires a device
result confirming connection, the 100 cm target, a 3000 ms hold, and landing.
An offline gateway leaves the request queued until it expires; it must not fly a
stale request after reconnecting. A claimed or recovery-required test is not
automatically retried. Inspect the device and its result before attempting another.
The gateway conservatively attempts landing after an interrupted test that may
have taken off; a journal proving takeoff never started reports failure instead.
It never repeats takeoff after reboot. If landing cannot be confirmed, its local
lock and the backend reservation remain in place. No automatic time-based unlock
or force-reset endpoint is provided. Fix connectivity and establish ground telemetry
before retrying; do not erase the journal to clear an uncertain flight.

The hold timer starts after the UAV reaches the target altitude, not when the HTTP
request is made. ESP32 completes the flight locally while its Wi-Fi radio is on
Tello, then reconnects to Internet Wi-Fi to upload its result. Backend connectivity
may therefore be absent during the flight. Sensor records are retained.

## Verification boundaries

Host and API tests can prove command authorization, persistence, parsing, simulated
flight timing and replay guards. They cannot prove the real aircraft's altitude,
radio quality or landing behavior. Hardware acceptance remains a separate test
after the firmware profile and backend configuration have been installed.
