# DATABASE.md

Flight-test `status` also supports `CANCELLED` (existing varchar mapping; no schema
migration). Administrative cancellation stores `ADMIN_CANCELLED:<userId>:<reason>`
in the existing 100-character `error_code` and uses `reported_at` for its UTC time.
The original requested_by_user_id and nullable device evidence remain unchanged.

## 1. Database Standard

Database engine:

```text
PostgreSQL
```

ORM:

```text
Entity Framework Core + Npgsql
```

Primary-key convention:

```text
int identity
```

Do NOT use UUID primary keys unless explicitly required later.

Database naming convention:

```text
snake_case
```

Examples:

```text
sensor_nodes
sensor_readings
mission_waypoints
created_at
gateway_id
```

---

## 2. General Column Conventions

Recommended common columns:

```text
id              integer identity primary key
created_at      timestamp with time zone
updated_at      timestamp with time zone nullable
```

For soft-deletable configuration/master data, optional:

```text
is_active       boolean
```

Operational history such as readings, mission logs, telemetry, alert history, and collection attempts should normally not be hard deleted.

---

## 3. Core Database Tables

The current baseline contains 24 tables, including the new `user_farms` assignment table.

### Identity and access

Migration `PasswordRecovery` adds `users.password_version` (integer, default 0), nullable `password_reset_hash` (varchar 64, unique index), `password_reset_expires_at` and `password_reset_requested_at` (timestamptz), and `refresh_tokens.password_version` (integer, default 0). It preserves all existing accounts/password hashes. The current reset token hash is stored on its user; no plaintext token/password is persisted. Password replacement locks the user row, increments its version, clears reset credentials and revokes refresh tokens in one transaction. The version is also an EF concurrency token to prevent a concurrent login rehash overwriting a password change.

1. `users`
2. `roles`
3. `user_roles`
4. `refresh_tokens`

Farm access additionally uses `user_farms` (section 5.24).

### Farm and sensor management

5. `farms`
6. `zones`
7. `sensor_nodes`
8. `sensor_types`
9. `sensor_channels`
10. `sensor_thresholds`
11. `sensor_readings`

### UAV and gateway

12. `uavs`
13. `gateways`

### Mission management

14. `missions`
15. `mission_waypoints`
16. `mission_targets`
17. `collection_attempts`
18. `mission_logs`
19. `telemetry_records`

### Synchronization

20. `sync_batches`

### Alerts and notification

21. `alerts`
22. `alert_histories`
23. `notifications`

---

## 4. Relationship Overview

```text
roles
  â””â”€â”€< user_roles >â”€â”€ users
                       â””â”€â”€< refresh_tokens

farms
  â””â”€â”€< zones
       â””â”€â”€< sensor_nodes
             â””â”€â”€< sensor_channels >â”€â”€ sensor_types
                    â”œâ”€â”€< sensor_thresholds
                    â””â”€â”€< sensor_readings

uavs
  â””â”€â”€< missions

gateways
  â””â”€â”€< missions

missions
  â”œâ”€â”€< mission_waypoints
  â”œâ”€â”€< mission_targets >â”€â”€ sensor_nodes
  â”œâ”€â”€< collection_attempts
  â”œâ”€â”€< mission_logs
  â””â”€â”€< telemetry_records

gateways
  â””â”€â”€< sync_batches

alerts
  â””â”€â”€< alert_histories

alerts
  â””â”€â”€< notifications
```

---

## 5. Table Definitions

## 5.1 users

Purpose: human account.

Suggested columns:

