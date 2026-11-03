# API.md

### Operational workflow revision — 2026-11-03 (supersedes older claim/paper/replacement descriptions)

See `BE_FLOW_CHANGES_2026-11-03.txt` for exact payloads and FE migration notes.
New authenticated `/api/operations` endpoints: `calendar`, `available-technicians`,
`dashboard`, `consultation-requests`, `device-history`,
`{installation|service}/{jobId}/installed-devices`, `service-jobs/{id}/plans` (GET/POST),
`service-plans/{id}/review` and `/execute` (POST). Calendar is Admin/global or
Technician/self; dashboard/history/availability/review are Admin-only. Leads also
permit active Engineers. Plans and installed equipment require current job context.
Periods are `[from,to)` and at most 366 days. Availability appointments max 24h.
Assignment payloads add optional `scheduledEndAt` (default start +2h), cross-type
conflicts include 30-minute travel buffer; Admin may reassign an eligible open job.

Homepage content adds `COMPONENT`, `replacementPriceVnd`, public `priceRevision`.
Proposal lines require a published rental `publicContentId`; responses retain price
revision/name/monthly-price snapshots. Engineer surveys no longer capture age or
installation position. The first recorded survey/follow-up can establish context
without a separate claim. The new lead list/dashboard expose no claim/shift KPI.

Installation reports require `paperwork.contract` and `.handover` image arrays;
optional `.bills`, `.paperContractNumber`, and `completedAt`. Paper approval does not
require owner online confirmation. Successful final approval creates a rental
atomically, starting midnight UTC+7 on the day after actual completion, ending after
the agreed months. Do not call create-rental again. The rental worker activates due
drafts. Detail adds `priceLines`, initial-period `totalRentVnd`, `paperworkJson`.

Device incidents accept a null assignment with required `farmId`; no serial/photo
required. Admin must identify a current assignment before creating its service job.
Immediate `/replacements` now rejects with 409. Create a plan (published price lines,
quantity, warranty reason and optional typed device target), Admin review, then
execute with signed paper agreement and old/new device images for a physical
exchange. Service reports require `signedHandover` and an EXECUTED plan. Final Admin
approval alone applies equipment changes and closes the job atomically. Plan actions
use the job's `expectedRevision`, not a separate plan revision. No online signature,
stock mutation, payment processing or accounting is introduced.

### Operational dashboard summary

`GET /api/dashboard/overview?farmId={id}` additionally returns `operations`: `openInstallations`, `openMaintenance`, `openRepairs`, `activeRentals`, `expiringRentals`, `expiredRentals`, `warningDays`, `activeSeasons`, and `recentCultivation` (up to five `{id, seasonId, seasonName, date, type, description}` entries ordered by activity date then ID). Existing overview fields are unchanged. Counts are computed in the same repeatable-read snapshot as device/alert/mission totals. Owner results require current farm membership and the linked owner identity; Admin can view all or filter to one farm. Expiring contracts use the configured `RentalExpiration:WarningDays`; dates and status are evaluated on the server. Missing cultivation data returns an empty array, never estimated activities or harvest values.

## Cancel a manually claimed, never-started flight test

Admin-only `POST /api/gateways/{gatewayId}/flight-tests/{id}/cancel` accepts
`{ "reason": "Manually claimed; never started", "confirmAircraftGrounded": true, "confirmNeverStarted": true }`.
The confirmations mean the aircraft is physically grounded and no firmware is
executing this test or has issued a takeoff command. This endpoint is not a landing command.
Reason is required, max 60 characters, with no control characters.
QUEUED, CLAIMED and EXPIRED tests without flight evidence can become CANCELLED.
Recovery and completed/failed results cannot be overwritten. Retrying CANCELLED
is idempotent. The response uses the existing flight-test envelope. Cancellation
releases reservations, preserves existing telemetry, and records administrator ID
and reason in `errorCode` (`ADMIN_CANCELLED:<userId>:<reason>`) with server `reportedAt`.

Reservation conflicts return HTTP 409 with a `message` identifying visible blocking
missions (ID, name, status, matched UAV/gateway IDs and planned window in UTC).
Active flight tests include ID/status for administrators and their assigned device.
Other users receive a generic flight-test reservation notice. Inaccessible-farm
mission details are redacted; reservations still block the operation. At most ten
mission and ten flight-test details are included, with an additional-reservations notice.

## Gateway heartbeat

`POST /api/device/gateways/{gatewayId}/heartbeat` requires `X-Gateway-Code` and
`X-Api-Key`. No request body is required. Returns HTTP 200 with
`{ "success": true, "data": { "gatewayId": 1, "status": "ONLINE", "lastSeenAt": "<UTC server time>" }, "message": null }`.
Missing/invalid/revoked credentials or inactive gateways return 401; a route ID
different from the authenticated gateway returns 403. Heartbeats remain allowed
while equipment is reserved; they never change assignments or flight state.

Gateway catalog/detail and device reports derive `status` from `isActive` and
`lastSeenAt`: ONLINE through 90 seconds after contact, otherwise OFFLINE.
Sync and telemetry also refresh contact. Gateway metadata writes no longer accept
client-provided `lastSeenAt` as evidence of contact. No database migration is needed.

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

`PUT /api/users/{id}` is administrator-only and accepts `email`, `fullName`,
and optional `roles`. When supplied, `roles` must contain exactly one
supported non-administrator role; profile and role changes are saved together.
Omitting `roles` leaves current roles unchanged. The separate role endpoint
remains administrator-only for its existing role-management behavior.

Admin-created user passwords must be 6-1024 characters. Password reset and
change endpoints retain their separate 12-character minimum.

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
FarmOwner (ManageMissions)
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

