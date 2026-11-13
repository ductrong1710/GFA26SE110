# BUSINESS_RULES.md

The dated phase-checkpoint sections preserve implementation history. Later workflow sections supersede their pending-work notes. Current workflow evidence is recorded in [Current functional evidence](docs/superpowers/plans/2026-11-04-srs-current-evidence.md), including installation approval, rental activation, service closure, maintenance, crop support and crop seasons.

An Administrator may explicitly reopen CLOSED service work with a fresh revision, future appointment, active technician and reason. The affected device or its recorded replacement must still be assigned under a non-ended rental. Reopening records REOPENED and returns the job to ASSIGNED and its incident to OPEN without changing approved reports. Further work requires a new service plan, report, owner confirmation and administrator approval.

### External consultation dossier — 2026-11-03

Consultation, device agreement and travel-deposit payment happen outside the system.
The engineer records the customer, garden, agreed devices/period/appointment and bill.
The engineer submits customer details without granting account or farm access. On approval, Admin creates or reuses an eligible owner account and creates its farm/membership when the consultation was not previously linked. The binding and approved installation are committed together. Returning the dossier does not create an account.
Existing administrator, engineer, mixed-role or inactive accounts cannot be reused as
customers by this operation. Account creation does not reveal or overwrite passwords.
Pending dossiers cannot be superseded by edits; returned versions and bills remain
available for audit. The latest revision and latest survey must be used. Final approval
is serialized and creates exactly one approved installation request before assignment.
Record plan and deposit confirmations before dossier submission. Submitting a bill preserves both confirmation records.


### Operational workflow — 2026-11-03 (supersedes earlier conflicting checkpoints)

Public consultation forms are contact leads. Admin monitors contact lists/counts;
no shifts, missing-claim KPI or Admin dispatch for these leads. External quick links
do not create tracked contacts. Engineer context may be established by first saved
survey/follow-up without a claim. New surveys discard age and proposed equipment
position; technicians record the actual surveyed installation position.

Assignment duration defaults to 2h, max24h; 30-minute travel buffer applies across
open installation/service jobs. Admin may change technician/appointment with audit
and notifications. Scheduled maintenance keeps its configured technician, finds a
free slot, then creates assigned work. These are explicit scheduling assumptions,
not route planning, shift hours or leave management.

Installation and service reports require signed paper evidence. The linked Farm Owner must confirm the latest report before Admin can approve it. Admin can return a submitted report for rework.
Installation approval creates a draft with a suggested start on the next Vietnam calendar day. Admin can confirm or change the start within the handover day or later. A confirmed draft activates when due. Until then, or until Admin explicitly activates a due draft, rented UAVs cannot be used for missions. Catalog price edits never mutate snapshotted proposal/service prices.
Rental total is the initial agreed period, not received revenue or renewal totals.

ServicePlan lifecycle: PROPOSED -> APPROVED -> EXECUTED -> APPLIED, or PROPOSED ->
REJECTED. Admin reviews before work; final report approval commits the equipment
exchange. Pending targets are reserved but not installed. Warranty lines cost zero;
other parts use published snapshotted unit prices. Labour is free. No warehouse or
accounting. Audit actions add PLAN_PROPOSED/APPROVED/REJECTED/EXECUTED. Final changes
and job closure rollback together on conflict; repeat approval cannot exchange twice.
Failed equipment becomes inactive/FAULTY_AWAITING_COLLECTION; contractual assignment
release is not proof of physical collection. Do not delete assets/history.

An unidentified incident belongs to a currently authorized owner's farm; technician
or Admin identifies the device, not the customer. No-data symptoms are not automatic
hardware diagnoses. Dashboard buckets use real creation/open timestamps and current
statuses; they are not historical transition counts. All new technical reads enforce
current assignment/role, and the new operations dashboard remains administrator-only.

### Operational dashboard aggregation

Dashboard jobs remain open until installation APPROVED or service CLOSED. Service counts distinguish MAINTENANCE and REPAIR. A rental is currently active only when its stored status is ACTIVE and its start/end interval contains the current time; expiration counts include stored EXPIRED contracts and ACTIVE contracts whose end has passed. Ended/draft contracts are not active or expiring. The advance-warning window comes from the rental expiration configuration. Owner operational counts and recent cultivation require both current farm access and the corresponding owner identity; Admin can aggregate across farms. Recent cultivation lists at most five actual activities by recorded agricultural date, without estimating missing outcomes.