```text
id                  int PK identity
email               varchar(255) UNIQUE NOT NULL
password_hash       text NOT NULL
full_name           varchar(150) NOT NULL
is_active           boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

Indexes:

```text
UNIQUE(email)
```

---

## 5.2 roles

Supported human role values:

```text
FarmAdministrator
FarmOwner
FarmEngineer
Technician
```

Columns:

```text
id                  int PK identity
name                varchar(100) UNIQUE NOT NULL
description         varchar(255) NULL
```

---

Migration `20261007041338_HumanUserRoles` removes memberships referencing the deprecated `UavDeviceOperator` role by name, deletes that role, and inserts missing FarmOwner/FarmEngineer roles. Users and password hashes are preserved; no account is automatically reassigned. Role IDs are internal identifiers, not an authorization contract. Existing target roles and their memberships are preserved. Rolling back cannot recover removed role assignments.

An account with no supported role cannot log in, refresh or access `/api/auth/me` until an administrator assigns a valid role. Protected business policies read current database roles rather than trusting stale JWT roles.

FarmAdministrator has global farm access without membership. FarmOwner requires a current `user_farms` assignment to access a farm. Engineer and Technician require contextual workflow authorization; farm membership alone grants neither role general farm access. Roles still come exclusively from `user_roles`/`roles`.

Human users use JWT Bearer; gateway/device communication uses X-Api-Key + X-Gateway-Code independently. See API.md for the six-policy authorization matrix.

## 5.3 user_roles

Many-to-many users and roles.

```text
id                  int PK identity
user_id             int FK users(id) NOT NULL
role_id             int FK roles(id) NOT NULL
```

Constraint:

```text
UNIQUE(user_id, role_id)
```

---

## 5.4 refresh_tokens

Purpose: refresh-token rotation and revocation.

```text
id                  int PK identity
user_id             int FK users(id) NOT NULL
token_hash          text NOT NULL
expires_at          timestamptz NOT NULL
created_at          timestamptz NOT NULL
revoked_at          timestamptz NULL
replaced_by_token_id int FK refresh_tokens(id) NULL
created_by_ip       varchar(64) NULL
revoked_by_ip       varchar(64) NULL
```

Never store plaintext refresh tokens if avoidable.

---

## 5.5 farms

```text
id                  int PK identity
name                varchar(150) NOT NULL
description         text NULL
latitude            numeric(10,7) NULL
longitude           numeric(10,7) NULL
is_active           boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

GPS fields are nullable because prototype/demo operation may also rely on local/reference coordinates.

---

## 5.6 zones

```text
id                  int PK identity
farm_id             int FK farms(id) NOT NULL
name                varchar(150) NOT NULL
description         text NULL
center_latitude     numeric(10,7) NULL
center_longitude    numeric(10,7) NULL
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

Constraint:

```text
UNIQUE(farm_id, name)
```

---

## 5.7 sensor_nodes

Represents one physical sensor device.

One node may expose many measurement channels.

```text
id                  int PK identity
zone_id             int FK zones(id) NOT NULL
device_code         varchar(100) UNIQUE NOT NULL
name                varchar(150) NOT NULL
status              varchar(50) NOT NULL
latitude            numeric(10,7) NULL
longitude           numeric(10,7) NULL
local_x             numeric(10,3) NULL
local_y             numeric(10,3) NULL
last_seen_at        timestamptz NULL
battery_percent     numeric(5,2) NULL
is_active           boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

Use GPS or local coordinates depending on prototype mode.

---

## 5.8 sensor_types

Represents measurement type.

Example records:

```text
air_temperature
air_humidity
soil_moisture
light_intensity
ph
water_level
```

Columns:

```text
id                  int PK identity
code                varchar(100) UNIQUE NOT NULL
name                varchar(150) NOT NULL
unit                varchar(50) NULL
description         varchar(255) NULL
```

---

## 5.9 sensor_channels

Represents one measurable channel on a sensor node.

```text
id                  int PK identity
sensor_node_id      int FK sensor_nodes(id) NOT NULL
sensor_type_id      int FK sensor_types(id) NOT NULL
channel_code        varchar(100) NOT NULL
name                varchar(150) NULL
is_active           boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

Constraint:

```text
UNIQUE(sensor_node_id, channel_code)
```

---

## 5.10 sensor_thresholds

```text
id                  int PK identity
sensor_channel_id   int FK sensor_channels(id) NOT NULL
min_value           numeric(18,6) NULL
max_value           numeric(18,6) NULL
data_timeout_minutes int NULL
low_battery_percent numeric(5,2) NULL
is_enabled          boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

