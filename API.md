# API.md

## 1. API Standard

Base URL:

```text
/api
```

Protocol:

```text
HTTPS
```

Content type:

```text
application/json
```

Authentication:

```text
Human endpoints:
JWT Bearer Access Token

Gateway/device endpoints:
Device Authentication: X-Api-Key + X-Gateway-Code (both required; human JWTs are not device credentials)
```

Do not expose EF entities directly.

---

## 2. Standard Success Response

For simple operations:

```json
{
  "success": true,
  "data": {},
  "message": null
}
```

For collection/list APIs:

```json
{
  "success": true,
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 100,
    "totalPages": 5
  }
}
```

---

## 3. Standard Error Response

```json
{
  "success": false,
  "message": "Validation failed.",
  "errors": [
    {
      "field": "name",
      "message": "Name is required."
    }
  ],
  "traceId": "..."
}
```

---

## 4. Authentication

Login accepts only `email` and `password`; no role selection is accepted as part of authentication. Roles are loaded from the database and returned in the JWT, login response and `/api/auth/me`. Active users with correct passwords and any of the three supported roles can authenticate. Accounts with no supported role are denied login/refresh/me until an administrator assigns one.

CreateUser rejects `FarmAdministrator`, including mixed-role requests; it accepts `FarmOwner` and `FarmEngineer`. SetRoles retains the three supported role names. Both require unique roles and reject unknown or deprecated names. Existing email/password validation remains in effect.

### POST `/api/auth/login`

Request:

```json
{
  "email": "admin@example.com",
  "password": "..."
}
```

Response:

```json
{
  "success": true,
  "data": {
    "accessToken": "...",
    "refreshToken": "...",
    "expiresIn": 900,
    "user": {
      "id": 1,
      "fullName": "Admin",
      "email": "admin@example.com",
      "roles": ["FarmAdministrator"]
    }
  }
}
```

---

### POST `/api/auth/refresh`

Request:

```json
{
  "refreshToken": "..."
}
```

Behavior:

- verify token
- verify not expired/revoked
- rotate refresh token
- revoke old refresh token
- return new token pair

---

### POST `/api/auth/logout`

Revoke current refresh token/session.

Auth required.

### Password recovery and change

| Endpoint | Authentication | JSON body |
|---|---|---|
| POST `/api/auth/forgot-password` | Public | `{"email":"owner@example.com"}` |
| POST `/api/auth/reset-password` | Public | `{"token":"<token from email>","newPassword":"<12–1024 characters>"}` |
| POST `/api/auth/change-password` | Bearer JWT | `{"currentPassword":"<current password>","newPassword":"<12–1024 characters>"}` |

Successful responses use the standard `success/data/message` envelope with HTTP 200. Forgot-password queues the address without looking up the account or waiting for SMTP; it always returns the same message for eligible, unknown, inactive, throttled accounts and failed email delivery. It never returns the token. Invalid input, an invalid/expired/used reset token or an incorrect current password returns 400; missing/revoked authentication returns 401.

The background queue holds at most 100 addresses in memory. A server restart drops unprocessed requests; overload also drops requests with the same generic response. Users can request again if an email does not arrive. No raw reset token is stored in a queue/database/log. Delivery failures and queue overload emit generic operational warnings without account or token details.

Reset tokens are random, single-use, expire after 15 minutes and are delivered as a token in a plain-text email. A later eligible request replaces the previous token. At most one email is attempted per account per 60 seconds. No password is changed by requesting a token. SMTP must be configured using the existing `Email:Enabled`, `Host`, `Port`, `FromAddress`, `EnableSsl`, `Username`, `Password` settings; `TimeoutSeconds` bounds delivery. Disabled or unavailable SMTP does not expose account existence in the public response. Credentials belong in environment variables/user secrets.

Successful reset/change revokes all existing refresh tokens, invalidates previously issued access JWTs, and invalidates outstanding reset tokens. The user must sign in again. Each authenticated request checks the JWT password version against the account. Pre-migration tokens are version zero and remain valid until a password change or their normal expiry.

`POST /api/users` returns 400 if `roles` includes `FarmAdministrator`, even alongside another role. Existing administrator accounts, administrator seeding and the separate role-update endpoint retain their existing behavior.

---

## 5. User Management