Flight tests have an additional terminal status CANCELLED. Only an administrator
may cancel a QUEUED, CLAIMED or EXPIRED test after explicitly confirming that the
aircraft is grounded and the test was never started by firmware. Recorded takeoff,
positive altitude/hold evidence, or RECOVERY_REQUIRED prevents this cancellation.
It preserves device evidence, records actor/reason in error_code and time in
reported_at, and releases reservations under the existing shared transaction lock.
Cancelled tests cannot be claimed or replaced by device results. Cancellation is
not a flight-control/landing command; running firmware must not be executing the test.

Reservation-conflict messages identify blocking missions only within the current
human user's farm access. Device callers may see only blockers assigned to their
gateway. Flight-test IDs/status are shown only to administrators or the assigned
device. Hidden blockers still prevent scheduling/starting and produce a generic
notice. Overlap rules and reservation locking are unchanged.

## Gateway connectivity

Gateway catalog/detail and report status is ONLINE only for an active gateway
with server-recorded contact in the last 90 seconds (inclusive). Never-seen,
inactive, expired or future-dated contacts are OFFLINE. This describes backend
connectivity, not whether the gateway has electrical power or whether a UAV is airborne.
Heartbeat, successful sync and telemetry update contact. Human gateway edits cannot
set contact time. Stored administrative status remains separate from computed
connectivity, including existing error monitoring. Heartbeat requires device credentials
and matching route identity; it only writes last_seen_at and does not acquire or
release flight reservations. Firmware attempts heartbeat every 30 seconds when
Internet is available and flight/collection work is safe; Tello operation can cause
expected OFFLINE periods. Dashboard and gateway device reports use the same 90-second rule.

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

Human account roles are FarmAdministrator, FarmOwner, FarmEngineer and Technician.

- FarmAdministrator: system/user administration; farm, zone and device management; farm data access; threshold configuration; mission monitoring; alerts and reports.
- FarmOwner: assigned farm/sensor data, alerts and reports; mission create/update/schedule/start/complete/fail/cancel actions. Cannot administer farms, devices, users or thresholds.
- FarmEngineer: assigned consultation, survey, proposal and crop-support work; supporting contextual reads are pending the workflow modules. No general farm access, threshold or mission control.

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

Password recovery uses a cryptographically random token emailed to the active account. Only its SHA-256 hash is stored; it expires after 15 minutes and is consumed atomically once. Resending after a 60-second cooldown invalidates the earlier token. Public responses do not disclose account existence or email delivery outcome. Requesting recovery never changes the password.

Changing one's password requires a valid human JWT and the current password. Reset and change require a new password of 12–1024 characters and invalidate all old access/refresh sessions and outstanding reset tokens. Concurrent resets cannot both succeed. Account password versions also reject tokens issued concurrently using stale credentials.

Admin user creation cannot assign `FarmAdministrator`, alone or combined with other roles. This restriction is enforced in backend validation; the separate role-update operation and seeded administrator accounts retain their existing rules.

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

## Device mission lifecycle and explicit target assignment

- Human/web users create and schedule missions under existing role/farm rules.
  TargetAssignments explicitly links SensorNodeId to WaypointSequenceNo with a
  unique positive target SequenceNo. The referenced waypoint must be submitted
  in the same plan. Legacy SensorNodeIds supports drafts; when both forms are
  supplied their sets must agree. Scheduling and starting require complete
  target-to-waypoint mappings and active sensors in the mission farm.
- The assigned gateway pulls only its SCHEDULED missions, ordered by scheduled
  start then ID. Pull is a read and is allowed before scheduled time; it returns
  only one mission. An empty queue is successful with null data.
- Download validation/local persistence/verification is gateway responsibility.
  A downloaded ACK appends one DOWNLOADED device log and keeps SCHEDULED. READY
  and DOWNLOADED are not new backend mission statuses.