For administrators, `history` contains daily event counts over the current UTC day and the preceding 29 days, through the time of the request. Each row contains `date`, `category` and `count`. Categories are `ALERT_OPENED`, `MISSION_{terminal status}`, and `{INSTALLATION|MAINTENANCE|REPAIR}_{job action}`. Only recorded events appear, ordered by date and category. The optional farm filter applies to every category. Owners receive no administrative history.


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

| Policy | FarmAdministrator | FarmOwner | FarmEngineer | Technician |
|---|---|---|---|---|
| ManageUsers | Yes | No | No | No |
| ManageFarms | Yes | No | No | No |
| ReadFarmData | Yes | Yes | No | No |
| CreateDevices | Yes | No | No | No |
| ManageDevices | Yes | No | No | No |
| ConfigureThresholds | Yes | No | No | No |
| ManageMissions | No | Yes | No | No |

Human users authenticate with JWT Bearer. Gateway/device endpoints use independent Device Authentication with both `X-Api-Key` and `X-Gateway-Code`; human JWTs never substitute for device credentials. Device credentials do not grant access to human endpoints.

Administrator and assigned FarmOwner may read general farm data. Administrator manages farms, devices, thresholds and alert resolution; FarmOwner operates routine missions. All four supported roles may access their own notifications. Engineer/Technician contextual workflow modules are pending implementation.

FarmAdministrator has global farm access without membership. FarmOwner requires a current `user_farms` assignment to access a farm. Engineer and Technician require contextual workflow authorization; farm membership alone grants neither role general farm access. Roles still come exclusively from `user_roles`/`roles`.

UavDeviceOperator is retired from supported authentication and assignment. Historical mission fields such as `operatorNotes` remain operational notes, not authorization roles.

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
- FarmOwner accesses assigned farms only. Engineer and Technician cannot use general farm endpoints. No assignments means empty scoped lists and aggregates; explicit existing resources outside access return 403.
- Only FarmAdministrator manages assignments through GET/POST `/api/farms/{farmId}/members` and DELETE `/api/farms/{farmId}/members/{userId}`.
- Assignment requires an existing active FarmOwner user. Administrators (including mixed admin/owner roles) cannot be assigned. Assigning/removing membership never changes global roles.
- Only administrators create farms; owner membership is assigned explicitly afterward. Existing farms/users receive no automatic assignments during migration.
- Current database roles and memberships are checked per operation. Removing a membership immediately denies subsequent requests using the same JWT. Stale memberships cannot grant access without a permitted current role.
- Farms, zones, nodes/channels, readings/history/latest/comparison, thresholds, missions and their child data/telemetry, alerts and farm-derived dashboard/report aggregates are scoped. Lists are filtered in SQL before paging/counting/aggregation. Every explicit farm/resource filter is authorized.
- Alerts resolve their farm through SensorChannel -> SensorNode -> Zone -> Farm, otherwise SensorNode -> Zone -> Farm, otherwise Mission -> Farm. Alerts without such an association are administrator-only. Gateway/UAV identity alone does not establish a farm.
- New alert notifications target administrators and eligible members of the resolved farm. Notification APIs retain user-specific ownership checks, including historical notifications after membership removal; they never expose another user's notification.
- Historical mission target IDs, outcome/status and sequence remain part of mission history. If a target sensor has since moved outside the viewer's accessible farms, its current `sensorName` is null rather than exposing live metadata from another farm.

UAV/Gateway catalog access and mission eligibility now use explicit RentalDeviceAssignment records linked to farms and rental periods; the physical equipment entities remain separate from farm ownership.

Direct UAV/Gateway catalog APIs scope FarmOwner reads to unreturned rental assignments with current linked owner/farm access; Admin retains global catalog access. Farm-scoped device reports/dashboard gateway counts use unreturned rental assignments in accessible farms (or the requested authorized farm), requiring the linked Owner for non-admin reads. Historical missions do not grant current equipment access; new assignments appear before any mission. An administrator's unfiltered aggregate remains global. Sensor-type definitions are a global catalog.

Gateway/device authentication remains X-Api-Key + X-Gateway-Code. `user_farms` is only for human users and is never applied to device identity or required for telemetry/sync ingestion. Human telemetry reads require mission-farm access.

Response conventions: 401 without valid authentication; 403 for denied role/farm access; 404 for nonexistent resources; 409 for duplicate membership; 400 for invalid request data; 422 for ineligible membership targets.

### Farm filters on aggregates

`GET /api/dashboard/overview` and `GET /api/reports/devices` also accept optional `farmId`. Existing sensor/mission/alert reports retain their farm/date/pagination filters. Explicit unauthorized farmId returns 403; absent farmId aggregates only assigned farms for Owner. Device reports associate UAV/gateway equipment via current rental assignments. Engineer/Technician general report access is denied.

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

All four routes require JWT Bearer and the existing **ManageDevices** policy (FarmAdministrator). Responses use the usual `{ data: ... }` envelope and `Cache-Control: no-store`. DeviceKey credentials cannot call these human routes.

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

Human `/api/missions/{id}/start` returns 409 for Tello missions. Use scheduled launch
approval or start locally on the assigned gateway, which acknowledges RUNNING before switching Wi-Fi.
Changing status manually would otherwise remove the mission from the scheduled
download queue without commanding the aircraft.