Role:

```text
FarmAdministrator
```

Endpoints:

```text
GET    /api/users
GET    /api/users/{id}
POST   /api/users
PUT    /api/users/{id}
PATCH  /api/users/{id}/status
PUT    /api/users/{id}/roles
```

---

## 6. Farm Management

### Farms

```text
GET    /api/farms
GET    /api/farms/{id}
POST   /api/farms
PUT    /api/farms/{id}
PATCH  /api/farms/{id}/status
```

### Farm members (FarmAdministrator only)

```text
GET    /api/farms/{farmId}/members?page=1&pageSize=20&search=...
POST   /api/farms/{farmId}/members
DELETE /api/farms/{farmId}/members/{userId}
```

POST body: `{ "userId": 3 }`. Returns 201 with the standard data envelope; GET returns a paginated envelope. Member data contains `userId`, `email`, `fullName`, `roles`, `assignedAt` (UTC), never password/token material. DELETE returns 204 and removes only the assignment. Duplicate assignment is 409; missing farm/user/assignment is 404; inactive, roleless or administrator target is 422. Non-admin callers get 403 and cannot assign themselves.

GET farms automatically scopes non-admin results. GET/PUT/status and zone endpoints authorize the relevant farm; an existing unassigned farm returns 403, nonexistent farm returns 404. Owner POST farms creates its assignment transactionally; Engineer POST remains forbidden.

### Zones

```text
GET    /api/farms/{farmId}/zones
POST   /api/farms/{farmId}/zones
GET    /api/zones/{id}
PUT    /api/zones/{id}
```

---

## 7. Sensor Management

Endpoints:

```text
GET    /api/sensor-nodes
GET    /api/sensor-nodes/{id}
POST   /api/sensor-nodes
PUT    /api/sensor-nodes/{id}
PATCH  /api/sensor-nodes/{id}/status

GET    /api/sensor-types
POST   /api/sensor-types

GET    /api/sensor-nodes/{sensorNodeId}/channels
POST   /api/sensor-nodes/{sensorNodeId}/channels
PUT    /api/sensor-channels/{id}

GET    /api/sensor-channels/{id}/threshold
PUT    /api/sensor-channels/{id}/threshold
```

---

## 8. Sensor Data

### GET `/api/sensor-readings`

Suggested query:

```text
sensorNodeId
sensorChannelId
farmId
zoneId
missionId
from
to
page
pageSize
```

Example:

```text
GET /api/sensor-readings?sensorChannelId=12&from=2026-09-01T00:00:00Z&to=2026-09-18T00:00:00Z
```

---

### GET `/api/sensor-nodes/{id}/latest`

Returns latest value for every channel on the node.

---

### GET `/api/sensor-channels/{id}/history`

Returns historical readings.

---

### GET `/api/zones/compare`

Example:

```text
GET /api/zones/compare?zoneIds=1,2&sensorTypeId=3&from=...&to=...
```

Supports the requirement to compare environmental conditions between zones.

---

## 9. UAV Management

```text
GET    /api/uavs
GET    /api/uavs/{id}
POST   /api/uavs
PUT    /api/uavs/{id}
PATCH  /api/uavs/{id}/status
```

---

## 10. Gateway Management

```text
GET    /api/gateways
GET    /api/gateways/{id}
POST   /api/gateways
PUT    /api/gateways/{id}
PATCH  /api/gateways/{id}/status
POST   /api/gateways/{id}/assign-uav
```

---

## 11. Mission APIs

### GET `/api/missions`

Filters:

```text
farmId
uavId
gatewayId
status
scheduledFrom
scheduledTo
page
pageSize
```

---

### GET `/api/missions/{id}`

Returns:

- mission info
- farm
- assigned UAV
- assigned gateway
- targets
- waypoints
- current status
- progress summary

---

### POST `/api/missions`

Role:

```text
FarmAdministrator or FarmOwner (ManageMissions)
```

Example request:

