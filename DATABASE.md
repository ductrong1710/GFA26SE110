# DATABASE.md

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

The current baseline contains 23 tables.

### Identity and access

1. `users`
2. `roles`
3. `user_roles`
4. `refresh_tokens`

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
  └──< user_roles >── users
                       └──< refresh_tokens

farms
  └──< zones
       └──< sensor_nodes
             └──< sensor_channels >── sensor_types
                    ├──< sensor_thresholds
                    └──< sensor_readings

uavs
  └──< missions

gateways
  └──< missions

missions
  ├──< mission_waypoints
  ├──< mission_targets >── sensor_nodes
  ├──< collection_attempts
  ├──< mission_logs
  └──< telemetry_records

gateways
  └──< sync_batches

alerts
  └──< alert_histories

alerts
  └──< notifications
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

Suggested values:

```text
FarmAdministrator
UavDeviceOperator
```

Columns:

```text
id                  int PK identity
name                varchar(100) UNIQUE NOT NULL
description         varchar(255) NULL
```

---

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