Validation:

```text
min_value <= max_value
```

when both are not null.

---

## 5.11 sensor_readings

High-volume measurement table.

```text
id                  bigint identity PK
sensor_channel_id   int FK sensor_channels(id) NOT NULL
gateway_id          int FK gateways(id) NULL
mission_id          int FK missions(id) NULL
source_record_key   varchar(150) NOT NULL
value               numeric(18,6) NOT NULL
measured_at         timestamptz NOT NULL
collected_at        timestamptz NOT NULL
received_at         timestamptz NOT NULL
quality_status      varchar(50) NULL
is_valid            boolean NOT NULL default true
validation_error    varchar(500) NULL
```

Although project-wide IDs use integer identity, this high-volume table may use `bigint` if desired. If strict uniformity is required, change it back to `int`.

Critical duplicate-prevention constraint:

```text
UNIQUE(sensor_channel_id, source_record_key)
```

Alternative deterministic unique key may be used if gateway cannot generate a source key.

Recommended indexes:

```text
(sensor_channel_id, measured_at DESC)
(mission_id)
(gateway_id)
```

---

## 5.12 uavs

```text
id                  int PK identity
code                varchar(100) UNIQUE NOT NULL
name                varchar(150) NOT NULL
model               varchar(150) NULL
status              varchar(50) NOT NULL
battery_percent     numeric(5,2) NULL
last_seen_at        timestamptz NULL
is_active           boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

---

## 5.13 gateways

Represents mobile sensor collection gateway.

Examples:

- ESP32
- Raspberry Pi

```text
id                  int PK identity
code                varchar(100) UNIQUE NOT NULL
name                varchar(150) NOT NULL
gateway_type        varchar(50) NOT NULL
status              varchar(50) NOT NULL
uav_id              int FK uavs(id) NULL
last_seen_at        timestamptz NULL
battery_percent     numeric(5,2) NULL
firmware_version    varchar(100) NULL
is_active           boolean NOT NULL default true
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

`uav_id` may be nullable because a gateway can be tested independently from a UAV.

---

## 5.14 missions

```text
id                  int PK identity
farm_id             int FK farms(id) NOT NULL
uav_id              int FK uavs(id) NULL
gateway_id          int FK gateways(id) NULL
name                varchar(150) NOT NULL
status              varchar(50) NOT NULL
scheduled_start_at  timestamptz NULL
started_at          timestamptz NULL
completed_at        timestamptz NULL
created_by_user_id  int FK users(id) NOT NULL
operator_notes      text NULL
failure_reason      text NULL
created_at          timestamptz NOT NULL
updated_at          timestamptz NULL
```

Status baseline:

```text
PENDING
SCHEDULED
RUNNING
COMPLETED
FAILED
CANCELLED
```

---

## 5.15 mission_waypoints

```text
id                  int PK identity
mission_id          int FK missions(id) NOT NULL
sequence_no         int NOT NULL
latitude            numeric(10,7) NULL
longitude           numeric(10,7) NULL
local_x             numeric(10,3) NULL
local_y             numeric(10,3) NULL
altitude_m          numeric(10,3) NULL
action_type         varchar(50) NULL
planned_hold_seconds int NULL
```

Constraint:

```text
UNIQUE(mission_id, sequence_no)
```

At least one coordinate system must be usable by the implementation.

---

## 5.16 mission_targets

Maps missions to selected sensor nodes.

```text
id                  int PK identity
mission_id          int FK missions(id) NOT NULL
sensor_node_id      int FK sensor_nodes(id) NOT NULL
waypoint_id         int FK mission_waypoints(id) NULL
sequence_no         int NULL
status              varchar(50) NOT NULL
```

Constraint:

```text
UNIQUE(mission_id, sensor_node_id)
```

Suggested target statuses:

```text
PENDING
COLLECTED
FAILED
SKIPPED
```

---

## 5.17 collection_attempts

Stores each attempt to collect from a sensor.