```json
{
  "name": "Morning collection - Farm A",
  "farmId": 1,
  "uavId": 2,
  "gatewayId": 3,
  "scheduledStartAt": "2026-09-20T01:00:00Z",
  "sensorNodeIds": [10, 11, 12],
  "targetAssignments": [
    { "sensorNodeId": 10, "waypointSequenceNo": 1, "sequenceNo": 1 },
    { "sensorNodeId": 11, "waypointSequenceNo": 2, "sequenceNo": 2 },
    { "sensorNodeId": 12, "waypointSequenceNo": 2, "sequenceNo": 3 }
  ],
  "waypoints": [
    {
      "sequenceNo": 1,
      "latitude": 10.1234567,
      "longitude": 106.1234567,
      "altitudeM": 10
    },
    {
      "sequenceNo": 2,
      "latitude": 10.1235000,
      "longitude": 106.1236000,
      "altitudeM": 10
    }
  ]
}
```

Rules:

- farm must exist
- selected sensors must belong to the selected farm
- assigned UAV/gateway must be active
- waypoint sequence must be unique
- scheduled time cannot violate system scheduling rules

---

### PUT `/api/missions/{id}`

Allowed only before mission starts.

Do not allow editing completed/failed/cancelled mission structure.

---

### POST `/api/missions/{id}/schedule`

Request:

```json
{
  "scheduledStartAt": "2026-09-20T01:00:00Z"
}
```

Moves mission to `SCHEDULED` when valid.

---

### POST `/api/missions/{id}/start`

Moves valid mission to:

```text
RUNNING
```

The endpoint records tracking status. It does not directly send low-level flight-control commands unless a later project phase explicitly adds such an integration.

---

### POST `/api/missions/{id}/complete`

Request:

```json
{
  "operatorNotes": "Mission completed successfully."
}
```

---

### POST `/api/missions/{id}/fail`

```json
{
  "failureReason": "Gateway lost communication.",
  "operatorNotes": "Returned UAV manually."
}
```

---

### POST `/api/missions/{id}/cancel`

Valid only when mission is not terminal.

---

### PATCH `/api/missions/{id}/status`

Use only if manual status update is required by operator workflow.

Request:

```json
{
  "status": "RUNNING",
  "note": "Mission started manually."
}
```

Business rules still validate transition.

---

### GET `/api/missions/{id}/results`

Returns:

```json
{
  "success": true,
  "data": {
    "missionId": 100,
    "status": "COMPLETED",
    "totalTargets": 5,
    "successfulTargets": 4,
    "failedTargets": 1,
    "targets": []
  }
}
```

---

### GET `/api/missions/{id}/waypoints`

Returns route/waypoint plan.

---

### GET `/api/missions/{id}/logs`

Returns operational history.

---

## 12. Collection Attempt APIs

```text
GET /api/missions/{missionId}/collection-attempts
```

Gateway/internal API may create attempt data through sync payload rather than a public human endpoint.

---

## 13. Telemetry APIs

### POST `/api/device/telemetry`

Device-authenticated.

Example:

```json
{
  "missionId": 100,
  "uavId": 2,
  "gatewayId": 3,
  "recordedAt": "2026-09-20T01:05:00Z",
  "latitude": 10.1234567,
  "longitude": 106.1234567,
  "altitudeM": 12.4,
  "batteryPercent": 74,
  "currentWaypointNo": 2,
  "flightStatus": "AUTO"
}
```

Telemetry is optional and stored when available.

---

### GET `/api/missions/{id}/telemetry/latest`

Returns latest mission telemetry.

---

### GET `/api/missions/{id}/telemetry`

Returns historical telemetry.

---

## 14. Gateway Offline Sync API

### POST `/api/device/gateways/{gatewayId}/sync`

Device-authenticated.

Example request:

```json
{
  "batchKey": "gw3-20260920-000001",
  "missionId": 100,
  "records": [
    {
      "sensorNodeCode": "SN-001",
      "channelCode": "temperature",
      "sourceRecordKey": "SN-001-temperature-1726794300",
      "value": 31.5,
      "measuredAt": "2026-09-20T01:04:00Z",
      "collectedAt": "2026-09-20T01:05:10Z"
    }
  ],
  "collectionResults": [
    {
      "sensorNodeId": 10,
      "status": "SUCCESS",
      "attemptNo": 1,
      "recordsReceived": 1
    }
  ]
}
```

Server behavior:

1. authenticate gateway
2. verify batch
3. detect already-processed `batchKey`
4. validate each record
5. prevent duplicates using `sourceRecordKey`
6. save accepted readings
7. save collection results
8. evaluate alerts
9. return per-record outcome

