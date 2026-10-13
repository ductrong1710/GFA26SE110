# Central mock domain data

Frontend-only, deterministic snapshot at `MOCK_NOW` (2026-10-06 08:18 Vietnam / 01:18 UTC). No API calls, timers, random IDs, or database changes. Treat exported records as read-only; keep editable demo state separately.

Import datasets and selectors from `src/data/mock/index.js`. Integer IDs are identities; codes such as `NODE_001` and `MSN-2026-001` are display identifiers. `mockFarms` remains an alias of `farms` for the existing shell.

- Farms own zones; zones own nodes; nodes expose channels linked to sensor types. There are 12 Green Valley nodes and 3 Highland nodes, with three channels each.
- Readings refer to channels, with optional gateway, mission, and collection attempt IDs. Twelve hourly history samples per channel support trends. Mission samples come from successful collection attempts. Units and precision live on sensor types.
- A reading with `receivedAt: null` is still buffered on a gateway. Reading selectors exclude these by default; pass `{ includeUnsynced: true }` to preview local collection. Invalid sync payloads have no canonical reading. Duplicate sync results reference the existing reading and source key.
- Missions link farms, UAVs, gateways, and users. Targets link nodes and waypoints; attempts preserve failures and retries. Progress is derived from targets/waypoints, not copied into each page. The active mission is battery 72%, waypoints 6/10, targets 8/12.
- Sync batches contain per-record outcomes. Their counters are derived from those outcomes; pending includes processing. A collection success does not imply an upload success.
- Alerts retain relevant entity IDs, triggering values, and lifecycle history. Gateway errors always retain `gatewayId`. Notifications reference alerts and recipient users. Operator alert filtering uses assignment relationships; this is a demo, not server authorization.
- Domain users contain no credentials. `accounts.js` derives the existing four login fixtures from them and keeps the existing demo passwords. User 5 demonstrates multiple role assignments.

```js
import { ACTIVE_MISSION_ID, getMissionDetails, getSensorNodeDetails } from './data/mock/index.js'

const mission = getMissionDetails(ACTIVE_MISSION_ID)
const node = getSensorNodeDetails(5)
// Unknown entity IDs return null; collection selectors return empty arrays.
```

Mission `READY`, `IN_PROGRESS`, `COMPLETED`, `PARTIAL`, and `FAILED` are frontend demo display states, not a replacement for the backend transition enum. No backend mapping is assumed. Alert lifecycle and severity are separate fields. Thresholds are illustrative fixture values, not agronomic recommendations; light readings follow a day/night pattern, so future threshold evaluation should account for daylight windows. Historical exports and imported samples are deliberately downsampled; they are not a complete five-minute archive.

Run `npm run test:mock-data` to validate relationships, counts, timestamps, retry/sync semantics, and selector behavior.

`missionPlanning.js` extends the shared in-memory operations state with DRAFT,
SCHEDULED, and CANCELLED examples. New READY plans normalize targets and waypoints
with stable integer IDs. Draft plans retain incomplete configuration separately.
Use `missionPlanProgress(mission, operations)` for current planning state rather
than the immutable historical fixture selectors. The operator and administrator
dashboards read the same mission collection as the list. Grouping, availability,
coordinate conversion, and final validation live here rather than inside pages.

`missionMonitoring.js` derives attempt counts, collected records, retry errors,
elapsed time, and timeline events from the linked records. `missionTelemetry` is a
fixed, mission-scoped recorded position for the active fixture, not a live stream.
Local notes/status reports append `missionEvents` with actor and timestamp.
Collection TIMEOUT labels retain the original FAILED/SENSOR_TIMEOUT evidence.
Successful attempts / finished attempts defines the displayed success rate.
UAV_002 has telemetry support disabled in operational state to exercise the
no-telemetry view; unreported positions and battery values remain unavailable.

`syncState.js` extends the batch fixtures with an interrupted retransmission example
and keeps local retry history and accepted receipts in the shared operations state.
Batch 6 references the same canonical readings/source keys as part of batch 2.
Queued copies can outlive another batch's acknowledgement; only unique valid source
keys create mock receipts. Rejected payloads never become accepted through retry.

`sensorAnalytics.js` centralizes enriched readings, Vietnam-time filters, hourly
channel means, latest-reading aggregates, sensor availability, and zone comparisons.
Synchronized data is the default; operator technical views may explicitly include
buffered measurements. It overlays local sync receipts without mutating fixtures.
Missing coverage stays null; stale/offline channels are excluded from current KPI
averages but remain visible in historical analysis. Thresholds come from channels.

`reportSelectors.js` produces detached, role-scoped previews from current shared
operations and management state. Report definitions explain the date field and
aggregation used by each type. `reportCsv.js` exports only the columns represented
by that preview, checks the active role, and quotes/neutralizes spreadsheet text.