```text
id                  int PK identity
mission_id          int FK missions(id) NOT NULL
mission_target_id   int FK mission_targets(id) NOT NULL
gateway_id          int FK gateways(id) NULL
attempt_no          int NOT NULL
started_at          timestamptz NOT NULL
finished_at         timestamptz NULL
status              varchar(50) NOT NULL
records_received    int NOT NULL default 0
error_code          varchar(100) NULL
error_message       varchar(500) NULL
```

Constraint:

```text
UNIQUE(mission_target_id, attempt_no)
```

---

## 5.18 mission_logs

Operational audit/log entries.

```text
id                  int PK identity
mission_id          int FK missions(id) NOT NULL
user_id             int FK users(id) NULL
gateway_id          int FK gateways(id) NULL
log_type            varchar(50) NOT NULL
message             text NOT NULL
created_at          timestamptz NOT NULL
```

---

## 5.19 telemetry_records

Telemetry is optional and recorded when available.

```text
id                  bigint identity PK
mission_id          int FK missions(id) NOT NULL
uav_id              int FK uavs(id) NULL
gateway_id          int FK gateways(id) NULL
recorded_at         timestamptz NOT NULL
latitude            numeric(10,7) NULL
longitude           numeric(10,7) NULL
local_x             numeric(10,3) NULL
local_y             numeric(10,3) NULL
altitude_m          numeric(10,3) NULL
battery_percent     numeric(5,2) NULL
current_waypoint_no int NULL
flight_status       varchar(50) NULL
```

Index:

```text
(mission_id, recorded_at DESC)
```

---

## 5.20 sync_batches

Represents one offline synchronization batch.

```text
id                  int PK identity
gateway_id          int FK gateways(id) NOT NULL
mission_id          int FK missions(id) NULL
batch_key           varchar(150) NOT NULL
record_count        int NOT NULL
accepted_count      int NOT NULL default 0
duplicate_count     int NOT NULL default 0
rejected_count      int NOT NULL default 0
status              varchar(50) NOT NULL
started_at          timestamptz NOT NULL
completed_at        timestamptz NULL
error_message       text NULL
```

Constraint:

```text
UNIQUE(gateway_id, batch_key)
```

This helps guarantee idempotent retry behavior.

---

## 5.21 alerts

An alert may relate to a sensor, gateway, UAV, or mission.

```text
id                  int PK identity
alert_type          varchar(100) NOT NULL
severity            varchar(50) NOT NULL
status              varchar(50) NOT NULL
sensor_node_id      int FK sensor_nodes(id) NULL
sensor_channel_id   int FK sensor_channels(id) NULL
gateway_id          int FK gateways(id) NULL
uav_id              int FK uavs(id) NULL
mission_id          int FK missions(id) NULL
message             text NOT NULL
triggered_value     numeric(18,6) NULL
opened_at           timestamptz NOT NULL
acknowledged_at     timestamptz NULL
closed_at           timestamptz NULL
```

Important rule:

A `GATEWAY_ERROR` alert must include the related `gateway_id`.

Status:

```text
OPEN
ACKNOWLEDGED
CLOSED
```

Possible types:

```text
SENSOR_THRESHOLD
SENSOR_DATA_TIMEOUT
SENSOR_LOW_BATTERY
GATEWAY_ERROR
GATEWAY_OFFLINE
UAV_LOW_BATTERY
MISSION_ERROR
```

---

## 5.22 alert_histories

Stores alert handling history.

```text
id                  int PK identity
alert_id            int FK alerts(id) NOT NULL
user_id             int FK users(id) NULL
action              varchar(50) NOT NULL
note                text NULL
created_at          timestamptz NOT NULL
```

Actions:

```text
CREATED
ACKNOWLEDGED
NOTE_ADDED
CLOSED
REOPENED
```

---

## 5.23 notifications

```text
id                  int PK identity
alert_id            int FK alerts(id) NULL
user_id             int FK users(id) NOT NULL
channel             varchar(50) NOT NULL
subject             varchar(255) NULL
message             text NOT NULL
status              varchar(50) NOT NULL
sent_at             timestamptz NULL
error_message       text NULL
created_at          timestamptz NOT NULL
```

