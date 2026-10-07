# ARCHITECTURE.md

## 1. Architectural Style

The backend uses **Clean Architecture implemented with an N-Layer structure**.

The objective is to separate:

- business/domain logic
- application use cases
- infrastructure implementation
- HTTP/API concerns

The backend is not responsible for directly controlling UAV flight.

High-level architecture:

```text
┌──────────────────────────────┐
│ Web Application / Devices    │
│ Admin / Owner / Engineer UI  │
│ Gateway / UAV Companion      │
└──────────────┬───────────────┘
               │ HTTPS / API
               ▼
┌──────────────────────────────┐
│ FarmMonitoring.Api           │
│ Controllers / Middleware     │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ FarmMonitoring.Application   │
│ Use Cases / Services / DTOs  │
└──────────────┬───────────────┘
               │ interfaces
               ▼
┌──────────────────────────────┐
│ FarmMonitoring.Domain        │
│ Entities / Enums / Rules     │
└──────────────────────────────┘

Infrastructure implements Application interfaces:

┌──────────────────────────────┐
│ FarmMonitoring.Infrastructure│
│ EF Core / PostgreSQL / JWT   │
│ Email / Repositories         │
└──────────────┬───────────────┘
               ▼
         PostgreSQL
```

---

## 2. Recommended Solution Structure

```text
backend/
├── src/
│   ├── FarmMonitoring.Api/
│   │   ├── Controllers/
│   │   ├── Contracts/
│   │   ├── Middleware/
│   │   ├── Filters/
│   │   ├── Extensions/
│   │   └── Program.cs
│   │
│   ├── FarmMonitoring.Application/
│   │   ├── Interfaces/
│   │   ├── DTOs/
│   │   ├── Features/
│   │   ├── Validators/
│   │   ├── Mappings/
│   │   └── Common/
│   │
│   ├── FarmMonitoring.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Constants/
│   │   ├── ValueObjects/
│   │   └── Exceptions/
│   │
│   └── FarmMonitoring.Infrastructure/
│       ├── Persistence/
│       │   ├── AppDbContext.cs
│       │   ├── Configurations/
│       │   ├── Repositories/
│       │   └── Migrations/
│       ├── Authentication/
│       ├── Email/
│       ├── BackgroundJobs/
│       └── Services/
│
└── tests/
    ├── FarmMonitoring.UnitTests/
    └── FarmMonitoring.IntegrationTests/
```

---

## 3. Dependency Rules

Allowed dependencies:

```text
Domain
  └── no dependency on other project layers

Application
  └── Domain

Infrastructure
  ├── Application
  └── Domain

Api
  ├── Application
  └── Infrastructure only for dependency injection / startup wiring
```

Forbidden:

```text
Domain → Infrastructure
Domain → Api
Application → Api
Application → concrete PostgreSQL repository implementation
Controller → DbContext directly
```

---

## 4. Main Backend Modules

### 4.1 Authentication and Users

Responsibilities:

- login
- logout
- JWT creation
- refresh token rotation/revocation
- user management
- role assignment
- authorization

Actors:

- FarmAdministrator
- FarmOwner
- FarmEngineer

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

### 4.2 Farm and Zone Management

Responsibilities:

- manage farms
- manage zones
- assign sensor nodes to zones
- keep location metadata

Relationship:

```text
Farm
 └── Zone
      └── SensorNode
```

One farm may contain many zones.

---

### 4.3 Sensor Management

Responsibilities:

- register sensor nodes
- manage sensor channels
- define sensor types
- manage thresholds
- track device state
- ingest sensor measurements
- validate incoming measurements

Model:

```text
SensorNode
 └── SensorChannel
      ├── SensorType
      ├── SensorThreshold
      └── SensorReading
```

One physical node may have many sensor channels.

---

### 4.4 UAV and Gateway Management

Responsibilities:

- register UAVs
- register gateways
- associate UAV and gateway where appropriate
- record status/battery/location metadata
- support gateway sync

Important distinction:

```text
UAV/autopilot
    handles actual flight

Mobile Gateway
    handles sensor communication and offline storage

Backend
    handles planning, monitoring, storage and business logic
```

---

### 4.5 Mission Management

Responsibilities:

- create mission
- select farm
- select target sensor nodes
- define waypoints/flight points
- schedule mission
- assign UAV/gateway
- update mission status
- record mission notes
- monitor progress
- store results

Logical structure:

```text
Mission
├── MissionWaypoint[]
├── MissionTarget[]
├── CollectionAttempt[]
├── MissionLog[]
└── Telemetry[]
```

Mission execution is tracked by the platform, but actual direct UAV flight control is outside backend scope.

---

## 5. Mission Flow

```text
FarmAdministrator / FarmOwner
   │
   ▼
Create Mission
   │
   ├── choose Farm
   ├── choose Sensors
   ├── define Waypoints
   ├── assign UAV/Gateway
   └── schedule time
   │
   ▼
PENDING / SCHEDULED
   │
   ▼
Mission begins
   │
   ▼
RUNNING
   │
   ├── telemetry received
   ├── waypoint progress updated
   ├── sensor collection attempts recorded
   └── operational notes recorded
   │
   ├──────── success ────────► COMPLETED
   │
   ├──────── failure ────────► FAILED
   │
   └──────── cancel ─────────► CANCELLED
```

Backend may receive automatic progress from a gateway/UAV companion or accept manual updates from users authorized by ManageMissions.