## Scheduled mission launch
Human GET `/api/missions/{id}/launch` requires ReadFarmData plus mission-farm access.
POST `/api/missions/{id}/launch/approve` requires ManageMissions plus farm access;
body `{ "confirmAircraftReady": true, "confirmAreaClear": true, "planRevision": 2, "session": "<32 hex characters from launch status>" }`.
Approval requires fresh gateway readiness (30s), the displayed plan revision/session,
and time from start minus 10 minutes up to (exclusive) min(start plus 60s, scheduled end).
Device POST `/api/device/gateways/{gatewayId}/missions/{id}/launch/ready` and
`/launch/consume` require DeviceKey authentication; body `{ "session": "<random per-boot 32 hex characters>", "planRevision": 2 }`.
Readiness response includes state, planRevision, scheduledStartAt, expiresAt, readyAt,
approvedAt and session. States: WAITING_FOR_GATEWAY, AWAITING_CONFIRMATION,
WAITING_FOR_TIME, READY_TO_START, CONSUMED, MISSED, TERMINAL, UNAVAILABLE.
Only the first successful consume returns START_ACCEPTED and atomically transitions
RUNNING. Retries return CONSUMED, never another flight authorization. Expired readiness
cancels an unstarted mission and returns MISSED. Identity/role/farm denial is 401/403;
stale plans or invalid launch gates return 409; invalid confirmations/session return 400.
Mission detail and device next include planRevision. Replanning/rescheduling invalidates
readiness and approval. Local explicit device start remains supported separately.






User creation (`POST /api/users`) requires one or more distinct supported non-administrator roles in `roles`.
Missing/null, empty, duplicate or unsupported roles are rejected with HTTP 400.
The existing prohibition on creating FarmAdministrator accounts remains.


### Audit remediation: unresolved sensor records
Device sync accepts optional `unresolvedRecords` (maximum 100), each with
`sensorNodeCode`, `sourceRecordKey`, and `payload` containing the original record
object (`deviceCode`, canonical `recordId`, positive uint32 `sequence`). Original
uncertain timestamps remain unchanged. Records are stored separately from validated
channel readings and do not generate threshold alerts. `data.unresolvedRecords`
returns index/sourceRecordKey/status/error outcomes. Only ACCEPTED/DUPLICATE
acknowledges durable archival. A partial batch reconsiders rejected indexes on retry,
while accepted indexes retain their original outcome. Normal reading counts remain
in accepted/duplicates/rejected; archive outcomes are counted separately in that array.

GET `/api/sensor-nodes/{id}/unresolved-records?page=1&pageSize=20` returns a standard
paginated envelope with id, sensorNodeId, gatewayId, missionId, sourceRecordKey,
payload and receivedAt. ReadFarmData and current node-farm access are required.
The sensor detail page displays these separately as readings needing review.

Flight-test result recovery: RECOVERY_REQUIRED may resolve to FAILED only with
landed=true and uavConnected=true, keeping the original takeoffAttempted,
observedAltitudeCm, heldMs and errorCode. Completion cannot be fabricated during
recovery. Identical result retries remain idempotent.

### Updated SRS role migration (2026-10-28)

Migration `20261028065024_UpdatedOperationalRoles` adds Technician without converting legacy operator accounts. Legacy role rows and memberships remain inert audit data. Only explicit administrator assignment grants Technician. Downgrade preserves assigned Technician rows; re-upgrade is idempotent. No application database migration has been run by this task.

Engineer/Technician job context and rental-based device scoping remain pending subsequent phases; the current generic farm API intentionally denies these roles. Device credentials and sample administrator provisioning are unchanged.

### Consultation intake and follow-up (Phase 2, partial)
`POST /api/consultations` is anonymous and returns only `{ id, status }` in the data envelope. Contact name/phone, farm location, service need and content are required; email and farm area (hectares) are optional. Submission persists NEW plus history and queues WEB/EMAIL notifications for active Agricultural Engineers atomically.

Agricultural Engineers use `GET /api/consultations?inbox=true` for unassigned summaries or the default list for their own work, with standard paging/search. Summaries omit contact fields. `POST /api/consultations/{id}/claim` atomically assigns the current engineer and transitions NEW to IN_PROGRESS; a competing/repeated claim returns 409. Detail GET and `POST /api/consultations/{id}/follow-ups` require the assigned current engineer. Follow-up body contains required `notes`, `contactResult`, `nextAction`; events append rather than replacing earlier notes. No unrestricted status PATCH exists.

Literal statuses, claim-based routing, field length limits and optional area in hectares are implementation assumptions where SRS does not specify them. Survey, proposal, deposit, cancellation/completion and installation submission are not implemented yet. A submitted consultation grants no farm/account/rental privileges.

### Farm survey revisions (Phase 2, partial)
The assigned Agricultural Engineer records a survey with POST `/api/consultations/{id}/surveys`. Body: `expectedRevision` (0 initially), `farmName`, `farmLocation`, optional `farmArea` in hectares, `monitoringNeeds`, optional `notes`, and 1–100 relational `zones`. Each zone has `name`, optional `crop`, optional `cropAgeDays`, optional `symptoms`, and required `installationLocation`. Optional unknown values stay null; zero crop age is distinct from unknown.

GET on the collection returns paged revision summaries, newest first. GET `/{surveyId}` returns a specific immutable revision. Every save appends a revision and consultation history event in one transaction. A stale expectedRevision returns 409 without overwriting history. Writes require an IN_PROGRESS consultation and its assigned active Engineer; owner identity is not inferred from a public email or phone. Admin/owner contextual survey reads, survey image uploads and linkage to an administered farm are still pending. Existing farm/zone inventory is not created or modified by recording a survey.

Implementation assumptions: survey revisions are snapshots per consultation, area uses hectares, crop age uses days, zone names are unique within a revision. These units and limits are not prescribed literally by the SRS. SRS optional survey images remain an open requirement, not represented by arbitrary external URLs.