Channels:

```text
WEB
EMAIL
```

---

## 5.24 user_farms

Many-to-many human user assignments to farms. No farm-specific role is stored here.

```text
id          int identity primary key
user_id     int NOT NULL FK users(id) ON DELETE RESTRICT
farm_id     int NOT NULL FK farms(id) ON DELETE RESTRICT
created_at  timestamptz NOT NULL (UTC)
```

Indexes: unique `(user_id, farm_id)` and nonunique `(farm_id)` for membership queries. The application controls eligibility; a stale membership cannot substitute for a current eligible global role.

Migration `20261007094620_AddUserFarmAssignments` adds only the table, keys and indexes and updates the generated EF snapshot. It does not insert assignments or modify users, roles, farms or prior migrations. Migration verification covers upgrades with existing users/farms, uniqueness, foreign keys and empty initial membership data.

## 6. Delete Behavior

Recommended behavior:

```text
users                RESTRICT when referenced by history
farms                RESTRICT
zones                RESTRICT
sensor_nodes         RESTRICT
sensor_channels      RESTRICT
uavs                 RESTRICT
gateways             RESTRICT
missions             RESTRICT
```

Avoid cascade-delete of:

```text
sensor_readings
telemetry_records
collection_attempts
mission_logs
alerts
alert_histories
sync_batches
```

Historical records are part of the project's monitoring/reporting value.

---

## 7. Important Indexes

At minimum:

```text
users(email)
user_farms(user_id, farm_id) UNIQUE
user_farms(farm_id)

sensor_nodes(device_code)
sensor_nodes(zone_id)

sensor_channels(sensor_node_id)
sensor_channels(sensor_type_id)

sensor_readings(sensor_channel_id, measured_at)
sensor_readings(mission_id)
sensor_readings(gateway_id)

missions(farm_id)
missions(status)
missions(scheduled_start_at)

mission_waypoints(mission_id, sequence_no)
mission_targets(mission_id)

telemetry_records(mission_id, recorded_at)

sync_batches(gateway_id, batch_key)

alerts(status)
alerts(alert_type)
alerts(sensor_node_id)
alerts(gateway_id)
alerts(mission_id)

notifications(user_id, status)
```

---

## 8. Data Integrity Rules

Enforce:

```text
battery_percent between 0 and 100
min threshold <= max threshold
mission waypoint sequence unique inside mission
mission target unique inside mission
refresh token belongs to exactly one user
sync batch key unique per gateway
sensor source record key unique per channel
```

---

## 9. PostgreSQL and EF Core Notes

Use explicit PostgreSQL-friendly types.

Preferred examples:

```text
text
varchar(n)
boolean
integer
bigint
numeric(p,s)
timestamp with time zone
```

For enum-like statuses, either:

1. store strings for readability, or
2. map PostgreSQL enums if the project intentionally adopts them.

For this capstone, string-backed enum columns are simpler to maintain.

---

## 10. Migration Rules

Each schema change must:

1. update Domain entity if needed
2. update EF Core configuration
3. add migration
4. review generated migration
5. update this file
6. add/update tests

Never edit production schema manually without also creating the matching migration.


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

UAV/Gateway entities are not directly farm-owned in the current schema. Direct per-farm device authorization requires a future explicit device-to-farm assignment model.

Direct UAV/Gateway catalog APIs retain existing global role permissions. Farm-scoped device reports/dashboard counts include only equipment referenced by missions in accessible farms (or the requested authorized farm); this is a mission association, not device ownership. An administrator's unfiltered aggregate remains global. Sensor-type definitions are a global catalog.

Gateway/device authentication remains X-Api-Key + X-Gateway-Code. `user_farms` is only for human users and is never applied to device identity or required for telemetry/sync ingestion. Human telemetry reads require mission-farm access.

Response conventions: 401 without valid authentication; 403 for denied role/farm access; 404 for nonexistent resources; 409 for duplicate membership; 400 for invalid request data; 422 for ineligible membership targets.

