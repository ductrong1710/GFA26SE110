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