### Consultation customer/farm binding and survey sharing
Administrator may read consultation summaries/details through the existing GET endpoints, but cannot claim work, record surveys or add engineer follow-up solely by holding the administrator role. PUT `/api/consultations/{id}/customer-farm` accepts `{ ownerId, farmId }`, requires an active FarmOwner with current membership in that farm, and appends CUSTOMER_BOUND history. Binding is only permitted during IN_PROGRESS. Repeating the same binding is idempotent; changing an existing binding returns 409 to prevent sharing earlier customer records with a different account. This one-time binding is an implementation assumption until a separately authorized correction flow is specified.

GET `/api/farms/{farmId}/survey-consultations` returns paginated linked consultation summaries; owners see only their own linked records and must retain current farm membership. GET consultation survey list/detail permits Admin, assigned Engineer, or explicitly linked Owner with current membership. Owner access does not include internal consultation contact/follow-up details and never permits survey edits. Public email/phone values grant no account or farm access. Admin performs binding in the consultation detail UI; Admin/Owner access shared surveys from the farm page.

Migration `20261028122649_ConsultationCustomerFarm` adds nullable owner/farm references with RESTRICT foreign keys and owner/farm index. Existing requests remain unbound. No application database migration was executed. Survey images and downstream proposal/deposit/installation workflows remain pending.

### Device proposals and installation submission (Phase 2 checkpoint)
GET `/api/consultations/{id}/proposals` (paged newest first) and `/{proposalId}` allow Administrator, assigned Engineer, or linked Owner retaining current farm membership. POST collection requires assigned Engineer and body `{ expectedRevision, surveyId, rentalMonths, requestedInstallationAt, notes, lines: [{ surveyZoneId, deviceType, quantity, installationLocation }] }`. Each save appends an immutable revision; survey and all referenced zones must belong to this consultation. A stale expectedRevision returns 409.

POST `/{proposalId}/confirm-plan` records customer agreement by the assigned Engineer. POST `/{proposalId}/confirm-deposit` records external deposit confirmation by that Engineer or Administrator. Both require `{ note }` evidence/reference; no payment gateway is involved. Transitions are DRAFT -> PLAN_CONFIRMED -> DEPOSIT_CONFIRMED -> SUBMITTED; confirmation/submission requires the newest proposal and newest survey. Earlier versions and confirmation history remain intact. A revision does not inherit confirmations.

POST `/{proposalId}/submit-installation` requires assigned Engineer, confirmed plan/deposit, a linked currently authorized Owner/farm, an active farm and future requested installation time. Creates a separate SUBMITTED installation request and administrator WEB/EMAIL notifications atomically. Duplicate submission returns 409. Admin review/assignment and downstream rental activation are still pending Phase 3/4, not implied by submission.

Assumptions where the SRS gives no literal limits: rental duration 1–120 months, 1–100 relational proposal lines, quantity 1–10000, confirmation note required up to 2000 characters. Device type is a descriptive proposal value; inventory assignment occurs in the later installation/rental workflow. No application database migration has been run.

### Installation request review
GET `/api/installation-requests` returns paginated scoped summaries: all for Administrator, assigned consultation requests for Engineer, linked requests for Owner with current farm membership. GET `/{id}` includes submitted proposal devices, rental duration, requested appointment, plan/deposit confirmation evidence and review decision. Technician has no request access merely through the technician role.
POST `/{id}/approve` or `/{id}/return` requires Administrator and `{ note }` (nonblank, max 2000). Only SUBMITTED may transition to APPROVED or RETURNED. Approval rechecks confirmed proposal evidence, future requested appointment, active farm and current owner membership. Approval authorizes scheduling only; it creates neither a job nor a rental. Return and approval both retain reviewer/time/note, append consultation history, and notify the submitting engineer through WEB/EMAIL.
A returned request remains immutable history. The assigned engineer creates a new proposal revision, records plan/deposit confirmations for that revision, and submits a separate request. Existing pending/approved requests block duplicate submissions. Repeated or conflicting review returns 409. All review writes serialize on the consultation/request; authorization is rechecked from current database roles.
Implementation assumption: review notes are required for approval as well as return. SRS explicitly requires a return reason and does not prescribe literal enum names or note length. Technician assignment/report/owner confirmation/final approval remain Phase 3 follow-up work.

### Installation job assignment and work queue
POST `/api/installation-requests/{id}/jobs` requires current Administrator, an APPROVED request, active Technician and future appointment; body `{ technicianId, scheduledAt, note }`. There is at most one job per request. GET `/api/installation-jobs/technicians` is an administrator-only paginated list of active technician IDs/names. GET `/api/installation-jobs` returns scoped paginated jobs; GET `/{id}` includes the submitted survey location, proposed device lines and append-only job history. Admin sees all; Engineer requires assigned consultation; Owner requires linked identity/current farm membership; Technician requires current assignment. Job access never grants general farm access or exposes deposit/contact records.
POST `/api/installation-jobs/{id}/assignment` requires Admin and `{ expectedRevision, technicianId, scheduledAt, note }`; changes assignment/appointment on ASSIGNED or IN_PROGRESS work, resets ASSIGNED, advances revision and preserves previous events. POST `/{id}/start` requires current assigned Technician and `{ expectedRevision }`, changes ASSIGNED to IN_PROGRESS and advances revision. Stale revisions and invalid transitions return 409; nonassigned technicians return 403. Assignment checks current role/status under ordered user locks and serializes job mutations. Notifications inform the assigned technician, displaced technician and linked owner when appointment/assignment changes.
Implementation assumptions: appointment is a single UTC instant (no duration or invented calendar conflict policy); reassignment/rescheduling resets work to ASSIGNED and requires technician start again. Starting before the appointment is allowed because SRS specifies no start-time window. Notes required, max 2000. Reports, checks, evidence images, owner confirmation and final Admin approval remain pending; IN_PROGRESS is not completion or rental activation.

