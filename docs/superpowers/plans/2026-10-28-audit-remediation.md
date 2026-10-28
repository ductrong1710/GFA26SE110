# Audit remediation F02–F11

User authorized all verified audit fixes except F01 (sample administrator provisioning).
Existing working-tree changes must be preserved. No commits, deployment, flashing, or migrations against existing databases.

## Backend
- Reject the published JWT key outside Development; retain explicit development setup.
- Resolve flight recovery only with confirmed landing and preserved original failure evidence.
- Reconsider rejected batch indexes without reprocessing accepted indexes.
- Archive raw measurements with unknown timestamps or no valid values, using a dedicated durable entity and farm-scoped read endpoint. Never invent measurement time.

## Gateway
- Authenticate flight/mission control requests using the existing local configuration authentication.
- Parse bounded HTTP conflict bodies so mission revision reload works.
- Send unresolved measurements to the archival contract and require per-record acknowledgements.
- Reclaim synced measurements only after a successful durable node ACK. Retain duplicate receipts when the node ACK is uncertain; release them after a successful retry. This bounds normal continuous operation without forgetting unacknowledged identities.
- Count pending data by mission for mission completion.
- Preserve recovery locks until telemetry confirms landing, including restored journals.

## Frontend
- Keep request token and displayed session identity consistent across tabs.
- Match backend alert enums and select the newest collection error explicitly.

## Verification
Regression tests must demonstrate each failure before implementation, then pass. Run affected host suites, backend unit tests/build, frontend tests/lint/build, and firmware compilation where dependencies are already installed. Database integration and physical flight remain separate verification gates.

## Verified result — 2026-10-28
- Backend: 105 unit tests passed; solution build passed without warnings or errors. EF reports no pending model changes. Archive migration generated but not applied.
- Frontend: Node and user suites passed; final credential/component suite passed 45 tests, including archived-record display/pagination. Lint and production build passed (existing bundle-size warning).
- ESP32: mission, flight-test, storage lifecycle, measurement adapter, bounded HTTP and control-auth host suites passed. Storage regression exercises 1,200 confirmed deliveries and retains evidence after uncertain ACK/write failure.
- Firmware builds passed for esp32dev, flight_test and mission_flight; no flashing or physical commands executed.
- Sample administrator migration unchanged; ESP8266 working tree clean. Database integration tests, including the added archive-route farm-scope cases, were compiled but not executed.
- Rollout requires a production JWT secret and the archive database migration before enabling the updated gateway archive flow. Old backends cannot acknowledge archived records; the gateway retains them until a compatible backend accepts them.
