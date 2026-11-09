# Tello mission execution

Use the explicit `mission_flight` build for the backend's Tello mission profile.
The ordinary `esp32dev` build remains ground-only. `flight_test` is for the
separate administrator flight-test workflow and does not run missions.

```powershell
pio run -e mission_flight
# Upload when the aircraft is on the ground and the operator is ready:
pio run -e mission_flight -t upload
pio device monitor -b 115200
```

Uploading firmware preserves existing NVS configuration and measurement files.
Do not erase flash or upload a filesystem image to install this change.

## Preparation and execution

1. Configure the gateway Internet Wi-Fi, HTTP backend URL, gateway identity and
   device key in its local setup portal. The backend must listen on an interface
   reachable from that Wi-Fi. Tello SSID remains configured in `include/Secrets.h`.
2. In the web application choose a UAV whose model contains `Tello` before
   defining the route. Set local points measured from the takeoff origin:
   X is forward and Y is right relative to the initial heading. These are meters,
   not latitude/longitude. No GPS conversion is performed.
3. Use 0.8–1.2 m altitude (nominal 1 m), MOVE/HOLD/COLLECT, maximum 16 waypoints,
   8 targets and 30 seconds hold per point. Each nonzero axis movement, including
   return to origin, must be at least 20 cm. The planner also checks the 128-step
   capacity. Firmware performs takeoff, X-then-Y legs, return to origin and landing.
   The configured altitude range is a control target, not a guaranteed physical
   ceiling during Tello takeoff or disturbances.
4. Schedule the mission with its mapped targets, UAV and gateway. The ESP32
   downloads and persists it, acknowledges `/downloaded`, and stays READY.
5. Join the gateway AP. Inspect `GET http://192.168.4.1/api/mission/status`.
   With the flight area clear, explicitly send `POST /api/mission/start` to this
   local gateway. Do not manually call backend device `/start` to trigger flight.
   Firmware obtains the backend RUNNING acknowledgment before switching to Tello.
6. Observe `[WIFI]`, `[TELLO]`, `[MISSION]`, `[COLLECT]`, `[SYNC]` and
   `[MISSION-SYNC]` logs. Backend RUNNING means acceptance of the workflow;
   telemetry and gateway state establish whether the aircraft actually took off.

The local start is refused when backend acknowledgment fails. Missing ground
telemetry waits for a bounded interval before `GROUND_TELEMETRY_TIMEOUT`.
Battery, fresh telemetry, storage, command acknowledgment and landing checks
remain active. A reboot during possible flight enters recovery and never
automatically replays takeoff.

## Data and terminal status

Collected readings persist their mission ID. Sync batches contain readings from
one mission only; legacy background readings retain a null mission ID. Duplicate
stored identities are not reassigned to a new mission.

After landing, readings sync first. Target outcomes use the existing device
`/sync` API and must be acknowledged before `/complete`. Failures/cancellations
use `/fail` or `/cancel` and then sync outcomes. Stable batch keys and persisted
acknowledgments support retries; no `/result` endpoint is assumed. Unsynced or
invalid old readings can keep a mission waiting for sensor sync: inspect the
per-record sync errors before changing stored data.

## Verification

`tests/run_mission_host.ps1` covers the backend envelope, explicit start,
download/start acknowledgments, route execution, local collection, result sync,
landing and reboot recovery. The end-to-end simulation runs both the existing
low-altitude configuration and `mission_flight` with return to origin.
`tests/run_measurement_host.ps1` covers local record migration and mixed-mission
batch ownership. Host simulations do not establish physical flight performance.
