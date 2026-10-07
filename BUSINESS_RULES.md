# BUSINESS_RULES.md

## 1. Project Scope

The system is a UAV-assisted IoT platform for farm monitoring and sensor data collection.

The backend supports:

- farms and zones
- sensor nodes and sensor channels
- UAVs
- mobile gateways
- mission planning and scheduling
- waypoint/flight-point planning
- mission monitoring
- sensor collection results
- sensor measurement storage
- offline synchronization
- threshold-based alerts
- web/email notifications
- reports

The initial project scope does NOT include direct UAV flight control from the backend.

---

## 2. Roles

The only human account roles are FarmAdministrator, FarmOwner and FarmEngineer.

- FarmAdministrator: system/user administration; farm, zone and device management; farm data access; threshold configuration; mission management; alerts and reports.
- FarmOwner: farm/zone and device metadata management; farm/sensor data, alerts and reports; existing mission create/update/schedule/start/complete/fail/cancel actions. Cannot manage users or configure thresholds.
- FarmEngineer: read farms/zones and sensor data/history, alerts and reports; configure thresholds. Cannot manage users, farms/zones, devices or missions.

DevelopmentAdminSeeder bootstraps only FarmAdministrator through the existing environment-variable configuration. It does not create Owner/Engineer accounts or hard-code passwords.
---

## 3. Farm and Zone Rules

1. A farm can contain many zones.
2. A zone belongs to one farm.
3. A sensor node belongs to one zone.
4. A sensor selected for a mission must belong to the mission's selected farm.
5. A farm or zone with operational history should not be hard deleted by default.

---

## 4. Sensor Node Rules

1. One physical sensor node can expose many sensor channels.
2. A channel represents one measurable value.
3. Sensor types may include:
   - soil moisture
   - air temperature
   - air humidity
   - light intensity
   - pH
   - water level
4. Every node must have a unique device code.
5. Only registered/active devices are accepted for normal collection.
6. Sensor location can use:
   - GPS latitude/longitude, or
   - local demo/reference coordinates
7. Sensor data may be temporarily stored on the node until a gateway approaches.

---

## 5. Sensor Reading Rules

Every accepted reading must identify:

- sensor channel
- value
- measurement time
- collection/source identity
- source record key for duplicate prevention

Optional context:

- mission
- gateway
- collection time
- quality/validation status

Validation should detect:

- unknown sensor
- unknown channel
- inactive sensor
- malformed value
- missing required field
- invalid timestamp
- duplicate record
- value outside technical validation range when such a range exists

A duplicate reading must not be inserted again.

---

## 6. Gateway Rules

A mobile gateway can be ESP32, Raspberry Pi, or another approved device.

Responsibilities:

1. discover registered sensors
2. authenticate/identify sensors
3. collect sensor data
4. record collection time
5. optionally record location
6. save collected data locally if server/Internet is unavailable
7. synchronize later
8. retry failed synchronization
9. avoid data loss

The gateway communicates with the backend API.

It must not connect directly to PostgreSQL.

---

## 7. Offline Synchronization Rules

### 7.1 Idempotency

The same sync batch may be sent more than once.

The backend MUST return a safe result without duplicating persisted data.

A batch is identified by:

```text
gateway_id + batch_key
```

A sensor record is identified by a unique source key or equivalent deterministic key.

### 7.2 Partial success

A sync batch may contain:

```text
valid records
duplicate records
invalid records
```

One invalid record should not necessarily reject the whole batch.

The response should report:

```text
accepted count
duplicate count
rejected count
error reason per rejected item
```

### 7.3 Retry

If the gateway times out without receiving confirmation, it may resend the same batch.

That retry must be safe.

---

## 8. UAV Rules

The UAV is treated as an operational asset.

Backend may track:

- status
- model
- battery
- latest seen time
- assigned gateway
- telemetry
- mission assignment

Actual autopilot behavior such as stabilization, waypoint navigation, RTL, and low-level flight control belongs to the UAV/autopilot system, not backend business logic.

---

## 9. Mission Definition

A mission is one planned data-collection operation.

A mission can contain:

- one farm
- zero or one assigned UAV during planning
- zero or one assigned gateway during planning
- multiple target sensor nodes
- multiple waypoints
- scheduled start time
- operational notes
- mission result
- collection attempts
- telemetry when available

---