- After local START, the gateway must receive backend /start success while still
  online, before disconnecting to execute offline. Shared MissionLifecycle uses
  existing MissionRules, active resource checks, schedule validity, explicit
  mapping and reservation overlap checks. Start sets RUNNING and StartedAt.
- After safely landing and reconnecting, the gateway submits readings and
  collection outcomes through the existing sync endpoint with MissionId.
  Completion requires at least one target and none PENDING; FAILED/SKIPPED target
  outcomes continue to permit operational completion under existing rules.
- Device fail requires FailureReason, sets CompletedAt and preserves existing
  MISSION_ERROR alert generation. Device cancel uses existing cancellation
  transitions and represents the gateway's completed physical abort handling.
  The backend does not issue flight or motor commands.
- Device identity uses existing X-Gateway-Code + X-Api-Key. Route gateway and
  mission assignment are both enforced. Device services never require human
  farm membership or JWT. Logs use GatewayId with null UserId; human transition
  logs retain UserId with null GatewayId.
- Writes lock the mission in a transaction before ownership/state decisions,
  then acquire equipment locks in existing order. Concurrent retries serialize.
  Repeated downloaded in SCHEDULED adds no log; repeated start in RUNNING or
  complete in COMPLETED does not change timestamps or logs. Failed retry must
  have the same trimmed reason. Repeated cancel preserves the original outcome.
  Other invalid/terminal transitions remain conflicts, including a web cancel
  that wins the lock before device start.
- Local and GPS coordinates remain supported; backend altitude validation uses
  the numeric storage range, not a fixed 40 cm ceiling. ESP32 performs final
  configurable flight-safety validation and local execution.

The sample administrator migration automatically creates admin@sep.local with the requested sample password 123456 (stored as a hash) and FarmAdministrator. Existing accounts are never promoted, reactivated or reset by this migration. Normal API password validation remains unchanged.

## Gateway credential rules

- ManageDevices remains FarmAdministrator/FarmOwner; gateway catalog access is global under the existing policy, not an invented farm ownership relationship.
- Create and rotate require an active gateway. There is at most one active credential per gateway; duplicate creation is a conflict.
- Rotation atomically revokes the old key. Revocation is soft/idempotent and remains available on an inactive gateway.
- API keys have at least 256 bits of entropy; only SHA-256 hashes and an 8-character identification prefix persist. Raw keys are returned once at issuance, never retrievable later.
- Device authentication requires both an active gateway and active/unrevoked database credential. Invalid requests do not update LastUsedAt. No configuration-key fallback is permitted.
- The UI only presents credential management to DEVICES_MANAGE users for gateways. One-time keys live only in the open dialog, are discarded on close/account change, and are never saved in browser storage or URLs.
# Admin flight-test rules

An admin may request an independent gateway/UAV flight test, executed by firmware:
reach1m, hold continuously3seconds, land with telemetry confirmation. The backend
queues and records it; it never sends Tello UDP flight commands itself.
Only the current active FarmAdministrator role creates/reads these requests.
Device authentication separately restricts claim/result to the assigned gateway.

Statuses: QUEUED, CLAIMED, COMPLETED, FAILED, RECOVERY_REQUIRED, EXPIRED.
QUEUED expires after120seconds. CLAIMED and RECOVERY_REQUIRED reserve both gateway
and UAV indefinitely; no automatic retry may cause another takeoff. Equipment edits
and mission schedule/start conflict with active reservations. An exact request-key
retry returns the existing test; conflicting result retries are rejected.
COMPLETED requires connection, takeoff, altitude, duration and landing evidence.
A failure before takeoff does not fabricate landing evidence; it may close an
interrupted queued/expired claim as FAILED with takeoff_attempted=false and zero
flight measurements. A failure after takeoff without confirmed landing must remain
RECOVERY_REQUIRED. Existing mission statuses and sensor records are unchanged.

## Geographic boundary rules

Farm/Zone boundaries are optional simple polygons with one exterior ring. Validate longitude/latitude ranges, distinct vertices, positive area and non-intersection. Close the ring before persistence. Boundary coordinates are longitude-first; center fields are calculated as the mean of distinct vertices, matching Turf.centroid. Zone polygons must be covered by the parent polygon when present; shared edges are allowed. A Farm update cannot exclude an existing child boundary. Legacy center-only records remain valid. This feature does not enforce sensor or mission geofences.
# Tello mission readiness

