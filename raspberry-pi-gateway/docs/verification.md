# Verification and acceptance status

Verification date is 2026-11-07 in Asia/Saigon. The user selected continued simulation testing. Hardware acceptance remains open. VERIFIED below always states its scope.

## Dedicated branch verification — 2026-11-15

Source extracted from local commit `9a494fd` into `raspberry-pi-gateway/` on the dedicated `RaspberryPi` branch. Runtime and tests are unchanged. A fresh virtual environment installed the editable package with test dependencies. `python -m pytest -q` passed all 124 tests in 20.40s, including synthetic video with FFmpeg/ffprobe. `compileall` passed. The CLI simulation reported COMPLETED/REPORTED, one simulated takeoff/consume, landed=true, two synchronized measurements, one synchronized attempt, two channel readings, one unresolved archive and one telemetry row. No aircraft or sensor hardware was contacted. Historical backend/packaging benchmarks below were not rerun for this branch-only extraction.

## Executed evidence

| Check | Result and scope |
|---|---|
| Python suite | 124 passed in 19.82s with FFmpeg/ffprobe on PATH; compileall passed. Tests use loopback HTTP/UDP and actual temporary SQLite. Capture lifecycle faults use injected processes; the optional FFmpeg integration test uses real binaries. |
| Backend contract suite | 40 passed on a disposable local PostgreSQL18 cluster, .NET10.0.401. Filter selected SyncTests, DeviceMissionTests, TelemetryTests and GatewayCredentialTests. Build had0 warnings/errors. Cluster stopped after testing. Log is repository `.local/pi-gateway-contracts.log`. |
| Wheel packaging | Built `gfa26se110_pi_gateway-0.1.0-py3-none-any.whl` (44,896 bytes, SHA256 `ca056d9c24e5bb734e88eaccf3cf1ad8070aa96e45145abb0b30e30b8f06dc96`), installed into an isolated venv and reran CLI help plus `python -m gateway.main simulate` outside the source directory. Both packaged SQL migrations are exercised. |
| Installed simulation | Ready state AWAITING_CONFIRMATION, consume1, takeoff1, landed true, mission REPORTED, Backend COMPLETED, two synchronized measurements, one synchronized attempt, two channel readings, one unresolved archive, one telemetry row. |
| Synthetic video | Real FFmpeg9.0.2/ffprobe received synthetic320x240 H.264 over loopback UDP, copied to fragmented MP4, finalized/probed/hashed and uploaded exact bytes through the media mock. `tests/test_video_ffmpeg.py` skips explicitly when binaries are absent. Windows test binary from [Gyan release essentials](https://www.gyan.dev/ffmpeg/builds/), archive SHA256 `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba` verified against publisher checksum. No binary is committed or bundled with the gateway. |
| Backlog test | Final installed package processed 10,000 measurements in 100 batches in 4.880s on the Windows host. Python allocations peaked at 0.257MiB during sync. SQLite files totalled 10.351MiB. This excludes native allocations and physical Pi CPU/RSS. Re-run with `python scripts/benchmark.py --records 10000` after installing the package. |
| Shell syntax | `bash -n scripts/setup-ap.sh` and `bash -n scripts/check-network.sh` passed under Git Bash. Network configuration itself remains NOT TESTED on Linux hardware. |
| Review | A separate reviewer reproduced and checked fixes for stale cancelled plans, blocked quota draining and definite telemetry failures (91 targeted tests). Final cancellation/bench deltas received a further review with52 targeted tests passing and no blockers. |

The Backend tests validate the upstream behavior exercised by its existing integration suite. The Python end-to-end test uses a local Backend mock. A live Python-to-deployed-.NET run remains a deployment acceptance item.

## Phase outputs

| Phase | Created files | Verification and outstanding work |
|---|---|---|
| 1 Repository audit | `docs/compatibility.md`, `docs/architecture.md` | All remote branches fetched and source contracts compared. Pinned source hashes recorded. Production video support MISSING. |
| 2 Foundation | `pyproject.toml`, `gateway/config.py`, `storage.py`, `migrations/*.sql`, config/env templates | SQLite rollback, crash/reopen, backup, quota, FK and migration tests. Host Python3.15.2 used. Pi Python3.11+ remains a deployment check. |
| 3 Sensor gateway | `gateway/sensors.py`, `tests/test_sensors.py`, `docs/sensor-contract.md` | HTTP framing/auth/scope, bounded responses, presence expiry, immutable duplicates, commit-before-ACK and timeout tests. Real ESP8266/AP NOT TESTED. |
| 4 Backend | `gateway/http.py`, `backend.py`, `tests/test_backend.py`, `test_http.py` | Exact envelopes, immutable batch retries, rejected outcomes, unresolved records, telemetry ambiguity and definite retry, request deadlines. Credentials/server configuration still required for deployment. |
| 5 Mission | `gateway/missions.py`, `tests/test_missions.py` | Plan validation, download commit ordering, session/revision/owner gates, reboot recovery, cancellation, terminal result retry. |
| 6 Tello | `gateway/tello.py`, `mocks/tello.py`, `tests/test_tello.py`, `docs/flight-safety.md` | Actual UDP controller tested on loopback. Timeout poisoning, fresh altitude, low battery, stale telemetry, owner checks, rotation, abort and stable landing. Physical flight NOT TESTED. |
| 7 Video | `gateway/video.py`, `mocks/media.py`, `tests/test_video.py`, `test_video_ffmpeg.py`, `docs/video-contract.md` | Injected capture fault tests plus real FFmpeg synthetic H.264 capture/finalization/upload. Resumable upload uses real loopback HTTP with lost replies and checksums. Physical Tello camera remains NOT TESTED; deployed media/Web remains MISSING. |
| 8 Integration/deployment | `gateway/main.py`, `network.py`, `mocks/demo.py`, `sensor.py`, `backend.py`, `tests/test_e2e.py`, `test_runtime.py`, `scripts/*`, `systemd/gateway.service`, README and deployment docs | Installed simulation and runtime no-takeoff/restart checks pass. Linux systemd boot, AP chipset, SD interruption and Pi resource acceptance remain open. |

## Required failure scenarios

| Objective scenario | Evidence |
|---|---|
| 1 valid registration / wrong token | sensor registration parameterized tests and loopback server |
| 2 sensor disconnect | presence expiry, offline collector rejection, request deadline tests |
| 3 duplicate record | ACK timeout replay retains first payload/mission/collection time |
| 4 process loss before commit | subprocess exits inside SQLite transaction, reopen finds no row |
| 5 process loss after commit before ACK | subprocess exits after commit, reopen finds row; collector ACK-loss replay test |
| 6 sensor ACK timeout | collector preserves committed row, retries idempotently |
| 7 Backend receipt with lost response | immutable persisted batch replay, unchanged batchKey |
| 8 unknown timestamp | unresolved archive preserves measuredAt0 and original JSON |
| 9 rejected record | partial batch remains pending, accepted channels retained |
| 10 reboot READY | new per-process session, fresh approval required |
| 11 reboot possibly airborne | RECOVERY_REQUIRED, no UDP on service construction/tick |
| 12 plan revision changes | exact409 code forces new download and blocks consume |
| 13 launch unapproved | mock E2E observes AWAITING_CONFIRMATION before explicit approval |
| 14 Tello timeout | takeoff sent once, poisoned command state, recovery |
| 15 stale/conflicting telemetry | partial fields discarded, stale command interrupted, watchdog land |
| 16 low battery | configured floor test and default25% gate |
| 17 video requested/not requested | opt-out creates no stream/file; requested capture persists metadata |
| 18 interrupted video | startup and monitor quarantine retained partial file |
| 19 interrupted upload | lost init/chunk/complete responses reconcile durable offsets |
| 20 Internet outage/reconnect | stable batch retry and definitely-unsent telemetry retry |
| 21 disk full | quota and injected database failure prevent ACK; delivery can use reserve |
| 22 sensor succeeds/video fails | separate worker test retains synced sensor state and upload file |
| 23 mission results retry | persisted same terminal request replay |
| 24 systemd restart | service forces ground mode, runtime restart test retains recovery, unit ExecStart has only service action |

Process termination exercises SQLite crash recovery. Physical loss of power, controller caches and microSD durability require the hardware checklist.

## Remaining acceptance gates and limits

- Physical Pi/ESP8266/Tello, AP chipset, SSH administration and systemd behavior need a configured device. Hardware was deferred by the user's simulation choice.
- Actual camera bitstream capture on Pi remains unverified. Synthetic H.264 with real FFmpeg passed on Windows; Pi FFmpeg version, camera stream and sustained resource use still need hardware testing.
- Production video is a proposed extension. Implement matching Backend/Web DTOs, upload authorization and playback before enabling `media_contract_enabled`.
- Scheduled real flight needs an independent WAN path for live launch approval. Offline collection works with WAN absent. Automatic Wi-Fi switching is absent and no link is released during uncertain flight.
- Local route control is open-loop and lacks obstacle/position sensing. SDK acknowledgment correlation has protocol limitations documented in flight-safety.md.
- OS DNS resolution can take the host resolver's configured time. Sensor endpoints are literal IPs and their exchange deadlines are bounded. Configure bounded resolver timeouts for WAN.
- Data is retained locally, including confirmed uploads. Operator archival is required before disk usage exhausts the configured reserve. Ingestion stops at the soft database quota. Delivery bookkeeping can use the free-space reserve down to4MiB.
- Definite telemetry connection failures retry. Explicit validation/authentication failures enter HELD and can be requeued with `retry-held ID --kind telemetry`. Ambiguous POST outcomes remain UNCERTAIN because upstream lacks idempotency.

The overall hardware/deployment acceptance has not been marked complete. All15 required artifact categories are present. This report distinguishes the software/mocked evidence from the external checks that remain.