### Installation report versions and handover decisions (backend checkpoint)
GET `/api/installation-jobs/{jobId}/reports` and `/reports/{id}` use the same current job read scope. POST `/reports` requires current assigned Technician, IN_PROGRESS/REWORK and expected job revision. Body: `{ expectedRevision, notes, issues?, devices: [{ proposalLineId, sensorNodeId?, uavId?, gatewayId?, installationLocation, accessoriesChecked, connectivityPassed, operationPassed, testResults }] }`. Each device row references exactly one registered active asset, each physical device appears once, and every proposed line quantity must be covered. Sensor nodes must belong to the job farm. Outcomes are required booleans (omitted is invalid); failed checks require issue details. Persisted reports/checks are immutable revisions; report submission changes job to REPORT_SUBMITTED and notifies Owner/Admin.
POST `/confirm-handover`, `/approve`, `/return` take `{ expectedRevision, reportId, note }` and require the latest report and current job revision. Linked current FarmOwner confirms REPORT_SUBMITTED -> OWNER_CONFIRMED. Admin approves OWNER_CONFIRMED -> APPROVED only if all checks pass, or returns REPORT_SUBMITTED/OWNER_CONFIRMED -> REWORK with reason. Decisions and confirmations retain actor/time/note on the precise report; subsequent report versions do not inherit confirmation. All changes append job history and queue notifications transactionally. Approval does not activate rental.
Assumptions: all three checks apply to every reported device; up to 1000 checks per report; issues are descriptive, failed checks are the mechanical approval gate. Device types are the proposal's free-text descriptions, not a new catalogue taxonomy. UAV/gateway exclusivity and actual farm assignment are still required in the rental phase. Secure images, a technician asset-selection/report-entry UI, broader concurrency/negative regression cases and live browser E2E remain incomplete; this checkpoint is not full UC11–13 completion.

### Administrator device allocation and technician report entry
PUT `/api/installation-jobs/{id}/devices` requires current Admin and `{ expectedRevision, note, devices: [{ proposalLineId, sensorNodeId?, uavId?, gatewayId? }] }`. Exactly one typed active registered device per entry, no duplicate physical devices, exact proposal quantities, sensor farm scope. Only ASSIGNED/IN_PROGRESS/REWORK accept changes. Prior active allocation records receive released_at, new rows retain device code/name snapshots and allocating actor/time; job revision/history advance atomically. Filtered unique indexes prevent overlapping active allocations across jobs, including concurrent requests; conflicts return 409 and roll back replacement.
GET the same route uses current job read scope and exposes only current allocated device names/codes/typed IDs. Technician report submission must exactly match this allocation, closing the earlier broad UAV/gateway input eligibility. Frontend admin selects from existing registered catalogs (sensors filtered to farm), technician form loads only job allocation and requires explicit accessory/connectivity/operation results, locations, evidence and notes. Failed checks require issues. Successful submission returns to persisted job/report state; errors keep draft values. No arbitrary device-ID entry in the technician form.
Reservations are retained after installation approval for the upcoming rental lifecycle; rental activation/return/replacement must integrate these records before Phase 4 is complete. Existing pre-allocation reports remain historical but new submissions require explicit allocation. No automatic inferred device assignment or production database migration occurs.

Proposal capacity correction: device quantities now have a per-line maximum of 1000 and a total proposal maximum of 1000, matching allocation/report limits. This replaces the earlier 10000-per-line assumption and prevents an accepted proposal becoming impossible to report. Tests cover single-line overflow and aggregate overflow; SRS does not prescribe these numerical limits.

### Survey and installation image evidence
Survey creation and installation-report submission accept optional `images: [{contentType, dataBase64}]`. Supported MIME types are `image/png` and `image/jpeg`; at most five images, each at most 2 MiB decoded. Invalid encoding, mismatched signatures and excess limits return 400. These two write endpoints allow at most 20 MiB JSON bodies including base64 overhead and existing fields. Omitted/null images mean no images in the new revision.
Scoped detail responses include `images` with the same fields; list responses do not include image content. Detail access uses current survey/job permissions, with no public image URL or standalone upload. Responses disable caching. Images are saved atomically with the immutable survey/report revision; new versions do not automatically inherit evidence unless explicitly supplied.

### Rental contracts (updated SRS)
GET `/api/rental-contracts` is paginated and scoped to current Admin, linked Owner with farm membership, or the consultation's Engineer. GET `/{id}` includes assigned devices, actual installation locations, return timestamps and dated audit history; Technician has no general contract access.
Admin POST `/api/installation-jobs/{jobId}/rental-contract` accepts `{startAt,endAt,note}` after final installation approval, creating one DRAFT per job. POST `/api/rental-contracts/{id}/activate` accepts `{expectedRevision,note}` and requires the agreed period to have begun and not expired. POST `/{id}/extend` accepts `{expectedRevision,endAt,note}` for a later externally confirmed end. POST `/{id}/end` requires `{expectedRevision,note,devicesReturned:true}`; records retrieval and releases reservations. All dates are UTC. Owner handover/installation approval never activates a rental automatically.
Stale revision, wrong state and duplicate creation return 409; invalid dates/missing evidence/return confirmation return 400; inactive or unassigned farm/devices return 422. Current linked owner and active devices are checked on creation/activation/renewal. Expiration does not release devices. Ended contracts retain all history and cannot be renewed.