UAV models containing `Tello` (case insensitive) use the ESP32 `mission_flight` planning limits at schedule and start, including scheduled create/update: local coordinates in meters (no GPS conversion), ±100m bounds, whole-centimeter precision, 1–16 waypoints, 1–8 targets, 0.8–1.2m required altitude (nominal 1m), 0–30s hold, and MOVE/HOLD/COLLECT actions. Normalize WAIT to HOLD and COLLECT_SENSOR to COLLECT; omitted action/hold become MOVE/0. Every target maps to COLLECT and every COLLECT has targets. Generic UAV rules are unchanged.

Validate axis movements including automatic return to takeoff origin: zero is allowed, nonzero must be at least 0.2m. Limit routes to 128 steps, counting two markers per waypoint and axis segments up to 1m, including return. This is plan compatibility validation only; firmware owns flight execution, altitude control, return and landing.

Tello device starts require a prior download acknowledgment. Lost start responses/reboots may retry `/downloaded` after RUNNING only when the same gateway's existing DOWNLOADED log proves a prior acknowledgment; this does not append another log or change timestamps/status. Scheduled first acknowledgments and existing idempotent start behavior remain intact.


### Tello local start

Tello missions enter RUNNING through the assigned gateway after its download
acknowledgment. Human start is rejected; scheduling alone never launches the UAV.
This preserves the scheduled download queue until the gateway explicitly starts.

## Scheduled launch with operator confirmation
The web can authorize the assigned gateway to begin a Tello mission without changing
laptop Wi-Fi. This authorization is not a direct flight command: the gateway owns
preflight checks and flight control. Confirmation is tied to the downloaded plan revision
and current device boot session, requires fresh readiness, and is allowed from 10 minutes
before schedule until min(start + 60 seconds, end). No approval, stale session, early
consume or expired window permits flight. An expired mission is cancelled on the next
device readiness report with MISSED_LAUNCH_WINDOW. No automatic late catch-up occurs.
Concurrent consumes serialize with other reservations and transitions; exactly one
returns START_ACCEPTED. A reboot/lost consume response requires reconciliation and must
never replay takeoff. RUNNING indicates accepted execution, not observed physical flight.
While the gateway connects to Tello it cannot receive an immediate web stop command.






User creation (`POST /api/users`) requires one or more distinct supported non-administrator roles in `roles`.
Missing/null, empty, duplicate or unsupported roles are rejected with HTTP 400.
The existing prohibition on creating FarmAdministrator accounts remains.


### Audit remediation recovery rules
- Unknown-time/all-null sensor records are durably archived, never assigned fabricated
  measurement times or included in normal analytics/threshold evaluation.
- Partial sync retries reconsider rejected indexes; accepted outcomes are stable.
- A recovered flight test requires gateway-confirmed landing and unchanged original
  failure evidence before releasing its reservation as FAILED.

### Updated SRS role migration (2026-10-28)

Migration `20261028065024_UpdatedOperationalRoles` adds Technician without converting legacy operator accounts. Legacy role rows and memberships remain inert audit data. Only explicit administrator assignment grants Technician. Downgrade preserves assigned Technician rows; re-upgrade is idempotent. No application database migration has been run by this task.

Engineer/Technician job context and rental-based device scoping remain pending subsequent phases; the current generic farm API intentionally denies these roles. Device credentials and sample administrator provisioning are unchanged.

### Consultation intake and follow-up (Phase 2, partial)
`POST /api/consultations` is anonymous and returns only `{ id, status }` in the data envelope. Contact name/phone, farm location, service need and content are required; email and farm area (hectares) are optional. Submission persists NEW plus history and queues WEB/EMAIL notifications for active Agricultural Engineers atomically.

Agricultural Engineers use `GET /api/consultations?inbox=true` for unassigned summaries or the default list for their own work, with standard paging/search. Summaries omit contact fields. `POST /api/consultations/{id}/claim` atomically assigns the current engineer and transitions NEW to IN_PROGRESS; a competing/repeated claim returns 409. Detail GET and `POST /api/consultations/{id}/follow-ups` require the assigned current engineer. Follow-up body contains required `notes`, `contactResult`, `nextAction`; events append rather than replacing earlier notes. No unrestricted status PATCH exists.