Migration `20261013014617_SeedSampleAdministrator` inserts the sample admin@sep.local account with an Identity-compatible password hash and grants FarmAdministrator by role name. An existing email is left unchanged. Rollback preserves the account; there are no schema changes or farm assignments.

## Persistent gateway credentials

Migration `20261013021208_GatewayCredentials` adds `gateway_credentials`:

| Column | Storage |
|---|---|
| id | integer identity primary key |
| gateway_id | required FK to gateways, restrictive delete |
| key_hash | required 64-character SHA-256 hex; database validates hex format |
| key_prefix | nullable 8-character identifier |
| created_at | required timestamp with time zone |
| last_used_at | nullable timestamp with time zone, successful authentication only |
| revoked_at | nullable timestamp with time zone |
| is_active | required boolean |
| created_by_user_id | nullable FK to users, restrictive delete |

A unique filtered index on gateway_id WHERE is_active enforces at most one active key. A state check requires active credentials to have no revocation timestamp and inactive credentials to have one. The (gateway_id, created_at) index supports history; created_by_user_id is indexed. Raw API keys are never stored. Rotation revokes the old row and inserts the new hash in one transaction; revoke preserves all rows. Gateway row locks serialize create/rotate/revoke and device authentication. The migration preserves existing gateways and issues no credentials automatically; rollback removes the credential table/history.
# Flight-test persistence

Migrations `AdminFlightTests` and `FlightTestTakeoffEvidence` add `flight_tests`.
Rows retain the gateway/UAV/requesting-user foreign keys, unique(gateway_id,request_key),
status, creation/expiry/claim/report timestamps and nullable device evidence:
gateway_connected, uav_connected, landed, takeoff_attempted, observed_altitude_cm,
held_ms, error_code. Foreign keys restrict deletion of referenced operational history.
Gateway/status and UAV/status indexes support reservation checks. PostgreSQL transaction
advisory lock74611001 serializes flight-test create/claim/result, mission transitions,
and equipment writes, preventing conflicting reservations across different gateways
that share a UAV. This is a coarse lock appropriate to the short transactions here;
no HTTP/device I/O runs while it is held.

## Geographic boundaries

Migration `20261020085024_GeographicBoundaries` adds nullable text column `boundary_geo_json` to `farms` and `zones`. Values are validated GeoJSON Polygon objects serialized by Application; no PostGIS extension is required. Existing rows remain null and existing center columns are unchanged. Apply from the backend directory with:

```powershell
dotnet ef database update --project src/FarmMonitoring.Infrastructure --startup-project src/FarmMonitoring.Api
```

Stop a running Debug API before building/applying, or specify `--configuration Release` to avoid locked Debug binaries. Connection configuration follows AppDbContextFactory (appsettings/environment/Development user secrets). Migration generation and integration tests do not update the user's application database.

## ScheduledMissionLaunch migration
Adds missions.plan_revision (integer, existing rows default 0), launch_session
(nullable varchar(32)), launch_ready_at, launch_approved_at, launch_consumed_at
(nullable UTC timestamptz). Revision increments and launch fields clear on plan/schedule
changes or returning to pending. Session changes clear approval. LAUNCH_APPROVED logs
retain the author/time; LAUNCH_CONSUMED logs retain gateway/time. Consumption and RUNNING
commit atomically under the existing reservation transaction. No mission/history is deleted.




### Archived unresolved sensor records
Migration `ArchiveUnresolvedSensorRecords` adds `archived_sensor_records` with integer
identity, restrictive FKs to node/gateway/optional mission, unique (sensor_node_id,
source_record_key), original JSONB payload, and server received_at. It does not change
existing readings or the sample administrator. Migration files were generated only;
no database migration was applied during this remediation.

### Updated SRS role migration (2026-10-28)

Migration `20261028065024_UpdatedOperationalRoles` adds Technician without converting legacy operator accounts. Legacy role rows and memberships remain inert audit data. Only explicit administrator assignment grants Technician. Downgrade preserves assigned Technician rows; re-upgrade is idempotent. No application database migration has been run by this task.

