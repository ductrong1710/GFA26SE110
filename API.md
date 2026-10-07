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

CreateUser and SetRoles accept only the three supported role names, require unique roles, and reject unknown or deprecated role names. Existing email/password validation remains in effect.

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
