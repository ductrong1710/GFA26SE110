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
│ Admin UI / Operator UI       │
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

- Farm Administrator
- UAV and Device Operator

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
Operator
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

Backend may receive automatic progress from a gateway/UAV companion or accept manual operator updates.

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

Device authentication should be independent from human JWT login when possible.

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