---

## 6. Sensor Collection Flow

```text
Sensor Node
   │
   │ local protocol such as Wi-Fi/BLE/LoRa
   ▼
Mobile Gateway
   │
   ├── discover registered sensor
   ├── authenticate sensor
   ├── request/read data
   ├── validate basic payload
   ├── timestamp collection
   └── save offline when Internet unavailable
   │
   ▼
Backend Sync API
   │
   ├── authenticate gateway
   ├── validate batch
   ├── detect duplicates
   ├── persist readings
   ├── persist collection result
   └── trigger alert evaluation
   │
   ▼
PostgreSQL
```

---

## 7. Offline Synchronization Flow

```text
Gateway collects sensor data
        │
        ▼
Internet available?
   ┌────┴─────┐
   │          │
  Yes         No
   │          │
   ▼          ▼
Send       Store locally
batch      on gateway
   │          │
   │          ▼
   │      Retry later
   │          │
   └──────┬───┘
          ▼
POST Sync Batch
          │
          ▼
Backend checks duplicate keys
          │
   ┌──────┴─────────┐
   │                │
new record      duplicate
   │                │
persist          ignore/ack
   └──────┬─────────┘
          ▼
Return per-record result
```

The operation must be idempotent.

---

## 8. Alert Flow

```text
Sensor Reading / Device State / Mission Event
                   │
                   ▼
             Rule Evaluation
                   │
      ┌────────────┼─────────────┐
      │            │             │
 Threshold     Data timeout   Device/Mission
 violation                    issue
      │            │             │
      └────────────┼─────────────┘
                   ▼
                Alert
                   │
          ┌────────┴────────┐
          ▼                 ▼
      Web notification   Email notification
```

Alert lifecycle:

```text
OPEN
  ↓
ACKNOWLEDGED
  ↓
CLOSED
```

The history of handling actions must be retained.

---

## 9. Request Processing Pattern

Recommended pattern:

```text
Controller
   ↓
Application Use Case / Service
   ↓
Validation
   ↓
Repository Interface
   ↓
Infrastructure Repository
   ↓
EF Core
   ↓
PostgreSQL
```

Example:

```text
POST /api/missions
   ↓
MissionsController
   ↓
CreateMissionService
   ↓
CreateMissionValidator
   ↓
IMissionRepository
   ↓
MissionRepository
   ↓
AppDbContext
   ↓
PostgreSQL
```

---

## 10. Read and Write Separation

A full CQRS framework is not required.

However, code should distinguish intent:

```text
Commands:
CreateMission
StartMission
CancelMission
SyncSensorData
AcknowledgeAlert

Queries:
GetMissionById
GetMissionList
GetSensorHistory
GetOpenAlerts
GetDashboardSummary
```

Use simple services/handlers unless the project explicitly adopts MediatR.

---

## 11. Database Access

Only Infrastructure should know EF Core/PostgreSQL details.

Example:

```text
Application:
IMissionRepository

Infrastructure:
MissionRepository : IMissionRepository
```

`AppDbContext` belongs in Infrastructure.

Controllers must never inject `AppDbContext`.

---

## 12. Background Processing

Potential background tasks:

- stale sensor/data-timeout detection
- alert evaluation
- expired refresh token cleanup
- retryable notification processing
- scheduled mission readiness checks

A background job must still call Application-level services instead of duplicating business rules inside Infrastructure.

---

## 13. External Device Integration

Gateway and UAV-side software communicate through APIs.

Recommended separation:

```text
Human APIs
  /api/auth
  /api/farms
  /api/missions
  /api/alerts

Device APIs
  /api/device/gateways/...
  /api/device/telemetry
  /api/device/sync
```

Device Authentication is independent from human JWT login. Existing /api/device/... endpoints require X-Api-Key and X-Gateway-Code; JWT-only requests are rejected.

---

## 14. Deployment View

```text
┌───────────────────┐
│ Web Frontend      │
└─────────┬─────────┘
          │ HTTPS
          ▼
┌───────────────────┐
│ ASP.NET Core API  │
└─────────┬─────────┘
          │ Npgsql
          ▼
┌───────────────────┐
│ PostgreSQL        │
└───────────────────┘

┌───────────────────┐
│ UAV Gateway       │
│ ESP32 / Pi        │
└─────────┬─────────┘
          │ HTTPS
          └────────────► ASP.NET Core API
```

The gateway does not access PostgreSQL directly.

---

## 15. Cross-Cutting Concerns

Centralize:

- validation
- authentication
- authorization
- exception handling
- logging
- response conventions
- pagination
- audit fields
- UTC time handling

Avoid repeating these concerns in every controller.

---

## 16. Scope Boundary

Initial scope includes:

- mission planning
- mission monitoring
- sensor data collection
- sensor data synchronization
- telemetry recording
- threshold alerts
- device monitoring
- reports

Initial scope excludes:

- direct UAV flight control from web backend
- advanced AI
- crop image analysis
- autonomous obstacle avoidance implemented by the backend


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

### Farm access implementation boundaries

`ICurrentUser` is implemented by the API; `FarmAccessService` enforces current database roles and resource-to-farm resolution in Application. `IFarmAccessRepository` and SQL-translatable `ForFarms` query scoping are implemented in Infrastructure. List filtering precedes counting, pagination and aggregation. Device ingestion does not invoke human farm-access checks.