Example response:

```json
{
  "success": true,
  "data": {
    "batchKey": "gw3-20260920-000001",
    "accepted": 1,
    "duplicates": 0,
    "rejected": 0,
    "records": [
      {
        "sourceRecordKey": "SN-001-temperature-1726794300",
        "status": "ACCEPTED",
        "error": null
      }
    ]
  }
}
```

The endpoint MUST be idempotent.

---

## 15. Alerts

### GET `/api/alerts`

Filters:

```text
status
severity
alertType
farmId
zoneId
sensorNodeId
gatewayId
missionId
from
to
```

---

### GET `/api/alerts/{id}`

Returns alert plus handling history.

---

### POST `/api/alerts/{id}/acknowledge`

```json
{
  "note": "Operator is checking the sensor."
}
```

---

### POST `/api/alerts/{id}/notes`

Adds handling note without closing alert.

---

### POST `/api/alerts/{id}/close`

```json
{
  "note": "Issue resolved."
}
```

---

## 16. Notification APIs

```text
GET   /api/notifications
PATCH /api/notifications/{id}/read
```

Email sending is backend-driven based on alert/notification rules.

---

## 17. Dashboard

### GET `/api/dashboard/overview`

Example response data:

```json
{
  "farms": 1,
  "sensorNodes": {
    "total": 20,
    "online": 18,
    "offline": 2
  },
  "gateways": {
    "total": 2,
    "online": 1,
    "offline": 1
  },
  "missions": {
    "scheduled": 2,
    "running": 1,
    "failed": 0
  },
  "alerts": {
    "open": 3,
    "critical": 1
  }
}
```

---

## 18. Reports

Suggested endpoints:

```text
GET /api/reports/sensor-data
GET /api/reports/devices
GET /api/reports/alerts
GET /api/reports/missions
```

Possible formats:

```text
JSON initially
CSV optional
PDF optional if required by report deliverables
```

---

## 19. HTTP Status Code Rules

```text
200 OK
201 Created
204 No Content

400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity

500 Internal Server Error
```

Examples:

```text
409:
duplicate device code
invalid current-state conflict
sync batch already processed with conflicting payload

422:
valid JSON but business rule failed
```

---

## 20. Authorization Matrix

| Policy | FarmAdministrator | FarmOwner | FarmEngineer |
|---|---|---|---|
| ManageUsers | Yes | No | No |
| ManageFarms | Yes | Yes | No |
| ReadFarmData | Yes | Yes | Yes |
| ManageDevices | Yes | Yes | No |
| ConfigureThresholds | Yes | No | Yes |
| ManageMissions | Yes | Yes | No |

Human users authenticate with JWT Bearer. Gateway/device endpoints use independent Device Authentication with both `X-Api-Key` and `X-Gateway-Code`; human JWTs never substitute for device credentials. Device credentials do not grant access to human endpoints.

All three human roles may read farms/zones, sensor data/history, alerts, dashboard and reports. Only FarmAdministrator manages users. FarmAdministrator/FarmOwner manage farms/zones, device metadata and missions. FarmAdministrator/FarmEngineer configure thresholds. Alert handling also requires farm access; private notification APIs retain recipient ownership checks.

FarmAdministrator has global farm access without membership. FarmOwner and FarmEngineer require a current `user_farms` assignment to access a farm, in addition to the existing role policy. Roles still come exclusively from `user_roles`/`roles`.

UAV Operator may describe an external/use-case actor, but is NOT an authenticated backend user role and cannot receive JWTs as an operator. Historical mission fields such as `operatorNotes` describe operational notes, not authorization roles.

---

## 21. API Base Route Rule

All public endpoints must start at:

```text
/api
```

Do NOT add a version segment such as:

```text
/api/v1
/api/v2
```

unless the project explicitly decides to introduce API versioning later.

Current examples:

```text
/api/auth/login
/api/farms
/api/sensor-nodes
/api/missions
/api/alerts
```


## Farm-level access control

ROLE AUTHORIZATION answers "What is the user allowed to do?" FARM AUTHORIZATION answers "Which farm is the user allowed to do it on?" Both checks apply to human farm-scoped endpoints; the six-role-policy matrix above remains unchanged.

