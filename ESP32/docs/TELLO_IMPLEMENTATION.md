# Single-ESP32 Tello integration work log

Authoritative specification: ../ESP32_TELLO_CONTROL_AND_POST_LAND_SYNC_PROMPT.md
Branch reviewed: ESP32, initial HEAD ef9f6a6. Existing source is authoritative.

Pre-change review: all include/src modules, platformio.ini, WebServer body-limit
middleware, README/OPERATIONS, mock tools and existing tests inspected. Storage
already has a 22-character Base64URL key fix and detailed write diagnostics;
preserve these. Collection uses cooperative stages but its HTTP transport is
synchronous with bounded timeouts. Registry retains offline entries. API enforces
1024-byte POST limits before allocation. Existing backend adapter and data safety
remain unchanged. Phase 3 tests are historical and cannot validate current auth.

Design: GatewayWiFiManager owns the single STA target and asynchronous disconnect/
connect transitions while keeping AP enabled. TelloController owns two UDP sockets,
bounded telemetry and one response-requiring command. FlightStateManager owns
prepare/flight/landing and only releases the Tello link after landing evidence.
ApiServer adds bounded validated requests using those managers. Backend and NTP
must use target-aware readiness. Stop active SNTP on leaving Internet, retaining
system time. Commands never trigger takeoff implicitly. Timeouts never prove land.

Implementation sequence and evidence:

- [x] T1: target-aware network role, AP preservation, baseline and phase build.
- [x] T2: bounded UDP, SDK handshake, battery, telemetry; build.
- [x] T3: status/prepare API and asynchronous preparation; build.
- [x] T4: takeoff/land/move/rotate/RC with state/range guards; build.
- [x] T5: ACK plus landing evidence/settle, protocol stop, Internet transition;
  NTP gating and stop; build.
- [x] T6: backend readiness guard, truthful sync states; build.
- [x] T7: integration tests, clean build, ground/first-flight procedures.

Each phase must build successfully with warnings inspected before starting the
next. Do not upload or send live aircraft commands during implementation.

Test targets: unavailable SSID, handshake rejection/timeout, query/telemetry,
invalid movement/RC, state guards, land timeout, stale/missing telemetry without
switch, positive landing evidence, post-land Internet failures/backend recovery,
next prepare, node collection and persistent pending data. Distinguish host
tests/build evidence from manual hardware tests in final documentation.

SDK reference: https://dl-cdn.ryzerobotics.com/downloads/tello/20180910/Tello%20SDK%20Documentation%20EN_1.3.pdf

T1 evidence: baseline build passed (24.871 s); phase build passed (15.291 s),
48684 bytes static RAM / 890829 bytes flash. No compiler warnings/errors in
.tools/tello-t1-build.log. Backend/NTP readiness guards were moved into T1 so
the new Tello target cannot accidentally allow Internet work between phases.
SNTP is stopped before leaving Internet; TimeManager clears its started flag
when networking is unavailable and can restart on returning to Internet.
No hardware upload or runtime Wi-Fi claim. TELLO_SSID/PASSWORD added empty to
ignored Secrets.h without changing existing credentials.

T2: native tests run the production controller/parser with a fake UDP driver and
clock. Verified SDK ACK, numeric battery reply, malformed/expired telemetry,
bounded read retries and disconnect. Firmware build SUCCESS (45.184 s), no
compiler warnings. Tests first failed because the new modules did not exist.

T3: prepare/status API integrated, asynchronous connection/SDK/battery stages,
no implicit takeoff. Production state-manager host tests cover unavailable SSID
timeout and preparation completion. Build SUCCESS (39.701 s), no compiler warnings.

T4: build SUCCESS (14.646 s), no compiler warnings. Native tests verify explicit
takeoff guard, ground telemetry requirement, move/rotation/RC range validation,
RC expiry to neutral, telemetry loss while flying retaining Tello target, and
land ACK remaining in Landing. No automatic retries of movement/takeoff/land.