Engineer/Technician job context and rental-based device scoping remain pending subsequent phases; the current generic farm API intentionally denies these roles. Device credentials and sample administrator provisioning are unchanged.

ConsultationRequests adds consultations and consultation_history with integer identity keys, bounded fields, UTC event times, optional engineer FK, scoped queue index and RESTRICT foreign keys. Notifications are stored in the existing table and email delivery reuses the existing worker. No application database migration was executed.

Migration 20261028121947_FarmSurveyRevisions creates farm_surveys and survey_zones. Consultation/revision is unique; zone rows belong to a specific immutable revision, with RESTRICT foreign keys and nullable crop age/area. Survey creation serializes on the consultation row; current engineer eligibility is checked under the user-row lock. No application database migration was executed.

20261028122649_ConsultationCustomerFarm: consultations.owner_id and farm_id are nullable RESTRICT FKs to users/farms; owner_id/farm_id composite index supports scoped retrieval. Existing records remain unbound. Customer binding is recorded in consultation_history, never inferred from public contact data.
`20261028165531_DeviceProposalRevisions` adds device_proposals, proposal_lines and installation_requests, with RESTRICT foreign keys, unique consultation/revision and request/proposal constraints. Confirmation actor/time/evidence persist on each proposal revision. Installation submission has its own persisted status and administrator notification transaction.
`20261028170444_InstallationRequestReview` adds nullable reviewer FK (RESTRICT), reviewed_at UTC and review_note to installation_requests. Existing requests remain unreviewed; no historical rows are rewritten and no application database migration was run.
`20261028172133_InstallationJobAssignments` creates installation_jobs and installation_job_history, integer keys, UTC appointment/event times, unique installation_request_id, technician/status/schedule index and RESTRICT foreign keys. Assignment/start history is append-only. No application database migration has been run.
`20261028173338_InstallationReportVersions` adds installation_reports and installed_device_checks, report revision uniqueness, one typed asset FK per check (database constraint), unique per-report physical devices and RESTRICT foreign keys. Expands job history notes from 2000 to 4000 to retain report reference plus decision note. No app DB migrated; rollback narrows notes and removes new report tables, so it is not safe after retaining production report data.
`20261029005316_InstallationDeviceAllocations` adds typed installation asset allocation rows with registered code/name snapshots, actor/time, release time, exactly-one-device check and filtered unique indexes for active sensor/UAV/gateway reservations. All foreign keys RESTRICT. Replacement releases prior rows instead of deleting history; approved-installation reservations persist until the rental return/replacement lifecycle is implemented.

### Workflow evidence images
Migration `20261029011251_WorkflowEvidenceImages` adds `evidence_images`: integer ID, exactly one nullable survey/report FK, PNG/JPEG content type, bytea content (4..2097152 bytes). Both parent FKs use RESTRICT and indexed lookup. Saves share the parent transaction; old revision images remain unchanged. Bounded images are stored in PostgreSQL to avoid orphan files and storage/DB transaction drift; backups and storage capacity must include this data. No application database migration was executed.

### Rental lifecycle
Migration `20261029054949_RentalContracts` adds rental_contracts, rental_device_assignments and rental_history. One contract per installation job; period end must exceed start. Typed device assignments retain installation allocation, code/name/location and return timestamp. Filtered unique physical-device indexes prevent simultaneous unreturned rental assignments. RESTRICT FKs preserve history. Expiry/warnings and lifecycle changes use contract row locks; warning dedup is per confirmed end date. No application database migration has been executed.

Migration `20261029111406_MissionNoteEvidence` adds nullable `evidence_images.mission_log_id`, indexed RESTRICT FK to `mission_logs`, and extends the exactly-one-parent constraint to survey/report/mission log. Existing survey/report data remain valid; no data rewrite. Stored image content limits and MIME constraints are unchanged. Downgrade cannot restore the old parent constraint while mission-only image rows exist, so it fails transactionally instead of deleting evidence. No application database migration was run.