- FarmAdministrator has global access, with no `user_farms` row required.
- FarmOwner and FarmEngineer access assigned farms only. No assignments means empty scoped lists and aggregates; explicit existing resources outside access return 403.
- Only FarmAdministrator manages assignments through GET/POST `/api/farms/{farmId}/members` and DELETE `/api/farms/{farmId}/members/{userId}`.
- Assignment requires an existing active FarmOwner/FarmEngineer user. Administrators (including mixed admin/owner roles) cannot be assigned. Assigning/removing membership never changes global roles.
- Owner-created farms and the creator's assignment commit in one transaction. Administrator-created farms need no assignment. Existing farms/users receive no automatic assignments during migration.
- Current database roles and memberships are checked per operation. Removing a membership immediately denies subsequent requests using the same JWT. Stale memberships cannot grant access without a permitted current role.
- Farms, zones, nodes/channels, readings/history/latest/comparison, thresholds, missions and their child data/telemetry, alerts and farm-derived dashboard/report aggregates are scoped. Lists are filtered in SQL before paging/counting/aggregation. Every explicit farm/resource filter is authorized.
- Alerts resolve their farm through SensorChannel -> SensorNode -> Zone -> Farm, otherwise SensorNode -> Zone -> Farm, otherwise Mission -> Farm. Alerts without such an association are administrator-only. Gateway/UAV identity alone does not establish a farm.
- New alert notifications target administrators and eligible members of the resolved farm. Notification APIs retain user-specific ownership checks, including historical notifications after membership removal; they never expose another user's notification.
- Historical mission target IDs, outcome/status and sequence remain part of mission history. If a target sensor has since moved outside the viewer's accessible farms, its current `sensorName` is null rather than exposing live metadata from another farm.

UAV/Gateway entities are not directly farm-owned in the current schema. Direct per-farm device authorization requires a future explicit device-to-farm assignment model.

Direct UAV/Gateway catalog APIs retain existing global role permissions. Farm-scoped device reports/dashboard counts include only equipment referenced by missions in accessible farms (or the requested authorized farm); this is a mission association, not device ownership. An administrator's unfiltered aggregate remains global. Sensor-type definitions are a global catalog.

Gateway/device authentication remains X-Api-Key + X-Gateway-Code. `user_farms` is only for human users and is never applied to device identity or required for telemetry/sync ingestion. Human telemetry reads require mission-farm access.

Response conventions: 401 without valid authentication; 403 for denied role/farm access; 404 for nonexistent resources; 409 for duplicate membership; 400 for invalid request data; 422 for ineligible membership targets.

### Farm filters on aggregates

`GET /api/dashboard/overview` and `GET /api/reports/devices` also accept optional `farmId`. Existing sensor/mission/alert reports retain their farm/date/pagination filters. Explicit unauthorized farmId returns 403; absent farmId aggregates only assigned farms for Owner/Engineer. Device reports associate equipment via missions, not a new equipment ownership column.

## Device mission execution

Web users create and schedule through the existing human mission endpoints.
`MissionRequest.targetAssignments` contains `sensorNodeId`, `waypointSequenceNo`
and `sequenceNo`. IDs and sequences must be positive; sensor IDs and target
sequences must be unique. The waypoint sequence must exist in the submitted
waypoints. Multiple different sensors may share a waypoint.

`sensorNodeIds` is deprecated for scheduled plans, but remains supported for
legacy drafts. It may be omitted when assignments are supplied. If both fields
are supplied, their sensor ID sets must match exactly (including empty arrays).
Scheduling/starting requires every target to be mapped to this mission's
waypoint, and active sensors in the mission farm. Old unmapped drafts/scheduled
plans must be updated with explicit assignments before scheduling/starting.
Mappings are stored using the existing `MissionTarget.WaypointId`; nothing is
inferred from array position, sensor ID or coordinates. PUT replaces the plan,
including mappings, transactionally.

All endpoints below require **both** `X-Gateway-Code` and `X-Api-Key` through the
existing DeviceKey authentication scheme. Human JWT alone returns 401. Route
gateway ID must equal the authenticated `gateway_id` claim; the mission must
belong to that gateway. Mismatches return 403. Missing missions return 404.
Inactive/unrecognized gateway credentials return 401 at authentication; a
gateway deactivated during a write is rejected by the service with 403.