T5: build SUCCESS (22.258 s), no compiler warnings. Host tests now cover stable
landing samples and settling, protocol shutdown before target switch, next
mission preparation, safe ground-only disconnect, land timeout keeping link,
contradictory telemetry followed by loss not confirming landing, and a clean
land ACK plus conservative timeout fallback. No hardware flight validation.

T6: build SUCCESS (14.664 s), no compiler warnings. Existing BackendAdapter and
storage remain unchanged. Main feeds real pending/storage/configuration state
into flight status; missing configuration/storage cannot report SyncComplete.

T7 review found and corrected: SDK recovery had not rearmed after losing both
handshake replies while Wi-Fi remained connected. Regression reproduced failure,
then passed after bounded recovery retry. RC traffic marks response attribution
uncertain; flight uncertainty survives UDP restart. Neither permits ACK-only
landing fallback; fresh stable telemetry remains required in those cases.

T7 final clean build SUCCESS (49.916 s), 49444 bytes static RAM, 905169 bytes
flash, firmware.bin 911744 bytes. All T1–T7 compiler logs inspected: no warning:
or error: lines. Native suites pass under -Wall -Wextra -Werror, exercising
production protocol/flight/telemetry and production network/time implementations
with fake drivers. Ten existing Python mock tests pass. HTTP smoke script syntax
checked; not run against hardware. Independent read-only review rechecked the
recovery fix and reported no remaining concrete important finding.
The existing storage_diagnostics environment also builds successfully (39.487 s),
with no compiler warning/error lines. Secrets.h ignore status verified.

## Completion audit against master prompt

| Prompt sections | Current implementation/evidence |
|---|---|
| 1–4, 30–31 | GatewayWiFiManager StaTarget, SSID/IP/role readiness, disconnect barrier, AP unchanged; native network tests; channel caveat in runbook |
| 5–8, 29, 34, 36 | TelloController two STA-bound WiFiUDP sockets, SDK ACK, battery query, serialized bounded commands; official SDK ranges; native protocol tests and all phase builds |
| 9 | Latest-only RC slot, range guard,100ms rate,500ms expiry/neutral, flight-state restriction; native replacement/expiry tests |
| 10 | Bounded telemetry parser, battery/height/tof/time/vgz/attitude/timestamps, per-packet landing validity; malformed/expiry tests; no LittleFS telemetry writes |
| 11–14, 20 | FlightStateManager and API prepare/explicit takeoff/land; battery/state/fresh-ground guards; next-mission native tests |
| 15–16 | Land ACK followed by stable samples/settle or logged conservative fallback; uncertainty restrictions; stop UDP then Internet; native positive/negative landing tests |
| 17–18 | BackendSyncManager uses target-aware readiness, TimeManager stops/restarts NTP; no BackendAdapter changes; production network/time tests |
| 19, 33, 39 | Main retains registry/collection/storage calls during Tello mission; no new blocking protocol waits; unchanged collection/save-before-ACK source; real latency integration procedure and limitations documented |
| 21–28 | All seven required routes plus ground-only disconnect; bounded JSON/int/range validation; host command guards; post-upload tests/tello_http.py supplied, not hardware-executed |
| 32 | Connect/SDK/flight/landing timeouts, no loss-as-landing, bounded SDK recovery, retained target/data; native fault tests and ground procedures |
| 35, 40 | Tello isolated; Config flag disables prepare; existing storage/registry/client/adapter untouched; git diff checked |
| 37–38, 42 | Exact ground test, hover-only first flight, sensor integration and fault matrix in TELLO_OPERATIONS.md; native/host evidence separated from manual hardware validation |
| 41 | T1–T7 sequential build evidence above; final clean build after integration fixes |
| 43 | Initial branch/source review recorded above, before production changes; actual short storage filenames preserved |
| 44 | Architecture/files/states/UDP/routes/config/commands/tests/limitations/manual steps delivered in TELLO_OPERATIONS.md |

No hardware flight, radio continuity, HTTP-on-board, flash power interruption or
real backend integration claim is made. Those are manual validation steps, not
substituted by host test results. No source changes to other projects or upload.