Literal statuses, claim-based routing, field length limits and optional area in hectares are implementation assumptions where SRS does not specify them. Survey/proposal/deposit/submission are described in the later checkpoints below; consultation cancellation/completion remain pending. A submitted consultation grants no farm/account/rental privileges.

### Farm survey revisions (Phase 2, partial)
The assigned Agricultural Engineer records a survey with POST `/api/consultations/{id}/surveys`. Body: `expectedRevision` (0 initially), `farmName`, `farmLocation`, optional `farmArea` in hectares, `monitoringNeeds`, optional `notes`, and 1–100 relational `zones`. Each zone has `name`, optional `crop`, optional `cropAgeDays`, optional `symptoms`, and required `installationLocation`. Optional unknown values stay null; zero crop age is distinct from unknown.

GET on the collection returns paged revision summaries, newest first. GET `/{surveyId}` returns a specific immutable revision. Every save appends a revision and consultation history event in one transaction. A stale expectedRevision returns 409 without overwriting history. Writes require an IN_PROGRESS consultation and its assigned active Engineer; owner identity is not inferred from a public email or phone. Admin/owner contextual survey reads and linkage are described below; survey image uploads remain pending. Existing farm/zone inventory is not created or modified by recording a survey.

Implementation assumptions: survey revisions are snapshots per consultation, area uses hectares, crop age uses days, zone names are unique within a revision. These units and limits are not prescribed literally by the SRS. SRS optional survey images remain an open requirement, not represented by arbitrary external URLs.

### Consultation customer/farm binding and survey sharing
Administrator may read consultation summaries/details through the existing GET endpoints, but cannot claim work, record surveys or add engineer follow-up solely by holding the administrator role. PUT `/api/consultations/{id}/customer-farm` accepts `{ ownerId, farmId }`, requires an active FarmOwner with current membership in that farm, and appends CUSTOMER_BOUND history. Binding is only permitted during IN_PROGRESS. Repeating the same binding is idempotent; changing an existing binding returns 409 to prevent sharing earlier customer records with a different account. This one-time binding is an implementation assumption until a separately authorized correction flow is specified.

GET `/api/farms/{farmId}/survey-consultations` returns paginated linked consultation summaries; owners see only their own linked records and must retain current farm membership. GET consultation survey list/detail permits Admin, assigned Engineer, or explicitly linked Owner with current membership. Owner access does not include internal consultation contact/follow-up details and never permits survey edits. Public email/phone values grant no account or farm access. Admin performs binding in the consultation detail UI; Admin/Owner access shared surveys from the farm page.

Migration `20261028122649_ConsultationCustomerFarm` adds nullable owner/farm references with RESTRICT foreign keys and owner/farm index. Existing requests remain unbound. No application database migration was executed. Survey images and installation review/jobs remain pending; proposal/deposit/submission are described below.

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

### Evidence upload assumptions
Survey and installation report images are optional, immutable per revision and readable only with their parent record. Implementation limits (not SRS-prescribed): five PNG/JPEG images, two MiB each. No SVG, arbitrary external URLs or public storage paths. Backend validates declared MIME, file signatures and base64/size/count; it does not transcode or perform image-content analysis. Owner confirmation and administrator review apply to the exact report including its image evidence.

### Rental assumptions and expiration processing
Statuses DRAFT → ACTIVE → EXPIRED → ENDED are implementation choices for SRS UC14/15, BR30–33. Admin explicitly activates within the agreed period. An externally confirmed extension of ACTIVE/EXPIRED contracts reactivates only with valid current owner/farm/device assignments. DRAFT may be ended with explicit physical return confirmation; no contract/device deletion occurs. Ending requires confirmation for all devices; partial retrieval is not modeled here.
`RentalExpiration` settings: Enabled=true, IntervalSeconds=3600, WarningDays=7, BatchSize=100 by default. Bounds: interval1–86400 seconds, warning0–365 days, batch1–1000. Worker queues WEB/EMAIL notices to eligible linked owner and Admin; repeated processing for the same end date is idempotent. Read status is effectively EXPIRED at end time even before a worker tick. Reserved equipment remains unavailable until retrieval. Mission rental gating and replacement workflow are subsequent required phases, not provided by expiration status alone.