Base route: `/api/device/gateways/{gatewayId}/missions`

| Method and suffix | Behavior |
| --- | --- |
| GET `next` | At most one assigned SCHEDULED mission, ordered by scheduledStartAt then id ascending; no due-time filter. HTTP 200 with data=null if absent. |
| POST `{missionId}/downloaded` | Acknowledge only after gateway validation, verified local persistence. Requires SCHEDULED; status remains SCHEDULED. One gateway-origin DOWNLOADED log, even on concurrent retries. |
| POST `{missionId}/start` | SCHEDULED -> RUNNING, sets startedAt; shared resource, mapping, schedule and overlap checks. Already RUNNING returns success without changing timestamps/logs. |
| POST `{missionId}/complete` | RUNNING -> COMPLETED only with nonempty targets and no PENDING target; sets completedAt. Already COMPLETED is a successful retry. |
| POST `{missionId}/fail` | RUNNING -> FAILED; required nonblank failureReason, optional note. Sets failureReason/completedAt, raises existing MISSION_ERROR alert. Already FAILED succeeds only with the same trimmed reason; different reason is 409. |
| POST `{missionId}/cancel` | Existing PENDING/SCHEDULED/RUNNING -> CANCELLED semantics; optional reason. Already CANCELLED succeeds without rewriting original reason/log. Reports a locally handled physical abort, never controls motors. |

Downloaded/start/complete require no JSON body. Fail body:
`{"failureReason":"SENSOR_TIMEOUT","note":"Sensor SENSOR-002 was not discovered."}`.
Cancel body: `{"reason":"Operator aborted mission."}` (or `{}`).
Action text is bounded to 10,000 characters by existing action validation.
Invalid input is 400, invalid transition/incompatible retry is 409, and failed
resource/target-completion business rules are 422. Retrying a different terminal
action cannot reopen or overwrite a terminal mission.

Next response (only device-facing plan data; DeviceCode comes from SensorNode):

```json
{
  "success": true,
  "data": {
    "id": 15, "name": "FARM-MISSION-001", "status": "SCHEDULED",
    "gatewayId": 1, "uavId": 1,
    "scheduledStartAt": "2026-10-09T01:00:00Z",
    "scheduledEndAt": "2026-10-09T01:30:00Z",
    "waypoints": [{"id":100,"sequenceNo":1,"latitude":null,"longitude":null,
      "localX":0.5,"localY":0,"altitudeM":0.35,"actionType":"COLLECT","plannedHoldSeconds":10}],
    "targets": [{"id":22,"sensorNodeId":5,"deviceCode":"SENSOR-001",
      "waypointId":100,"waypointSequenceNo":1,"sequenceNo":1,"status":"PENDING"}]
  },
  "message": null
}
```

Action responses are HTTP 200 with standard envelope:

```json
{"success":true,"data":{"id":15,"status":"RUNNING","startedAt":"2026-10-09T01:00:00Z","completedAt":null,"failureReason":null},"message":null}
```

Required gateway workflow: pull -> validate/persist/verify -> downloaded ACK ->
local START -> await backend start success -> switch STA to Tello -> execute
offline -> safely land -> reconnect Internet -> existing `/sync` with missionId,
readings and collectionResults -> inspect per-item outcomes/retry failures ->
complete/fail/cancel. Download is not flight authorization. Backend cannot verify
LittleFS persistence or physical landing; those are gateway responsibilities.

Both GPS and local coordinates remain supported. AltitudeM is checked for
nonnegative supported storage range; no backend 0.40 m ceiling exists. ESP32
enforces its configurable ceiling and rejects plans incompatible with Tello.
Device responses never expose API keys, hashes, Wi-Fi secrets or human JWTs.

### PowerShell examples

Use a provisioned gateway and mission ID. The create/schedule request is sent
separately using a human account. Execute each next step only after the prior
response and corresponding local persistence/flight/sync operation succeed.