### Mission rental eligibility
Existing mission plan/schedule/start and launch-readiness/approval APIs now return 422 when selected UAV/gateway lacks a current assigned rental covering the mission period. Changing farm membership does not create a device assignment. Cancellation and terminal result/sync APIs remain available under existing access rules after rental expiration. Rental end returns 409 while related flight operations remain active or need recovery.

### Equipment catalog rental scope
GET `/api/uavs` and `/api/gateways` filter Owner rows before count/pagination to unreturned rental assignments linked to that owner with current farm membership. Detail endpoints reject existing but unassigned equipment with403. Admin retains full catalog access. Both list/detail add nullable `rental: {farmId,startAt,endAt,status}`; ACTIVE past end is reported EXPIRED immediately. DRAFT/EXPIRED unreturned equipment remains visible for tracking but is not eligible for missions. Returned devices remain in contract history and leave the Owner's live catalog. Device heartbeat and internal equipment lookup retain their separate authentication paths.

### Mission operational notes
`POST /api/missions/{id}/notes` accepts `{ "note": "Field observation" }` (nonblank, maximum2000 characters) and returns204. Current FarmOwner mission-management permission and farm membership are required. Appends an `OPERATIONAL_NOTE` mission log with authenticated user/time; does not replace planning notes, change status/plan revision, create collection outcomes or send flight commands. Notes may be appended to terminal missions for follow-up observations. Existing scoped/paginated mission logs expose the history. No new table or migration.

### Mission note image evidence
Mission note POST optionally accepts `images: [{ contentType, dataBase64 }]`, using the existing limit of five PNG/JPEG images, two MiB each and a20MiB request ceiling. Images and note are committed atomically. Mission log list adds `imageCount`, not image bytes. `GET /api/missions/{id}/notes/{noteId}/images` returns the evidence array under current mission read authorization, checks the note belongs to this mission, and disables response caching. Unknown/mismatched note returns404; loss of farm access returns403. Existing result counts and lifecycle are unchanged.

### Device incident intake
GET /api/device-incidents lists scoped incidents with standard pagination; GET /{id} returns evidence and reporter. POST accepts rentalDeviceAssignmentId, required description (max4000), optional missionId and optional bounded images; returns201 OPEN. Only current FarmOwner linked to the assignment contract and farm may submit; expired unreturned devices remain reportable. Returned/ENDED assignments reject new reports. Admin reads all, Owner reads linked current-farm incidents including historical returned-device reports; Technician/Engineer general access denied. Optional mission must belong to the farm and reference the affected UAV/gateway/target sensor. Evidence, report and active-admin WEB/EMAIL notifications commit atomically. Reads disable caching. No arbitrary incident status endpoint; service job processing uses the assignment and report actions below.

### Service job assignment and start
`POST /api/device-incidents/{id}/jobs` creates one service job per incident for an Administrator: type MAINTENANCE/REPAIR, active technicianId, future scheduledAt, required note<=2000, expectedRevision0. `GET /api/service-jobs` and `GET /{id}` are scoped to Admin, currently assigned Technician, or linked current FarmOwner. Detail supplies incident description/image evidence and installation location, not unrestricted farm/rental records. Engineer access denied. `POST /{id}/assignment` requires Admin and current expectedRevision, retains service type and assignment history, resets to ASSIGNED; `POST /{id}/start` requires assigned active Technician/current revision and transitions ASSIGNED->IN_PROGRESS. Assignment/reschedule notifies technician and eligible linked owner. There is no arbitrary status PATCH; reports and acceptance use the dedicated actions below.

### Independent service jobs
Service-job list/detail `incidentId` is nullable. Jobs link directly to their original rental device assignment; farm/owner authorization and installation location come from that assignment. A job without an incident returns description `Preventive maintenance` and empty incident images. Start, reassignment, replacement, reports and acceptance work with or without an incident. Approval resolves an incident only when one is linked. The incident creation endpoint still requires an incident; independent schedule creation and due processing remain pending.

### Service result reports and review
GET `/api/service-jobs/{jobId}/reports` returns paginated version summaries; GET `/reports/{id}` returns the immutable work evidence and recorded review, under the same current service-job scope. Detail responses disable caching. POST `/reports` accepts `{expectedRevision,cause,work,components,testResults,notes,connectivityPassed,operationPassed,images?}`. Cause/work/components/notes are required and bounded to4000; use an explicit "None" for unused components. Test results are required up to2000. Both pass/fail checks must be explicitly supplied; false remains valid evidence. Images reuse the existing five PNG/JPEG images of at most2MiB each; request limit20MiB. Invalid image content rolls back the whole submission.
Only the current assigned active Technician can submit IN_PROGRESS or REWORK jobs. Submission creates a new immutable report revision and sets REPORT_SUBMITTED; it does not change physical device/rental state. POST `/confirm-operation` accepts `{expectedRevision,reportId,note}` from the linked current FarmOwner with current farm access and sets OWNER_CONFIRMED. Admin POST `/approve` with the same request requires that latest report's confirmation and passing connectivity/operation checks, then sets CLOSED. Admin POST `/return` from REPORT_SUBMITTED/OWNER_CONFIRMED sets REWORK. Decisions require a note up to2000, current job revision and latest report ID. Stale versions/transitions return409; failed-check closure returns422; unauthorized actors return403. Rework keeps the original report, images, confirmation and decision; a corrected submission requires a fresh Owner confirmation. Admin may reassign a REWORK job, resetting ASSIGNED and preserving old reports. Independent maintenance schedules remain pending; replacement actions follow below.

