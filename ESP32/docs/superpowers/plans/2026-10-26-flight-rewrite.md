# Flight core replacement implementation plan

**Goal:** Replace flight control behind existing ESP32 API and backend contracts.
**Architecture:** Single FlightStateManager coordinator; bounded RC stream in
Tello transport; independent altitude decision engine; mission and flight-test
adapters own persistence and reporting only.
**Spec:** ../specs/2026-10-26-flight-rewrite.md (approved by user).
**Constraints:** No physical commands/uploads, no credentials reads, preserve
existing working files outside this scope and all stored data formats. Work in
the current user checkout because it contains the complete uncommitted firmware.

- [x] Replace RC queue/timer implementation; exercise refresh at 100 ms,
  actual-send-based 300 ms pulse, enqueue timeout, neutral retry on send failure,
  stale-data stop, delayed replies and rollover. Keep protocol contract tests.
- [x] Add header-only AltitudeController: pure decision state machine returning
  Wait/Pulse/Down20/Done/Failed; new-sample-only stability, fixed H source,
  90–110 cm/2 seconds, progress reporting and bounded failures. Test in isolation.
- [x] Refactor FlightStateManager into explicit state handlers and make it the
  only flight command caller. Add owner acquisition/release and altitude action
  dispatch. Keep recovery, landing evidence and public status names compatible.
- [x] Replace MissionManager altitude implementation with coordinator requests;
  remove obsolete timers. Adapt manual and flight-test paths to ownership.
  Preserve launch consume, journals, result sync and backend behavior.
- [x] Run tests/run_tello_host.ps1, tests/run_mission_host.ps1 and
  tests/run_flight_test_host.ps1. Fix regressions against contracts, not by
  weakening evidence or flight guards. Build esp32dev, mission_flight, flight_test.
- [x] Review complete changes, record verification and hardware limitations in
  docs. Leave changes reviewable in current checkout without committing unrelated
  pre-existing work.

Execution uses subagent-driven-development for independent transport/altitude
units, with coordinator integration and final verification by the primary agent.