## 10. Mission Status

Baseline mission states:

```text
PENDING
SCHEDULED
RUNNING
COMPLETED
FAILED
CANCELLED
```

Meaning:

### PENDING

Mission exists but is not yet ready/scheduled.

### SCHEDULED

Mission has a future/defined schedule and required planning data.

### RUNNING

Mission is currently being executed or tracked.

### COMPLETED

Mission execution ended successfully according to completion criteria.

### FAILED

Mission cannot complete due to operational/device/collection error.

### CANCELLED

Mission was intentionally stopped before normal completion.

---

## 11. Mission Status Transitions

Allowed baseline transitions:

```text
PENDING   → SCHEDULED
PENDING   → CANCELLED

SCHEDULED → PENDING
SCHEDULED → RUNNING
SCHEDULED → CANCELLED

RUNNING   → COMPLETED
RUNNING   → FAILED
RUNNING   → CANCELLED
```

Terminal states:

```text
COMPLETED
FAILED
CANCELLED
```

Do not allow transitions out of a terminal state unless a future explicit reopen/retry feature is designed.

---

## 12. Mission Creation Rules

To create a mission:

1. farm must exist and be active
2. creator must have ManageMissions permission (FarmAdministrator or FarmOwner)
3. selected sensors must exist
4. selected sensors must belong to the selected farm
5. duplicate target sensor IDs are not allowed
6. waypoint sequence numbers must be unique
7. assigned UAV/gateway must exist if supplied
8. mission begins as `PENDING` unless creation request explicitly satisfies scheduling rules

---

## 13. Mission Scheduling Rules

To change mission to `SCHEDULED`:

- mission must not be terminal
- scheduled start must be provided
- mission must contain at least one target sensor
- route/waypoint requirements must be satisfied for the active prototype approach
- assigned operational equipment must be valid if required by deployment

If two missions cannot share the same UAV at overlapping times, scheduling must detect conflicts.

Initial implementation may use a simple overlap check.

---

## 14. Mission Start Rules

A mission can become `RUNNING` only from an allowed state.

Before starting, validate:

- mission exists
- mission is not cancelled/completed/failed
- selected targets still exist
- assigned gateway/UAV are available when required
- required planning data exists

Starting a mission records:

```text
started_at
status = RUNNING
mission log entry
```

The backend status change means tracking has started.
It does not mean the backend directly sends motor/flight commands.

---

## 15. Mission Completion Rules

A mission may become `COMPLETED` when execution has ended and results are recorded.

Completion should capture:

```text
completed_at
target success/failure summary
operator notes when provided
```

A mission may still have failed individual collection targets while being considered operationally completed if project policy allows "completed with partial collection".

If this distinction is later required, add an explicit result summary rather than adding a new mission status without design review.

---

## 16. Mission Failure Rules

A mission may fail due to:

- gateway communication failure
- UAV operational issue
- critical battery issue
- unrecoverable sensor collection failure
- operator-declared operational failure

Failure should preserve:

```text
failure_reason
completed_at or failure timestamp
mission log
collection results already received
```

Do not delete partial mission data after failure.

---

## 17. Mission Cancellation Rules

Cancellation is allowed before terminal completion.

Cancellation should:

```text
set status = CANCELLED
record time
record the authorized human user
record optional reason
append mission log
```

Already collected data remains stored.

---

## 18. Mission Targets

Each selected sensor becomes a mission target.

Target status baseline:

```text
PENDING
COLLECTED
FAILED
SKIPPED
```

Rules:

- target must belong to same farm as mission
- one sensor appears once per mission
- collection may be attempted multiple times
- every attempt is recorded separately

---

## 19. Collection Attempts

One target may have multiple attempts.

Every attempt records:

- attempt number
- start
- finish
- success/failure
- number of records
- error code/message

Example:

```text
Target 10
  Attempt 1 → TIMEOUT
  Attempt 2 → SUCCESS
```

Do not overwrite Attempt 1 when Attempt 2 succeeds.

---

## 20. Waypoint Rules

A mission may contain multiple waypoints.

Each waypoint:

- belongs to one mission
- has a unique sequence number inside that mission
- may contain GPS coordinates
- may contain local/reference coordinates for prototype/demo
- may contain altitude
- may contain action/hold metadata

Backend stores waypoints for planning/monitoring.

The autopilot/UAV-side system decides how to physically execute them.

