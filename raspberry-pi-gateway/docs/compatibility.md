# Phase 1 — compatibility audit (2026-11-06)

Source snapshots fetched from https://github.com/ductrong1710/GFA26SE110:

| Branch | Commit |
|---|---|
| BackEnd | `4f4f80d4e07cbd008fcdf5fd009f381937b5522f` |
| ESP32 | `4e313ba43d9ab2ca2a445e52b0e5daafea261ae6` |
| ESP6288 | `19a489e60838bc2f5a999d25c95b4ccf4038b6f4` |
| FrontEnd | `ebf7604604bf2cd13e2524d49064aff3d9560145` |

VERIFIED means inspected in these sources, not tested on physical equipment. PROPOSED means an extension/design; MISSING means absent upstream; NOT TESTED means no execution evidence yet.

| Existing responsibility | Python replacement | Wire contract | Audit |
|---|---|---|---|
| Gateway registration / trusted registry | sensors registration HTTP server | POST `/api/gateway/nodes/register`; X-Device-Code + X-Device-Token; success/authenticated/deviceCode | VERIFIED; compact nonchunked Content-Length response <512 bytes required |
| SensorNodeClient / CollectionManager | sensors client + collector | GET info/data/health; POST ack/time | VERIFIED; max10 records, save and commit before ACK |
| StorageManager | storage.Store | SQLite WAL/FULL + migrations + unique source identity | PROPOSED replacement for filesystem queues |
| BackendAdapter / BackendSyncManager | backend client + durable outbox | POST `/api/device/gateways/{id}/sync` | VERIFIED; channel readings, original unresolved payload, exact immutable batch retry |
| MissionBackendClient | missions engine | next, downloaded, launch/ready, launch/consume, complete/fail/cancel | VERIFIED; no use of legacy start |
| MissionRoutePlanner | missions route validator | localX/localY meters; MOVE/HOLD/COLLECT | VERIFIED; 1–16 waypoints, 1–8 targets, altitude .8–1.2m, <=128 steps |
| FlightStateManager / FlightSafetyController | tello safety + recovery journal | only START_ACCEPTED grants one execution | VERIFIED; CONSUMED is not permission to retry takeoff |
| TelloController | tello UDP transport / receiver | command 8889, telemetry8890, video11111 | VERIFIED SDK documentation; physical transport NOT TESTED |
| GatewayWiFiManager | network checks + OS setup | stable interfaces, sensor192.168.4.1/24, Tello192.168.10.1 | PROPOSED Linux deployment; chipset/AP NOT TESTED |
| Heartbeat | backend heartbeat | POST `/api/device/gateways/{id}/heartbeat`, no body | VERIFIED |
| Telemetry | backend telemetry outbox | POST `/api/device/telemetry`, current DTO | VERIFIED; null GPS, do not fabricate coordinates |
| Mission video | video recorder + separate upload queue | optional `recordVideo=false`; proposed resumable media API | MISSING in Backend/Web; PROPOSED mock contract, never call production by default |

## Source evidence and discrepancies

- BackEnd `AGENTS.md`, `API.md`, `ARCHITECTURE.md`, `BUSINESS_RULES.md`, controller and application sources inspected. Older architecture role tables contradict current AGENTS/authorization; device authentication remains independent of human roles.
- `DeviceMissionContracts.cs` has `id`, not `missionId`, and no recordVideo. `MissionLaunchService.cs` requires a 32-hex per-process session and current planRevision. Ready expires after30s; approval belongs to mission owner. Consume returns START_ACCEPTED once; lost response must inhibit flight.
- Older API examples describe `/start`; `MissionLaunchService` is authoritative for scheduled Tello launch. Rental eligibility can produce422 and must not be bypassed.
- `SyncContracts.cs` contains per-channel records, not raw sensor JSON. `SyncService.cs` compares payload hash on batch retry; changed payload with same key yields409. Only matched per-item ACCEPTED/DUPLICATE is durable server acknowledgment. Partial batches can retry unchanged.
- `SyncService` unresolved identity is deviceCode + positive uint32 sequence and exact recordId; archived duplicate compares original JSON plus missionId. Preserve the original payload and first collectedAt; never reassign a duplicate's mission.
- ESP6288 `GatewayClient.cpp` rejects chunked, missing/oversized Content-Length registration replies. Presence refresh is20s; timeout30s.
- ESP32 parsing is looser than ESP6288 in places (sequence0, unsynced timestamp); new validation follows the actual sensor producer contract. ACK may partially apply before503, so ACK retries must tolerate already-removed records.
- Video scans on BackEnd, FrontEnd and fe find no mission recording/upload/playback implementation. Existing public hero video is unrelated. Production media integration requires an agreed separate API and Web changes; mock testing does not establish deployed support.

## External primary references

- [Tello SDK2.0](https://dl-cdn.ryzerobotics.com/downloads/Tello/Tello%20SDK%202.0%20User%20Guide.pdf)
- [Raspberry Pi network configuration](https://www.raspberrypi.com/documentation/computers/configuration.html)
- [Raspberry Pi SSH](https://www.raspberrypi.com/documentation/computers/remote-access.html)