### Rental eligibility for UAV mission operations
The active rental must belong to the mission creator, including validation during gateway download/readiness/start. Farm membership alone does not authorize use of another owner's rental. Only the mission creator may edit, schedule, change status, add notes, or approve launch; other members of the same farm cannot perform these actions. Existing read access is still farm-scoped and requires a separate ownership-scope audit.
Selecting a UAV/gateway in a mission plan, scheduling, starting, confirming a Tello launch, accepting gateway readiness and first download acknowledgment requires unreturned equipment assigned by an ACTIVE rental to the mission farm. Rental must have started, must not be expired at the current time, and must cover the scheduled mission end. Linked rental owner must retain active FarmOwner membership. Incomplete drafts with no selected UAV or gateway may still be saved; they cannot be scheduled or launched without eligible equipment.
Rental changes and expiration processing share the existing flight reservation transaction lock with mission operations. Explicit equipment retrieval is rejected while an affected mission is RUNNING or a flight test is active/recovery-required. Rental expiry does not block cancellation, completion, failure reports, telemetry or offline backlog synchronization; safe terminal/ingestion paths preserve existing validation. Equipment catalog scope/UI filtering remains a separate Phase5 requirement.

### Rental equipment UI and catalog
Owner catalog membership derives from RentalDeviceAssignment and current linked owner/farm access, not prior mission use or arbitrary frontend farm IDs. Frontend planning requires matching active rental farm and current period; changing farm clears chosen equipment. Inventory displays the rental period/status and filters by actual rental farm. Admin retains visibility of unallocated inventory. Mission backend validation remains authoritative for scheduled end and competing reservations.

Operational mission notes are append-only mission-log entries attributed to the current owner and server time. They are accepted for authorized farms even after terminal status, preserving original planning notes, lifecycle state and collection evidence. Text is required and bounded to2000 characters. The dedicated notes endpoint does not bypass mission start/complete rules or synthesize telemetry/results.

Mission images attach to immutable operational-note history and may document observations or terminal outcomes. They do not assert successful collection or change mission status. Only authorized Owner writes and Admin/authorized Owner reads are allowed. UI loads the evidence of an individual note on demand; list/poll responses contain only image counts. Same image signature/size/type validation as survey and installation evidence applies.

Device incident intake uses the rental assignment identity to preserve the device/contract lineage. A returned device cannot receive a new incident via that assignment, but its recorded incident history remains visible under current farm authorization. Expiry alone does not prevent reporting a physically assigned device problem. OPEN is an implementation status assumption. Creation shares the flight/rental advisory lock, so device return and incident eligibility serialize. Admin notifications use the existing transactional delivery queue.

ServiceJob is separate from InstallationJob. MAINTENANCE/REPAIR and ASSIGNED/IN_PROGRESS are implementation literals for initial service scheduling. Admin changes assignment/appointment with optimistic revision; current role and target Technician eligibility are locked before mutation, incident lock serializes duplicate creation. Reassignment revokes the previous Technician's job access immediately and preserves action/actor/technician/appointment/note/time history. Starting work does not assert repair success or change rental/device state. Service reports, Owner confirmation and Admin acceptance follow the service result rules below.

Service report results extend ServiceJob through REPORT_SUBMITTED -> OWNER_CONFIRMED -> CLOSED; Admin return from either review state yields REWORK. These codes implement SRS3.6's submission, operation confirmation and final review steps. Submission requires current assigned Technician and IN_PROGRESS/REWORK. Each report permanently records cause, work, components, explicit pass/fail checks, test details, notes, images and author/time. Only the linked current Owner with farm access confirms the latest submitted version; Admin closure additionally requires both test checks to pass. Failed checks may be submitted and retained for diagnosis/rework but cannot produce successful closure. All writes lock actor/job, validate expectedRevision and atomically store history/notifications. Rework assignment resets ASSIGNED; submitted/confirmed reports cannot be silently reassigned. Returned reports retain original confirmations/decisions and remain readable to current job participants; correction is a new report with fresh confirmation. SRS does not define exact text bounds:4000 for cause/work/components/notes,2000 for tests/decisions are implementation limits. Physical replacement uses the rules below; preventive scheduling remains pending.