```powershell
$base = 'https://localhost:5001/api'
$gatewayId = 1
$missionId = 15
$headers = @{ 'X-Gateway-Code' = 'GATEWAY-001'; 'X-Api-Key' = $env:GATEWAY_API_KEY }
$root = "$base/device/gateways/$gatewayId/missions"
Invoke-RestMethod "$root/next" -Headers $headers
# After verified local persistence:
Invoke-RestMethod "$root/$missionId/downloaded" -Method Post -Headers $headers
# After explicit local START, while still online:
Invoke-RestMethod "$root/$missionId/start" -Method Post -Headers $headers
# After local flight, safe landing and reconnection; sync.json uses existing contract:
Invoke-RestMethod "$base/device/gateways/$gatewayId/sync" -Method Post -Headers $headers -ContentType 'application/json' -InFile .\sync.json
# Choose one terminal outcome after processing sync acknowledgements:
Invoke-RestMethod "$root/$missionId/complete" -Method Post -Headers $headers
# Alternatives to complete:
# Invoke-RestMethod "$root/$missionId/fail" -Method Post -Headers $headers -ContentType 'application/json' -Body '{"failureReason":"SENSOR_TIMEOUT","note":"Sensor unavailable"}'
# Invoke-RestMethod "$root/$missionId/cancel" -Method Post -Headers $headers -ContentType 'application/json' -Body '{"reason":"Operator aborted mission."}'
```

There is no generic `/result` endpoint or fingerprint acknowledgement in this
contract. Existing ESP32 firmware needs a subsequent integration update to use
the envelope, downloaded/start gates, mission-associated sync and these terminal
endpoints. This backend task does not change firmware or validate physical flight.

## Gateway credential management

All four routes require JWT Bearer and the existing **ManageDevices** policy (FarmAdministrator or FarmOwner). Responses use the usual `{ data: ... }` envelope and `Cache-Control: no-store`. DeviceKey credentials cannot call these human routes.

| Method | Route | Result |
|---|---|---|
| POST | /api/gateways/{gatewayId}/credential | 201, one-time credential |
| POST | /api/gateways/{gatewayId}/credential/rotate | 200, one-time replacement credential |
| DELETE | /api/gateways/{gatewayId}/credential | 204, idempotent soft revocation |
| GET | /api/gateways/{gatewayId}/credential/status | 200, metadata only |

No request body is required. Issuance returns `{ gatewayId, gatewayCode, apiKey, createdAt }`. The raw `apiKey` appears only in this create/rotate response and must be copied immediately. There is no key-retrieval endpoint. Status returns `{ gatewayId, gatewayCode, hasCredential, isActive, createdAt, lastUsedAt, revokedAt, keyPrefix }`; dates/prefix are null before first issuance. `hasCredential` includes revoked history; `isActive` also requires an active gateway. Status never contains `apiKey` or `keyHash`.

Missing gateway: 404. Create/rotate on inactive gateway: 422. Create while a credential is active: 409. Rotate without an active credential: 409 (create instead). Revoke works on inactive gateways and is idempotent. Creating again after revocation is supported. Concurrent changes are serialized and cannot leave multiple active credentials.

Device authentication still uses `X-Gateway-Code` and `X-Api-Key` with the **DeviceKey** scheme for all existing `/api/device/...` routes. It now reads only database credentials; old `DeviceAuthentication:Credentials` configuration no longer authenticates. Use the management API/UI to provision each physical gateway after migration. Successful authentication updates `lastUsedAt`; invalid keys do not. Revocation/rotation takes effect for subsequent authentication immediately, with no cached key/configuration fallback. Human JWT alone never substitutes for the two device headers.

ESP32 sync request (body must follow the existing sync contract):

```http
POST /api/device/gateways/{gatewayId}/sync
X-Gateway-Code: GW-ESP32-01
X-Api-Key: <one-time generated API key>
Content-Type: application/json
```
# Admin gateway/UAV flight tests

`POST /api/gateways/{gatewayId}/flight-tests` accepts `{ "requestKey": "unique-client-key" }`
and returns HTTP 202 with the normal `success/data` envelope. Only a current active
`FarmAdministrator` may create or read tests. `GET /api/gateways/{gatewayId}/flight-tests/{id}`
returns status and connection/flight evidence. Repeating a request key for that gateway
returns the original test, including after completion/expiry; use a new key for a new test.
The gateway must have an assigned active UAV. Equipment reserved by a scheduled/running
mission or another active flight test cannot be used. Target100cm and hold3000ms are fixed.

