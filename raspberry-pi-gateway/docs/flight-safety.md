# Flight ownership and recovery

`MissionEngine` owns the route. `SafetyController` owns command UDP, current flight owner, fresh telemetry and landing evidence. Service and foreground commands share an operating-system lock over the data directory. Systemd service execution always enables ground mode.

## Launch sequence

1. Validate assigned gateway/UAV, scheduled mission, current revision, target mappings and local coordinates. Commit the plan and waypoint rows before acknowledging download.
2. The explicit operator invocation creates a fresh 32-hex session. SDK readiness, synchronized host clock, fresh ground telemetry and the configured battery minimum of at least25% must pass before readiness is advertised.
3. The mission owner approves the current plan/session through the existing Backend launch workflow. The gateway accepts only `READY_TO_START` followed by one `START_ACCEPTED` response from consume.
4. Persist consume intent before the consume request and takeoff intent before UDP. Any ambiguous consume or possibly-airborne restart enters `RECOVERY_REQUIRED`. A repeated consume response of `CONSUMED` cannot execute flight.
5. Execute centimeter route movements, waypoint holds and bounded target collection. Return to origin and land. Landing is attempted before camera finalization or hashing.

Movement stays within the Backend route envelope. X maps to forward/back, Y to right/left, axes run sequentially, segments are20–100cm with short-tail redistribution, and return movement counts toward128 steps. Altitude targets are80–120cm. GPS fields never become SDK movements. A separate rotation method enforces owner, fresh telemetry and the same altitude envelope. A rotated manual trajectory requires its own coordinate-frame accounting.

## Telemetry and command uncertainty

Every state datagram replaces the previous sample. Partial datagrams cannot reuse old landing fields. Required flight data are height, ToF, vertical speed and battery. Telemetry older than2s, altitude disagreement over25cm, insufficient battery or a ceiling violation inhibit motion. The watchdog requests RC neutral and landing when safety checks fail. A waiting movement command checks telemetry every50ms so the command deadline cannot conceal stale telemetry.

SDK responses have no sequence identifier. The transport serializes requests, validates peer IP/port, drains pending datagrams and imposes a command gap. A timeout poisons the command stream and prevents movement/takeoff retries. Landing remains permitted as a recovery action. Arbitrarily delayed duplicate UDP acknowledgments cannot be perfectly correlated by this protocol. Hardware testing must establish the radio/SDK behavior within these limitations.

Default landing confirmation requires at least10 distinct stable post-command samples over5s, height and ToF at most15cm and absolute vertical speed at most5. Rejected or uncertain landing commands require the stronger zero-height/zero-vertical-speed signature. Missing telemetry keeps recovery active. The gateway retains flight ownership until evidence confirms landing.

## Operator intervention

After physically checking that the aircraft is on the ground, invoke `reconcile ID --confirm 'LANDED ID'` through the documented privileged launcher. It re-establishes SDK state reception, waits for fresh stable ground evidence and records cancellation for Backend reconciliation. Cancellation is valid even if an uncertain launch never reached the Backend. Use the service afterward to acknowledge cancellation and then send retained data.

`cancel ID --reason '...'` accepts an unconsumed grounded plan and persists a cancellation for delivery. An airborne emergency uses the owning safety controller's `abort(owner)` or a console interrupt of the explicit flight process, which enters the landing path. Closing a process or sending land alone does not prove that the aircraft landed. Restart always audits persisted mission state.

## Verification boundary

Tests use a loopback UDP simulator with configurable lost responses, battery, telemetry, altitude discrepancy and landing behavior. They exercise the same production controller. Test timing parameters are shortened only for loopback. Tests do not establish physical altitude accuracy, obstacle clearance, payload capacity, endurance, Linux radio behavior or SD power-loss durability. Complete the hardware checklist before supervised physical flight.