### Incident resolution after service approval
A newly reported incident is OPEN. Successful Admin approval of its linked service job marks that incident RESOLVED in the same transaction as the report decision, CLOSED job, audit history and notifications. Confirmation alone, rejected approval and rework do not resolve it. Another incident involving the same device remains independent. OPEN/RESOLVED are implementation literals because SRS3.6 does not prescribe incident status codes. Closure actor/time/note remain in the linked report and job history; there is no arbitrary incident status PATCH or reopening action.

### Replacement lineage and current rental equipment
The assigned Technician proposes and executes an approved service plan. Admin approval of the owner-confirmed report applies any planned physical exchange, preserving the old incident, allocation and rental identities. The successor belongs to the same contract and original installation line. Each exchange retains immutable device codes, assigned Technician, approving Admin, time and note. Reports retain the latest exchange recorded for that report, including an earlier exchange when subsequent service only inspects the device.
Serialize replacement using the shared flight/rental advisory lock before actor/job/contract locks; prohibit old/new equipment in RUNNING missions or active/recovery flight tests, and prohibit target devices already allocated or rented. Include running mission targets when replacing sensors. Require same device kind, active target and same-farm sensors. Preserve unique physical reservations; concurrent reservation failure rolls back old retrieval/deactivation as well as new rows. Increment both job and contract revisions and record histories/notifications in the same transaction.
Operational assumption: retrieved failed equipment becomes inactive, requiring deliberate Admin reactivation after repair. This is not hard deletion and does not remove historical readings or reports. Rental extension/activation validates current unreturned devices; ending a contract checks/retrieves only current devices and preserves earlier ReturnedAt/ReleasedAt values from exchanges. Contract period is unchanged by replacement. Preventive maintenance scheduling remains pending.

### Service work independent of incidents
Every service job has a direct rental device assignment; an incident is optional. Current Owner/farm membership and assigned-Technician access apply equally to incident and independent work. Replacement uses the latest successor assignment or the job's original assignment. Report acceptance and notifications use the contract owner; final approval resolves a linked incident only when present. Preventive work must not fabricate an incident. This establishes the job lifecycle prerequisite; maintenance schedule CRUD and automatic due-job creation remain pending.

### Maintenance schedule administration
Only a current active Administrator manages maintenance schedules. Writes recheck current Admin/Technician roles under user locks and serialize schedule revision changes; concurrent edits at one revision have a single winner. A specific assignment must remain unreturned under a non-ended rental, with active farm/current linked Owner. Device-type schedules are policy configuration; actual device eligibility must be checked when due work is generated. Admin can disable overdue schedules without selecting a new date or an active replacement technician. No hard deletion or incident fabrication. Scheduling does not itself create jobs or notifications yet; the due worker remains pending.

### Maintenance scheduler implementation
The scheduler now creates due work atomically with occurrence records and responsible-user notifications. It uses the shared flight/rental advisory lock, user locks and schedule revision/row lock to serialize with schedule edits, rental end and replacement. Due eligibility is rechecked rather than trusting configuration-time membership. Type schedules expand eligible current assignments; specific scopes follow retained replacement lineage. No eligible targets/inactive Technician leave the original due date unchanged. One current visit per eligible assignment covers missed periods; next date remains anchored to the configured cadence. Occurrence uniqueness plus transaction rollback prevents duplicates. Service history explicitly names automatic scheduling and attributes its configuration to the recorded Admin; the worker does not create a synthetic user or incident. Recipients are deduplicated by user ID, so an Owner who is also the assigned Technician receives one WEB and one EMAIL notification per job. Regression covers forced notification-write rollback and retry, replacement-successor selection, worker disabling and continuation after a failed schedule. Frontend, hosted configuration validation and browser-chain evidence remain pending for full UC29 verification.

