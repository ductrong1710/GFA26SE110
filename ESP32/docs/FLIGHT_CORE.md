# Flight core (2026-10-26)

The public local API and backend routes are unchanged. Upload `mission_flight`
for scheduled missions. `esp32dev` remains ground-only; `flight_test` retains
the fixed backend flight-test contract. No filesystem erase is required.

## Ownership and boundaries

`FlightStateManager` is the command coordinator. Manual, Mission and FlightTest
owners are mutually exclusive. Preparation takes ownership; confirmed landing
and Internet switch or safe preflight cancellation releases it. Landing remains
available as a stop action. Reboot restores possible-flight recovery and never
replays takeoff. Mission journals and sensor/result data formats are unchanged.

`MissionManager` now manages schedule, journal, route, collection and upload.
Its old altitude timers and RC pulse implementation have been removed.
`AltitudeController` independently evaluates new telemetry and returns an action;
the coordinator dispatches it and reports its completion. Flight-test and manual
adapters also send actions through the coordinator. Existing validated ACK/source
checks, ground evidence and storage helpers were retained, not blindly deleted.

## RC stream

The rewritten transport refreshes an active setpoint every 100 ms. A mission
pulse starts its 300 ms lifetime on the first successful UDP send, not enqueue.
Manual setpoints expire after 500 ms without renewal. No neutral or movement
request is treated as a discrete command ACK. Failed sends are surfaced; a failed
neutral remains pending and retries at cadence. Coordinator flight failure
conservatively initiates mission landing rather than relying on a later retry.
Stale/invalid height or sensor disagreement stops all RC axes and never resumes
the old setpoint when data recovers. Discrete flight commands are not replayed.

## Altitude

H remains primary, ToF cross-checks and constrains ascent. Mission target 100 cm
accepts 90â€“110 cm for two seconds on distinct fresh low-velocity samples. Data
outside operating bounds cannot complete stabilization. Ascent corrections use one `up 20` command at a time, followed by its ACK
and fresh telemetry. No positive RC is used for altitude correction. A step
is withheld if it would exceed the accepted target band or conservative H/ToF
ceiling. Down corrections retain bounded negative RC/20 cm commands. Fresh telemetry is required after each action, with
500 ms reassessment. Sensor disagreement and the 120 cm ceiling remain active.
The 20 second deadline reports `ALTITUDE_TIMEOUT_NO_PROGRESS` or
`ALTITUDE_TIMEOUT_NOT_STABLE`. `RC_SEND_FAILED` identifies transport failures.

## Verification

Run `tests/run_altitude_host.ps1`, `tests/run_flight_core_host.ps1`,
`tests/run_tello_host.ps1`, `tests/run_mission_host.ps1`, and
`tests/run_flight_test_host.ps1`. Build all three environments with PlatformIO.
Tests include rollover, failed sends/stops, ownership conflicts, stale/conflicting
data, boundary acceptance, landing rejection, reboot and ordered result upload.
Simulation cannot establish physical altitude accuracy or command delivery.
This source change does not calibrate H/ToF or guarantee mission success.

Ascent update: discrete `up 20` commands are not retried automatically.
The 90–110 cm mission band, 20 second deadline and 120 cm ceiling remain.
For example, H=80/ToF=96 permits up 20; ToF=105 blocks it because
105+20 exceeds the ceiling. Firmware build/tests do not verify physical accuracy.

## Collection failure continuation (2026-10-27)

A target timeout or reported collection failure records FAILED and any partial
record count, cancels only that collection attempt, and persists its result.
The mission continues other targets at the same waypoint and subsequent points.
After the planned return leg, it lands and uploads every target outcome. Any
failed target makes the overall result FAILED; successful data is retained.
The first collection failure reason/waypoint is retained for reporting. A target
which never started has zero records, never the preceding target's record count.

Flight/link/telemetry faults, storage failures and operator abort still use the
landing/recovery path. No return movement is guessed after an uncertain move or
reboot. Return uses the existing local-axis route and is not absolute positioning.
Host scenarios cover an unavailable first sensor, a partial collection failure,
and all targets failing, checking the second point is visited and return precedes
landing, followed by mixed result upload. Physical flight remains unverified.