Service approval also updates the linked device incident status from OPEN to RESOLVED atomically. Incident list/detail expose the final status; historical evidence remains readable under current scope. Returning service work leaves the incident OPEN. No incident status-write endpoint is added.

### Physical service-device replacement
GET `/api/service-jobs/{id}/replacement-options` is paginated/searchable and limited to Admin or the currently assigned Technician. It returns `{sensorNodeId,uavId,gatewayId,code,name}` for active, same-kind equipment without live rental/allocation reservations; sensors must belong to the job's farm. Options are advisory; mutations recheck availability and flight safety.
POST `/api/service-jobs/{id}/replacements` requires assigned Technician, IN_PROGRESS/REWORK, and `{expectedRevision,sensorNodeId?,uavId?,gatewayId?,oldDeviceRetrieved:true,note}`. Exactly one positive typed ID, same physical type and note<=2000 are required. Returns updated ServiceJobDetail with `currentDeviceCode` and `replacements:[{id,oldAssignmentId,newAssignmentId,oldDeviceCode,newDeviceCode,technicianId,createdAt,note}]`. Incident device identity remains original. ServiceReportResponse gains nullable `replacement`, fixed to the latest recorded exchange when that report was submitted.
Stale/state/returned/reserved/active-flight conflicts return409, wrong kind/ineligible farm returns422, missing retrieval/invalid request400, denied role/scope403. Exchange creates successor allocation/assignment under the same contract/location, records old retrieval, and sets retrieved physical equipment inactive. It does not close service work or replace Owner/Admin acceptance. Successive exchanges preserve all history; previous report snapshots never follow a later device. Expired, unreturned rentals permit repair; ended rentals do not.

### Maintenance schedule administration
Admin-only `GET /api/maintenance-schedules` supports page/pageSize and policy search; `GET /{id}` returns the schedule and creator/updater metadata. `POST` creates (expectedRevision0); `PUT /{id}` edits using current expectedRevision. Body: exactly one of `rentalDeviceAssignmentId` or `deviceType` (SENSOR/GATEWAY/UAV), required policy<=2000, intervalDays1..3650, future nextDueAt, active technicianId, enabled and expectedRevision. Eligibility follows recorded replacement successors while retaining the original schedule assignment. A current successor that has been returned, an ended contract, or missing active farm/current linked Owner is ineligible. `POST /{id}/disable` takes expectedRevision, works without rescheduling an overdue date and retains all history. No hard-delete endpoint. Stale revisions return409; invalid request400; ineligible technician/assignment422; missing schedule/assignment404. Current roles are checked on every operation, including when using an older JWT. Due processing is provided by the maintenance scheduler described below; frontend management remains pending.

### Maintenance due processing
`MaintenanceScheduler:Enabled` defaults true; `IntervalSeconds` defaults60 and accepts1..86400. The hosted worker scans due enabled schedules in ID pages of50 and processes each in its own scope/transaction; a failed schedule does not stop later IDs. It creates ordinary MAINTENANCE service jobs, occurrence/policy snapshots and WEB/EMAIL notifications for the responsible Technician and linked Owner. A specific assignment follows recorded replacement successors; a type schedule selects current unreturned assignments under non-ended contracts with active farm/current linked Owner. Expired unreturned equipment remains serviceable. No eligible assignment or inactive Technician leaves the schedule overdue for Admin correction. Disable remains available without changing the date. Processing advances nextDueAt to the first future anchored interval and increments revision; UI updates must reload after worker changes. No public trigger endpoint is exposed.

### Maintenance occurrence history
Admin-only `GET /api/maintenance-schedules/{id}/occurrences` returns standard paginated history ordered by dueAt/id descending. Each row includes occurrence id, jobId, rentalDeviceAssignmentId, recorded assignment deviceCode, dueAt, createdAt, policy snapshot and current job status. Editing the schedule does not rewrite its previous occurrence policies or original assignment identities. Missing schedule404, invalid pagination400, non-Admin403. The maintenance UI displays this history with links to `/app/service-jobs?jobId={id}`; linked job reads still use the service-job authorization rules.

### Crop condition intake
`POST /api/crop-condition-reports` accepts farmId, optional zoneId, crop (required, max200), description (required, max4000), optional sensorChannelIds (at most50 distinct positive IDs), and optional bounded PNG/JPEG images. Current active FarmOwner must have membership of the active farm; zone and channels must belong to that farm and, when selected, that zone. Returns201 with NEW status, revision1, evidence and initial SUBMITTED history. Report/evidence/history and active Engineer WEB/EMAIL notifications commit atomically. Invalid input400, denied access403, invalid zone/channel/farm eligibility422.
`GET /api/crop-condition-reports` returns standard pagination without image bytes. `GET /{id}` includes evidence, sensor reference IDs and ordered consultation updates. Admin can monitor all; Owner requires both reporter identity and current farm membership; Engineer can read unclaimed NEW reports or reports assigned to that Engineer. Technician has no access. Existing inaccessible report403, missing report404; reads disable caching. These endpoints do not grant access to general farm/sensor APIs. Processing actions and contextual sensor readings remain pending.