### Maintenance editing after replacement
Schedule writes and due processing share the same successor lookup. Keep the original assignment as the schedule lineage root, but validate the current replacement device/contract rather than rejecting an edit because its predecessor was retrieved. Returning the final successor without another replacement still rejects scheduling. A cycle in replacement history rejects processing instead of choosing an arbitrary device. Existing occurrence/job identities and policy snapshots remain unchanged.

### Crop condition intake and visibility
Crop reports are separate from commercial consultation and device incidents. Owner submission requires a current active Owner role and farm membership; inactive farms reject new reports. Optional sensor references must match the selected farm/zone. Images use shared evidence validation. Submission creates immutable SUBMITTED history and Engineer notifications in the same transaction. Owner reads are restricted to the reporter with current membership; Admin monitors all; Engineer sees unclaimed NEW queue entries and own assigned cases. Technician cannot read or submit. Engineer claim, follow-up and resolution actions are not yet implemented. NEW/IN_REVIEW/AWAITING_INFORMATION/RESOLVED remain implementation vocabulary where the SRS does not specify literal status names.

### Human crop consultation processing
Engineer must claim a NEW report before processing it; only the assigned current Engineer may request information, recommend or resolve. Owner reply requires reporter identity plus current Owner role and farm membership, and is accepted only while awaiting information. Responses and evidence append to history; they do not overwrite the original symptoms or earlier advice. Row locks and expected revisions serialize changes; two requests at one revision cannot both append history/notifications. RESOLVED is terminal. Recommendations may include an on-site survey suggestion; this records advice and does not create a commercial survey or technical job. Counterpart notifications contain a report reference/action rather than private consultation text. General sensor APIs remain inaccessible to Engineer; contextual reads remain pending.

Crop sensor context now uses a dedicated read-only paged report endpoint. It reuses report visibility, restricts queries to attached channel IDs and checks their current farm/zone before reading values. Historical attached IDs remain in the report but a channel moved elsewhere contributes no contextual readings. Results include measurement validity/quality to avoid presenting rejected readings as trusted values. This does not widen the Engineer's general farm or sensor permissions.

### Crop season foundation assumptions
SRS3.9/BR40–42 require manual data only: unknown season dates/age/results remain null, not inferred from current time, activity costs or other fields. Initial implementation uses ACTIVE/COMPLETED status tokens, date-only agricultural dates, hectares, age in days, yield kg and VND amounts; units/tokens are explicit assumptions because SRS does not specify them. Known area must be positive; known yield/cost/revenue/age may be zero, never negative. Planned end cannot precede known start. Activity taxonomy is WATERING/FERTILIZING/PESTICIDE/INSPECTION/OTHER with required date/description and optional cost/notes/images. Completion may retain unknown end/results. Contracts/validators/schema exist; application services still must enforce current Owner scope, same-farm zone, lifecycle/revisions and shared image decoding. No active endpoints or UI yet.

A Farm Owner can plan a mission for an entire authorized farm or a single zone on that farm. Zone-specific plans validate all target sensors when saved, scheduled, or started. The purpose is optional for existing plans and independent of operator notes.

### Survey and proposal location preservation

Survey zones retain submitted proposed installation locations and optional nonnegative crop ages. Proposal lines retain submitted installation locations, as required by the updated SRS. These values remain available alongside the published product price snapshot.

### Mission ownership on shared farms

An owner must be the recorded mission creator and a current member of its farm to read a mission, its launch state, results, telemetry or collection attempts. The same scope applies to mission filters, lists, mission reports and dashboard counts. Admin reads across owners. A device support request may only link a mission created by the reporting owner.

### MF-01: pre-survey travel charge

The nullable consultation channel preserves legacy leads. ONLINE is free. For ON_SITE the assigned engineer submits an immutable itinerary, fee and appointment revision. Admin approves it or returns it with a reason. Zero-fee visits still require approval and a separate scheduling decision; no payment row is created. Positive fees require an exact, verified external receipt for the current revision before scheduling. Confirmed receipts and finalized appointments block automatic amendments pending administrator reconciliation. The survey write checks the latest schedule before first-save assignment under the consultation row lock. Site-visit receipts do not confirm rental payments, proposal deposits or installation dossiers. See the MF-01 section in API.md for thresholds, manual quotes above 150 km, permissions, examples and rollout.
