# ESP32 flight rewrite — design for review

## Scope agreed with the user

Replace ESP32 Tello control and flight orchestration. Preserve backend/frontend
API contracts, Wi-Fi configuration portal, credentials, sensor collection,
measurement storage, heartbeat and result upload. No erase-flash operation.
Preserve existing uncommitted changes outside the replaced flight subsystem.

## Problem and limits of this change

Missions 13–16 show successful SDK entry, takeoff and landing. RC requests reach
the UDP send boundary, but height rises slowly and H/ToF remain different.
Rewriting code cannot calibrate sensors or prove physical command delivery.
The replacement must make command ownership, deadlines, evidence and failure
paths explicit rather than increase flight limits to force mission completion.

## Alternatives

1. Keep patching the existing managers: least disruption, but keeps overlapping
   state and timer ownership. Not selected because the user requested replacement.
2. Replace the ESP32 flight core behind compatible interfaces: selected proposal.
3. Replace backend and frontend as well: excluded by the user's scope decision.

## Proposed architecture

- Tello transport: UDP command/reply and telemetry only. Separate discrete SDK
  commands from RC frames. One pending discrete command. SDK/battery reads may
  retry; takeoff, movement and rotation never automatically replay after timeout.
  Preserve source validation, exact ACK parsing and late-response quarantine.
- Flight coordinator: sole owner of takeoff, movement, RC and landing decisions.
  Mission, manual endpoints and flight-test commands request actions through it.
  Explicit owner arbitration prevents competing controllers.
- Altitude evaluator/controller: one assessment shared by control, route gating
  and diagnostics. H remains the fixed primary source; no automatic ToF switch.
  ToF remains a cross-check and conservative ascent constraint.
- Mission adapter: scheduled launch/consume, durable checkpoints, waypoint
  sequence, collection and upload. It requests flight actions and consumes their
  results; it no longer independently runs RC pulse timers or landing logic.

State progression: idle → connecting → SDK → preflight → takeoff → settling →
executing → landing → grounded → reconnecting → reporting. Failure while flight
is possible routes to landing/recovery, never directly to idle or Internet.
Externally visible status values remain compatible with current APIs.

## Flight behavior

- Preflight validates configuration, battery and fresh ground evidence.
- Persist possible-flight state before sending takeoff. An uncertain takeoff
  result cannot cause another takeoff attempt.
- Mission target remains 100 cm, accepted H band 90–110 cm for 2 seconds with
  distinct fresh samples and low vertical velocity. Ceiling stays 120 cm.
- Keep the current bounded RC amplitudes (10 far below target, 3 near it),
  300 ms pulse and 500 ms reassessment as initial tuning. The redesign moves
  these into one controller; it does not assume stronger control is needed.
- RC activation time starts at successful transmission, not at enqueue. During
  the bounded active window, refresh the current RC frame at the existing
  100 ms cadence, checking telemetry and limits before every transmission.
  Stop on deadline, abort, stale data, conflicting sensors or link failure.
  Repetition is only for a live RC setpoint, never a discrete flight command.
- Stop corrections inside the accepted band and evaluate stability. Exiting
  the band, invalid velocity or stale data resets the stability interval.
- Sensor disagreement over 25 cm pauses correction; sustained fresh evidence
  for 2 seconds triggers landing. Keep the 20 second stabilization deadline.
  Report progress at timeout so lack of response is distinguishable from a
  changing altitude that has not settled.
- Landing requires post-command evidence. ACK alone does not establish ground
  in a mission. A rejected land command can complete only with the existing
  stricter sustained ground criteria, not because it returned an error.
- Reboot preserves recovery state. No automatic takeoff/route replay or deletion
  of mission files. Operator abort/recovery remains available locally.

## Compatibility and replacement

Replace implementations of TelloController, FlightStateManager and the flight
parts of MissionManager/FlightTestManager. Public adapters may retain names to
avoid changing external APIs. Replace obsolete altitude timers/helpers rather
than keeping a second flight implementation enabled. Review telemetry parsing
and safety evaluation against existing protocol regression cases.

Keep migration of stored mission state conservative: existing possible-flight
states enter recovery, terminal unuploaded results remain available for upload.
Do not lose sensor records when aborting or replacing a flight implementation.

## Verification and delivery

Host tests cover owner conflicts; protocol/source validation; lost/late ACKs;
RC refresh, expiry and failed sends; stale/disagreeing telemetry during active
RC; accepted-band timing; abort during each phase; landing rejection; reboot
and corrupt journals; launch consume uncertainty; ordered result upload.

Build esp32dev, mission_flight and flight_test. Keep all existing relevant
contract tests, replacing implementation-specific assertions only with a stated
reason. Hardware validation remains separate: compile/tests do not demonstrate
flight accuracy. Deliver the replacement source, test results and upload steps.
Do not upload or issue physical flight commands from the agent during this work.

Status: approved by the user and implemented. Host verification and firmware builds completed; physical flight validation remains outstanding.