### Crop support processing API
POST `/api/crop-condition-reports/{id}/claim` has no body; only a current active Engineer may claim an unclaimed NEW report, changing it to IN_REVIEW. Concurrent claims have one winner; others409. POST `/{id}/request-information`, `/{id}/reply`, `/{id}/recommend`, `/{id}/resolve` accept `{expectedRevision, content, recommendSiteSurvey?, images?}`; content required<=4000 and the same bounded evidence limits apply. Only the assigned current Engineer requests information, recommends or resolves; only the reporter with current Owner role and farm membership replies. Admin monitoring grants no processing rights.
Request-information: IN_REVIEW -> AWAITING_INFORMATION; reply: AWAITING_INFORMATION -> IN_REVIEW; recommend: IN_REVIEW stays IN_REVIEW; resolve: IN_REVIEW -> RESOLVED. RecommendSiteSurvey is allowed only on recommend. Invalid state/stale revision409; denied actor403; malformed content/images400; misplaced site-survey flag422. Each successful action increments revision and appends attributed UTC history with evidence. Eligible counterpart receives WEB/EMAIL notifications atomically. Resolved cases are read-only. Status vocabulary and explicit claim are implementation choices where the SRS does not prescribe tokens. No generated diagnosis or automatic survey/job creation.

### Crop report sensor context
GET `/api/crop-condition-reports/{id}/sensor-readings` accepts standard pagination and returns measurement id, sensorChannelId, channelCode, unit, value, measuredAt, isValid and qualityStatus, newest measuredAt/id first. It applies the same current report visibility as detail and restricts data to report-linked channels still in the report farm and optional zone. Moved channels produce no readings; general farm/sensor API permissions are unchanged. No write endpoint, mission/gateway identities, raw source keys or generated diagnosis are exposed by this contextual endpoint. Invalid pagination400; inaccessible report403; missing404. This is live context, not a snapshot of the readings at submission time.

### Crop seasons
POST /api/crop-seasons creates an ACTIVE season at revision 1. Submit expectedRevision 0, farmId, optional zoneId, name and crop. Optional metadata includes areaHectares, startDate, expectedEndDate, cropAgeDays and notes. Missing values remain null. PUT /api/crop-seasons/{id} replaces the editable metadata and requires the current revision and original farm/zone. Concurrent edits at one revision have one winner. A completed season rejects edits. Writes require the recorded Owner, current membership and an active farm. GET /api/crop-seasons lists visible seasons with standard pagination. GET /api/crop-seasons/{id} returns details. Admin may monitor all seasons. Owner reads require current membership and ownership. POST /api/crop-seasons/{id}/complete accepts the current expectedRevision plus optional endDate, yieldKg, totalCostVnd, revenueVnd and notes. Only the recorded Owner with current membership can complete an active season. An actual end date before a recorded start returns 422. Successful completion advances the revision and closes the season to further edits. Unknown results remain null and supplied zero values are retained.

POST /api/crop-seasons/{id}/activities appends a dated cultivation log to an active season and returns the updated season. Send expectedRevision, date, type and description with optional costVnd, notes and images. Image limits are five JPEG/PNG files up to 2 MiB each. The activity, images and season revision are saved together. GET /api/crop-seasons/{id}/activities lists activities by date and ID descending with standard pagination and imageCount. GET /api/crop-seasons/{id}/activities/{activityId}/images returns evidence for that activity. Both reads enforce current season access, including after completion.

GET /api/crop-seasons/compare?ids=1&ids=2 returns the selected seasons in request order. A Farm Owner may select 2 through 50 distinct seasons within current farm membership. Each row includes recorded area, yield, total cost, revenue, activity counts by type, elapsed days between actual start and end dates, and revenue less total cost. Missing inputs leave the derived result null. A selection containing an inaccessible season returns 403 for the whole comparison. An unknown season returns 404.

### Public homepage content

Visitors can read the paged list at `GET /api/public-content`. Each entry provides its category, title, body, optional estimated monthly device rent in VND and an image indicator. `GET /api/public-content/{id}/image` serves the published image. A missing, draft or withdrawn image returns 404.

Administrators list all entries or create drafts at `/api/admin/public-content` using GET or POST. Send `kind`, `title`, `body` and `expectedRevision: 0` to create a draft. Supported categories are SERVICE, DEVICE, INSTALLATION, WARRANTY, MAINTENANCE, RETURN and CONTACT. Only DEVICE accepts `estimatedMonthlyRentVnd`. Supply one optional PNG or JPEG as `image` with `contentType` and `dataBase64`, up to 2 MiB decoded.

Update with `PUT /api/admin/public-content/{id}` and the loaded revision. Omitting the image retains it. Set `removeImage` to remove it. Changes to a published entry take effect immediately. Use `POST /api/admin/public-content/{id}/publish` or `/unpublish` with `expectedRevision` to change visibility. A concurrent edit or repeated publication transition returns 409.

An incident detail response includes nullable `serviceJobId`. After assignment, the Admin or the authorized Farm Owner can open that service job from the incident detail. The incident remains OPEN during repair and changes to RESOLVED when the Admin approves the owner-confirmed service report.

### Operator-confirmed mission completion

`POST /api/missions/{id}/complete` accepts optional `manualCompletion: true` together with a required nonblank `note`. The normal Farm Owner authorization, farm access and RUNNING-to-COMPLETED transition still apply. This records a human observation when collection outcomes are unavailable. A `MANUAL_COMPLETION` log retains the actor, timestamp and statement. Existing pending targets remain pending and no collection attempt, reading or successful target is synthesized.

Requests without the flag continue to require recorded outcomes for every target. The flag is invalid for other lifecycle actions or device actors. A late result is handled by the existing ingestion rules. This implementation treats the mission's terminal state as the flight outcome and its per-target result as the separately recorded collection outcome.

Mission create and edit requests accept an optional purpose of up to 2,000 characters. Mission detail returns purpose independently of operatorNotes and appended operational observations.
Mission requests also accept an optional zoneId. The zone must belong to the selected farm and contain all target sensors. Mission detail returns zone as {id, name} or null for a farm-wide plan.