Device-only routes require `X-Gateway-Code` and `X-Api-Key`, with route identity matching
the authenticated gateway. Human JWTs cannot substitute:

- `GET /api/device/gateways/{gatewayId}/flight-tests/next`: data is null or a queued command.
- `POST /api/device/gateways/{gatewayId}/flight-tests/{id}/claim`: claims an unexpired command.
- `POST /api/device/gateways/{gatewayId}/flight-tests/{id}/result`: reports
  `{status,gatewayConnected,uavConnected,landed,takeoffAttempted,observedAltitudeCm,heldMs,errorCode}`.

Command/response data includes `id,gatewayId,uavId,status,altitudeCm,holdMs,expiresAt` and
nullable result fields. Results are idempotent only for identical evidence. COMPLETED
requires takeoffAttempted, both connections, landed=true, altitude97..103cm, heldMs>=3000,
and errorCode=null. FAILED after a takeoff requires confirmed landing; otherwise report
RECOVERY_REQUIRED. A pre-takeoff interrupted claim may report FAILED with takeoffAttempted=false,
zero altitude/hold and an error code even if the claim did not reach the backend.
Queued commands expire after120seconds. Claimed/recovery commands never auto-requeue or
auto-release reservations. See [setup and hardware boundaries](docs/FLIGHT_TEST_RUNBOOK.md).

## Geographic Farm and Zone boundaries

FarmRequest/ZoneRequest and their responses accept a nullable `boundary` GeoJSON object:
`{"type":"Polygon","coordinates":[[[106,10],[107,10],[107,11],[106,11],[106,10]]]}`.
Coordinates use longitude then latitude. One exterior ring is supported; holes and non-Polygon geometries are rejected. An unclosed input ring is closed before storage. At least three distinct vertices, valid coordinate ranges, non-zero area and no self-intersections are required (400 on invalid input).

POST/PUT and list/detail responses retain existing center fields. When boundary is supplied the server calculates the vertex centroid (same as Turf.centroid), overriding supplied center values. Null/omitted boundary retains the legacy center-only representation; PUT with null/omitted boundary clears a prior boundary. The frontend always sends its current boundary explicitly.

A Zone with a boundary must be covered by its parent Farm boundary, including shared edges. Without a parent boundary, containment cannot be checked. Updating a Farm boundary also validates existing Zone boundaries. Existing role and farm-access policies are unchanged.
# Tello mission compatibility

Mission schedule and start operations apply the ESP32 `mission_flight` contract when the assigned UAV's `model` contains `Tello` (case insensitive). Draft plans may remain incomplete. Unsupported ready plans return 422:

- 1–16 waypoints and 1–8 explicitly mapped targets.
- `localX` and `localY` are required meters relative to the takeoff origin, within ±100m, at whole-centimeter precision. GPS-only plans are rejected; no GPS conversion is performed.
- `altitudeM` is required, 0.8–1.2m with whole-centimeter precision; nominal takeoff/mission altitude is 1m.
- Actions are normalized to `MOVE`, `HOLD`, or `COLLECT`; `WAIT` and `COLLECT_SENSOR` are accepted aliases. Missing action defaults to `MOVE`. `plannedHoldSeconds` is 0–30, default 0.
- Targets must map to `COLLECT` waypoints, and each `COLLECT` waypoint must have at least one target.
- Each nonzero X/Y axis displacement must be at least 0.2m. The route includes automatic return to local origin at the final waypoint altitude. The 128-step capacity includes one altitude step and one waypoint marker per waypoint plus all outbound/return axis movements, split into segments up to 1m (short remainders are redistributed).

Device `POST /api/device/gateways/{gatewayId}/missions/{id}/start` requires an existing `DOWNLOADED` acknowledgment for Tello missions; otherwise it returns 409. A repeat `/downloaded` during `RUNNING` succeeds only when the same gateway already has a download log, returns the current state and creates no log. Terminal missions reject `/downloaded`. Existing response envelopes and generic UAV planning behavior remain unchanged.

Human `/api/missions/{id}/start` returns 409 for Tello missions. Start locally on
the assigned gateway so it can acknowledge RUNNING before switching Wi-Fi.
Changing status manually would otherwise remove the mission from the scheduled
download queue without commanding the aircraft.
