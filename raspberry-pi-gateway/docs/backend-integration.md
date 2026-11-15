# Backend integration

Audited BackEnd commit `4f4f80d4e07cbd008fcdf5fd009f381937b5522f`. Every request uses `X-Gateway-Code` and `X-Api-Key`. Set the HTTPS origin without `/api` in configuration. Device route gateway ID must match the authenticated credential. Responses require `success=true` and `data`, including HTTP201 telemetry.

| Operation | Method and path | Payload / handling |
|---|---|---|
| Presence | POST `/api/device/gateways/{id}/heartbeat` | Bodyless, verify echoed ID and ONLINE |
| Next plan | GET `/api/device/gateways/{id}/missions/next` | Null or plan with `id`, gateway/uav IDs, planRevision, waypoints, targets |
| Downloaded | POST `/api/device/gateways/{id}/missions/{mission}/downloaded` | Commit validated plan first |
| Ready | POST `.../{mission}/launch/ready` | 32-hex boot session, planRevision |
| Consume | POST `.../{mission}/launch/consume` | Same session/revision, require START_ACCEPTED once |
| Complete | POST `.../{mission}/complete` | Collection results acknowledged first |
| Fail | POST `.../{mission}/fail` | failureReason, nullable note |
| Cancel | POST `.../{mission}/cancel` | nullable reason, terminal retry persisted |
| Sync | POST `/api/device/gateways/{id}/sync` | batchKey, missionId, records, collectionResults, unresolvedRecords |
| Telemetry | POST `/api/device/telemetry` | missionId, uavId, gatewayId, recordedAt, nullable GPS/local coordinates, altitudeM, batteryPercent, currentWaypointNo, flightStatus |

The legacy `/start` is excluded from the scheduled launch workflow. A 409 with exact `PLAN_CHANGED_DOWNLOAD_AGAIN` causes a fresh download. `CONSUMED` after a lost consume response requires operator reconciliation. Backend owner approval and rental eligibility remain authoritative.

Sensor numeric fields map to identically named backend channel codes. Each non-null value creates a reading with `sensorNodeCode`, `channelCode`, `sourceRecordKey`, `value`, explicit UTC `measuredAt`, first `collectedAt`, and `qualityStatus=VALID`. A sourceRecordKey repeats across the channels of one physical measurement. Unknown time, all-null measurements or measured time after collection use an unresolved record containing the original JSON. Backend `receivedAt` is assigned server-side.

The outbox freezes a batch of at most100 measurements and100 attempts within one mission scope. Backend record results must match batchKey, index, sourceRecordKey or sensorNodeId/attemptNo. Only ACCEPTED and DUPLICATE advance delivery. Partial batches retry unchanged. A malformed HTTP200 response retains the batch. Collection statuses use SUCCESS, FAILED, TIMEOUT or SKIPPED.

401/403 require credential/authorization correction. 400/404/409/422 hold the immutable request for review. 429 respects numeric Retry-After up to one hour. Network errors and server failures retry with exponential delay from5 to300 seconds. No automatic retry is applied to launch consume or takeoff. Headers and response bodies are omitted from logs.

Mission results are separate from video completion. Sensor/results run before media on each service tick. A video failure retains its file/session and leaves acknowledged sensor flags intact. Telemetry uncertain delivery is retained for explicit reconciliation because the current API always inserts a new row.

Operator reconciliation after confirmed landing reports `CANCELLED`: an ambiguous consume may leave the Backend mission either SCHEDULED or RUNNING, and cancellation is legal from both. Its durable cancellation acknowledgment precedes sensor/collection sync because SCHEDULED missions reject sync. Normal COMPLETED/FAILED results still wait for their sensor and collection outcomes. Unrelated offline data continues syncing while cancellation is pending.

Run the loopback demonstration with `python -m mocks.demo`. Existing .NET contract tests can be run on a disposable PostgreSQL cluster using the repository's `scripts/Test-Postgres.ps1`. Passing those tests verifies the upstream behavior exercised there. It does not establish that a deployed remote server runs the audited commit.