Migration20261029113616_DeviceIncidents adds device_incidents referencing farm, rental_device_assignment, reporter and optional mission with RESTRICT FKs and farm/id index. EvidenceImage gains incident_id and extends exactly-one-parent validation; image size/type constraints remain unchanged. No application database was migrated.

Migration20261029114933_ServiceJobAssignments adds service_jobs and service_job_history with incident, technician and actor RESTRICT FKs, unique incident job, technician/status/appointment index and immutable assignment/start history. No application database migration applied. Current incident-backed service jobs do not yet represent independent preventive schedule occurrences; a later migration will add that required source relationship.

Migration `20261029165058_ServiceReportVersions` creates service_reports with integer identity, unique (job_id,revision), bounded evidence fields, explicit connectivity/operation booleans, submission/confirmation/review actor and UTC times. All report relationships use RESTRICT. EvidenceImage adds indexed nullable service_report_id and extends exactly-one-parent validation; existing image content limits remain unchanged. Each new report is immutable work evidence; review fields are set once by guarded transitions. Downgrade removes report data and cannot restore the earlier image constraint while service-only images exist; do not use downgrade to retain production history. No application database migration applied.

### Service device replacement history
Migration `20261030070236_ServiceDeviceReplacements` adds `service_device_replacements` with RESTRICT FKs to ServiceJob, old/new RentalDeviceAssignment and Technician. Unique old/new assignment indexes prevent branching or reuse of the same successor assignment; `(job_id,id)` supports ordered history. `service_reports.replacement_id` is nullable and RESTRICT-linked to the replacement captured at submission. Existing live allocation/rental partial unique indexes continue to enforce physical device reservation. Old assignment/allocation IDs and timestamps remain historical records. No application database was migrated by this task.

### Independent service job assignments
Migration `20261030071640_IndependentServiceJobAssignments` makes `service_jobs.incident_id` nullable and adds required indexed `rental_device_assignment_id` with a RESTRICT FK. It backfills existing jobs from their incident assignment before enforcing NOT NULL, retaining status, revision and history. Multiple jobs without incidents are allowed; linked incidents retain their unique job constraint. Downgrade refuses to proceed while independent jobs exist, avoiding deletion or fabricated incident links. Applied only to disposable test databases; application database remains unchanged.

### Maintenance schedules and occurrences (foundation)
Migration `20261030123842_MaintenanceSchedules` adds schedules with exactly one scope (rental assignment or SENSOR/GATEWAY/UAV type), service policy, interval in days, next UTC due date, responsible Technician, enabled flag, revision and creator/updater timestamps. Database checks enforce scope and interval. Occurrences retain schedule, due date, assignment, generated service job and policy snapshot. Unique `(schedule_id,due_at,rental_device_assignment_id)` and unique `job_id` constrain duplicate work; all FKs are RESTRICT. Disabling or editing a schedule must retain occurrences. Downgrade drops these tables and their data; do not use it to preserve operational history. Application database has not been migrated. CRUD, due worker and UI are still pending; schema alone does not activate maintenance scheduling.

### Crop condition support foundation
Migration `20261101100505_CropConditionSupport` adds crop_condition_reports, crop_consultation_updates and crop_condition_sensors with integer keys, bounded text, UTC timestamps and RESTRICT relationships. Sensor links use a composite key to prevent duplicate channels per report. Evidence images can belong to either a crop report or a consultation update; the existing exactly-one-parent constraint and size/type limits apply across all workflows. Downgrade refuses while crop reports exist to protect communication/evidence history. No application database migration applied.
This is a schema foundation, not an available API. State tokens NEW/IN_REVIEW/AWAITING_INFORMATION/RESOLVED and action tokens are implementation assumptions where SRS3.7 does not prescribe literal values. Intake limits200 characters for crop,4000 for symptoms/communication,50 distinct channel references reuse bounded conventions; authorization and same-farm/zone validation must be enforced in upcoming repository/API code. No AI diagnosis is implemented.
