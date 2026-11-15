# Raspberry Pi Zero 2 W gateway

Python replacement gateway for GFA26SE110. Runtime uses Python 3.11+ standard libraries, SQLite WAL, bounded HTTP/UDP and FFmpeg for optional H.264 recording. Work lives on the dedicated `RaspberryPi` branch, under `raspberry-pi-gateway/`.

## Run on a laptop

From this directory on Windows PowerShell:

```powershell
python -m venv .venv
.\.venv\Scripts\python -m pip install -e '.[test]'
.\.venv\Scripts\python -m pytest -q
.\.venv\Scripts\python -m gateway.main simulate
```

On Linux/macOS substitute `.venv/bin/python`. Calling `python -m gateway.main` with no arguments also runs simulation. The demonstration binds ephemeral loopback ports, registers a mock ESP8266, downloads a mission, observes owner-approval gating, executes UDP commands against a mock Tello, commits measurements before ACK, uploads per-channel readings and unresolved records, and reports the mission. It prints a JSON summary. Test fixtures contain local-only credentials.

## Runtime modes

| Mode | Entry point | Behavior |
|---|---|---|
| A Ground Simulation | `simulate` | Loopback HTTP/UDP and temporary SQLite |
| B Ground Hardware Test | `bench` | SDK preparation and telemetry, ground mode forced; optional `--video-mission ID --seconds 10` for a persisted plan with `recordVideo=true` |
| C Explicit Flight Test | `flight ID --confirm 'FLY ID'` | Requires flight configuration, operator confirmation, owner approval and one successful consume |
| D Service | `service` | Registration, collection, download and background delivery, ground mode forced |

Hardware commands require `--config /etc/pi-gateway/gateway.json` before the subcommand and private environment variables. Default `GROUND_TEST_MODE=true`. `service` remains in ground mode even if an environment override enables flight for a separate explicit invocation. Stop the service before bench/flight/reconcile to release its process lock.

Pi setup, NetworkManager AP, SSH, systemd, firewall and backup commands are in [deployment.md](docs/deployment.md). Configuration templates are `config/gateway.example.json` and `.env.example`. Use matching trusted sensor identity, token, farm and zone values. Provision backend gateway credentials and the seven literal channel codes before collection.

## Data and recovery

- Sensor registration replies use the ESP8266-compatible `Content-Length` envelope. Actual connection IP determines the node address.
- Each batch commits to SQLite before sensor ACK. Source record identity, original payload, first mission association and collection time remain stable across retry.
- Backend requests persist an immutable body and batch key. Every returned index and source identity is validated. Rejected records remain stored. Permanent transport/configuration errors enter `HELD`.
- `timeSynced=false` data uses the existing unresolved-record archive. Gateway collection time never substitutes for measurement time.
- Mission intent is durable before download acknowledgment, launch consume and takeoff. Ambiguous launch, command timeout and reboot during possible flight require recovery. Landing requires fresh stable telemetry.
- Telemetry POST has no upstream idempotency key. Ambiguous delivery is retained as `UNCERTAIN` for review, preventing automatic duplicate insertion.
- Optional video records only when the downloaded plan explicitly has `recordVideo=true`. It finalizes locally before entering a separate resumable upload queue. Uploads yield after four chunks to prioritize sensor synchronization.

Inspect counts with `status`. Use `backup /absolute/new.db` for a consistent SQLite backup. `retry-held OUTBOX_ID` retries the original request after correcting its cause. `reconcile ID --confirm 'LANDED ID'` requires physical inspection plus stable telemetry. Review [flight-safety.md](docs/flight-safety.md) before hardware tests.

## Contracts and verification

- [Compatibility matrix and pinned source commits](docs/compatibility.md)
- [Architecture and implementation phases](docs/architecture.md)
- [Sensor contract](docs/sensor-contract.md)
- [Backend integration](docs/backend-integration.md)
- [Proposed video API and Backend/Web work](docs/video-contract.md)
- [Verification results and remaining gates](docs/verification.md)
- [Hardware acceptance checklist](docs/hardware-checklist.md)

Production mission video APIs and Web recording/playback controls are absent in the audited upstream branches. This delivery supplies the gateway implementation, mock upload service and proposed contract. Enable production media upload only after the matching API is deployed. Hardware, microSD power-loss behavior, USB adapter compatibility and Pi resource consumption require the checklist measurements. The user selected continued simulation testing for this session.