---

## 21. Telemetry Rules

Telemetry is optional and stored when available.

Possible fields:

- UAV position
- altitude
- battery
- current waypoint
- flight status
- recorded time

Telemetry is time-series history.

Do not overwrite previous telemetry points with only the latest value.

The API may expose a separate "latest telemetry" query.

---

## 22. Alert Rules

The system detects abnormal conditions from configured rules.

Alert types include:

```text
SENSOR_THRESHOLD
SENSOR_DATA_TIMEOUT
SENSOR_LOW_BATTERY
GATEWAY_ERROR
GATEWAY_OFFLINE
UAV_LOW_BATTERY
MISSION_ERROR
```

An alert must reference the relevant entity where possible.

Examples:

```text
sensor threshold alert → sensor_channel_id
gateway error          → gateway_id
mission error          → mission_id
UAV low battery        → uav_id
```

A `GATEWAY_ERROR` without `gateway_id` is invalid.

---

## 23. Sensor Threshold Rules

A threshold may define:

```text
min_value
max_value
data_timeout_minutes
low_battery_percent
```

Threshold alert logic:

```text
value < min_value  → alert
value > max_value  → alert
```

If one side is null, only evaluate the configured side.

Do not create a new identical open alert every time the same reading condition is observed.

Prefer either:

- update existing open alert metadata, or
- suppress duplicates until condition clears/closes

The exact suppression strategy must remain consistent.

---

## 24. Data Timeout Rule

A sensor data-timeout alert is created when:

```text
current time - last valid reading time > configured timeout
```

Timeout evaluation should use UTC.

Inactive sensors should not automatically generate monitoring alerts unless explicitly required.

---

## 25. Alert Lifecycle

Status:

```text
OPEN
ACKNOWLEDGED
CLOSED
```

### OPEN

System detected condition.

### ACKNOWLEDGED

A user has seen/accepted responsibility for handling it.

### CLOSED

Condition was resolved or administrator closed the case.

Every handling action should append an `alert_histories` record.

Do not erase previous notes.

---

## 26. Notification Rules

Notification channels:

```text
WEB
EMAIL
```

Typical flow:

```text
Alert created
   ↓
Create web notification
   ↓
If email configured/required
   ↓
Send email
   ↓
Record delivery result
```

A failed email send must not roll back the underlying alert.

---

## 27. Authentication Rules

Human users authenticate with email/password.

Passwords:

- never plaintext
- always hashed

JWT access token:

- short-lived
- sent as Bearer token

Refresh token:

- persisted securely
- expires
- can be revoked
- rotated on refresh
- old token invalid after successful rotation

A revoked refresh token must not issue a new access token.

---

## 28. Authorization Rules

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

Backend policy enforcement is mandatory; do not rely on frontend visibility.

---

## 29. Audit and History Rules

Preserve important operational history:

- mission status changes
- mission logs
- collection attempts
- alert handling
- sync batches
- sensor readings
- telemetry

Avoid hard delete for these records.

---

## 30. Time Rules

All backend timestamps are UTC.

Use `DateTimeOffset` or equivalent UTC-safe representation.

Frontend may convert to local time for display.

---

## 31. Report Rules

Reports may summarize:

- sensor data
- device status
- alerts
- missions
- collection success/failure

Reports must be generated from persisted backend data and should honor role authorization.

---

## 32. Performance Rules

Normal web operations should target approximately three seconds or less under normal operating conditions.

Newly synchronized sensor data should appear on the dashboard within approximately ten seconds under normal connectivity.

Do not run unbounded queries for:

- sensor readings
- telemetry
- alerts
- mission logs

Always support date filters and/or pagination.

---

## 33. Data Safety Rules

The backend must:

- validate device payloads
- prevent duplicate readings
- preserve offline-synced data
- use database constraints where possible
- use transactions for multi-table operations that must be atomic

Examples requiring transactions:

```text
refresh-token rotation
mission creation with targets and waypoints
sync-batch persistence
alert creation plus initial history/notification records
```

---

## 34. Scope Guardrails

Do not add these to backend unless explicitly requested later:

- direct motor control
- flight stabilization logic
- autonomous collision avoidance
- crop image AI
- computer vision pipeline
- low-level MAVLink flight-control logic

Backend can store information from those systems, but should not own their low-level control logic.


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
