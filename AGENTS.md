# AGENTS.md

## 1. Purpose

This file is the primary instruction file for AI coding agents working inside the backend folder.

The backend belongs to the capstone project:

**UAV-Assisted IoT Platform for Farm Monitoring and Sensor Data Collection**

The system supports FarmAdministrator, FarmOwner and FarmEngineer users in managing farms, sensor nodes, UAV/mobile gateways, missions, sensor data, alerts, telemetry, and offline synchronization.

The backend MUST follow the rules in this file unless a task explicitly overrides them.

---

## 2. Backend Technology Stack

- Platform: **.NET / ASP.NET Core**
- Architecture: **Clean Architecture implemented as N-Layer Architecture**
- Database: **PostgreSQL**
- ORM: **Entity Framework Core**
- PostgreSQL provider: **Npgsql.EntityFrameworkCore.PostgreSQL**
- API style: **REST API**
- Authentication: **JWT access token + refresh token**
- API documentation: **Swagger / OpenAPI**
- Validation: prefer **FluentValidation** or equivalent centralized validation
- Logging: use ASP.NET Core logging abstractions
- Serialization: System.Text.Json unless project already uses another configured serializer

Do not change the project's target .NET version unless explicitly requested.
Read the `.csproj` files before adding packages and keep package versions compatible with the existing target framework.

---

## 3. Clean Architecture / N-Layer Structure

Recommended solution structure:

```text
backend/
├── src/
│   ├── FarmMonitoring.Api/
│   ├── FarmMonitoring.Application/
│   ├── FarmMonitoring.Domain/
│   └── FarmMonitoring.Infrastructure/
├── tests/
│   ├── FarmMonitoring.UnitTests/
│   └── FarmMonitoring.IntegrationTests/
├── AGENTS.md
├── ARCHITECTURE.md
├── DATABASE.md
├── API.md
└── BUSINESS_RULES.md
```

Dependency direction:

```text
Api
 ↓
Application
 ↓
Domain

Infrastructure
 ├── depends on Application
 └── depends on Domain
```

The Domain layer MUST NOT depend on Api, Infrastructure, EF Core, PostgreSQL, HTTP, or external SDKs.

---

## 4. Layer Responsibilities

### 4.1 Domain

Contains core business objects and domain rules.

Typical contents:

```text
Domain/
├── Entities/
├── Enums/
├── ValueObjects/
├── Constants/
└── Exceptions/
```

Domain entities should represent concepts such as:

- User
- Role
- Farm
- Zone
- SensorNode
- SensorChannel
- SensorType
- SensorReading
- SensorThreshold
- UAV
- Gateway
- Mission
- MissionWaypoint
- MissionTarget
- CollectionAttempt
- Telemetry
- SyncBatch
- Alert
- Notification

Do not place controller DTOs or EF migrations in Domain.

### 4.2 Application

Contains application use cases and business orchestration.

Typical contents:

```text
Application/
├── Interfaces/
├── DTOs/
├── Features/
│   ├── Auth/
│   ├── Users/
│   ├── Farms/
│   ├── Zones/
│   ├── Sensors/
│   ├── UAVs/
│   ├── Gateways/
│   ├── Missions/
│   ├── Telemetry/
│   ├── SensorData/
│   ├── Alerts/
│   └── Reports/
├── Validators/
├── Mappings/
└── Common/
```

Application defines interfaces for repositories and infrastructure services.

Examples:

```csharp
IUserRepository
IFarmRepository
ISensorNodeRepository
IMissionRepository
IAlertRepository
IUnitOfWork
ITokenService
IEmailService
IDateTimeProvider
```

Application must not contain PostgreSQL-specific SQL unless explicitly justified.

### 4.3 Infrastructure

Contains implementation details:

```text
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   ├── Repositories/
│   └── Migrations/
├── Authentication/
├── Email/
├── BackgroundJobs/
└── Services/
```

Infrastructure responsibilities:

- EF Core
- PostgreSQL
- repository implementations
- database migrations
- JWT token generation
- refresh-token persistence
- email notification implementation
- background synchronization or cleanup jobs
- external adapters

### 4.4 Api

Contains HTTP concerns only.

Typical contents:

```text
Api/
├── Controllers/
├── Middleware/
├── Filters/
├── Extensions/
├── Contracts/
└── Program.cs
```

Controller responsibilities:

1. receive HTTP request
2. validate basic transport requirements
3. call Application service/use case
4. map result to HTTP response

Controllers MUST NOT contain database queries or large business rules.

---

## 5. Main Request Flow

Use this direction:

```text
HTTP Request
    ↓
Controller
    ↓
Application Service / Use Case
    ↓
Repository Interface
    ↓
Repository Implementation
    ↓
Entity Framework Core
    ↓
PostgreSQL
```

For write operations:

```text
Controller
    ↓
Application
    ↓
Repository
    ↓
UnitOfWork / DbContext.SaveChangesAsync()
    ↓
PostgreSQL
```

---

## 6. Coding Conventions

### 6.1 General

- Use `async` / `await` for database and I/O operations.
- Use `CancellationToken` in controller, service, repository, and long-running operations.
- Avoid `.Result` and `.Wait()`.
- Enable nullable reference types where supported.
- Avoid magic strings for important statuses.
- Use enums or constants for well-defined state values.
- Do not duplicate validation logic across layers.
- Prefer small methods with one clear responsibility.
- Do not introduce patterns only for abstraction's sake.

### 6.2 Naming

C#:

```text
PascalCase       classes, methods, properties
camelCase        local variables, parameters
IName            interfaces
NameDto          DTO
CreateNameRequest
UpdateNameRequest
NameResponse
```

PostgreSQL:

```text
snake_case
```

Examples:

```text
sensor_nodes
sensor_readings
mission_waypoints
collection_attempts
refresh_tokens
created_at
updated_at
gateway_id
```

### 6.3 IDs

Primary keys use auto-increment integer IDs.

Preferred C# type:

```csharp
int
```

Preferred PostgreSQL definition:

```sql
GENERATED BY DEFAULT AS IDENTITY
```

Do NOT introduce UUID primary keys unless explicitly requested.

Foreign keys also use `int`.

### 6.4 Date and Time

Store timestamps in UTC.

Recommended C# type:

```csharp
DateTimeOffset
```

Recommended PostgreSQL type:

```text
timestamp with time zone
```

Do not store local timezone-dependent timestamps as plain strings.

---

## 7. Entity Framework Core Rules

- Use Fluent API entity configuration.
- Keep large mappings out of `OnModelCreating`.
- Prefer one configuration class per entity.
- Explicitly configure:
  - table name
  - key
  - required fields
  - max lengths
  - indexes
  - unique constraints
  - FK delete behavior
- Do not rely on cascade delete for important operational history.
- Use `AsNoTracking()` for read-only queries when appropriate.
- Avoid loading large graphs unnecessarily.
- Use projections for API read models.
- Add indexes for common filters and foreign keys.

Example:

```csharp
public sealed class SensorNodeConfiguration : IEntityTypeConfiguration<SensorNode>
{
    public void Configure(EntityTypeBuilder<SensorNode> builder)
    {
        builder.ToTable("sensor_nodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(x => x.DeviceCode)
            .IsUnique();
    }
}
```

---

## 8. Repository Rules

Repositories should hide persistence details but should not become generic dumping grounds.

Prefer feature-specific repositories.

Good:

```csharp
IMissionRepository
ISensorReadingRepository
IAlertRepository
```

Avoid creating a generic repository if it makes queries harder to understand.

Repository methods should reflect application needs:

```csharp
Task<Mission?> GetWithTargetsAsync(int missionId, CancellationToken ct);
Task<IReadOnlyList<SensorReading>> GetHistoryAsync(
    int sensorChannelId,
    DateTimeOffset from,
    DateTimeOffset to,
    CancellationToken ct);
```

---

## 9. API Rules

Base route:

```text
/api
```

Current backend API is intentionally **not versioned in the URL**.

Do NOT introduce routes such as:

```text
/api/v1/...
```

unless explicitly requested later.

Use resource-oriented routes.

Examples:

```text
POST   /api/auth/login
POST   /api/auth/refresh

GET    /api/farms
POST   /api/farms

GET    /api/sensor-nodes
POST   /api/sensor-nodes

GET    /api/missions
POST   /api/missions
POST   /api/missions/{id}/start
POST   /api/missions/{id}/cancel

POST   /api/gateways/{id}/sync
POST   /api/telemetry
```

Use appropriate HTTP status codes.

Do not return EF entities directly from controllers.
Use request/response contracts or DTOs.

---

## 10. Authentication and Authorization

Authentication uses:

```text
Access Token: short-lived JWT
Refresh Token: long-lived, persisted in database
```

Rules:

- Never store raw passwords.
- Use ASP.NET Core password hashing / Identity-compatible hashing.
- Store refresh token securely; prefer storing a hash instead of raw token.
- Refresh tokens should support:
  - expiration
  - revocation
  - replacement / rotation
- Protected endpoints require authentication.
- Authorization is role-based.

Main roles:

```text
FarmAdministrator
FarmOwner
FarmEngineer
```

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

Do not treat a gateway as a normal human user.

---

## 11. Mission Rules for Codex

A mission represents a planned UAV-assisted sensor data collection operation.

Important constraints:

- The backend does NOT directly fly the UAV in the initial project scope.
- The backend stores mission planning information.
- Mission can include:
  - farm
  - selected sensors
  - waypoints / flight points
  - schedule
  - current status
  - operational notes
  - telemetry when available
  - collection results
- UAV flight control belongs to the UAV/autopilot side.
- The platform may receive mission progress automatically or allow manual updates.

Mission status values:

```text
PENDING
SCHEDULED
RUNNING
COMPLETED
FAILED
CANCELLED
```

Do not invent extra mission states without updating `BUSINESS_RULES.md`.

---

## 12. Sensor and Gateway Rules

One sensor node can expose multiple sensor channels.

Examples:

```text
Sensor Node A
├── air_temperature
├── air_humidity
└── light_intensity
```

A channel represents one measurable value/type.

The mobile gateway:

- discovers registered sensors
- authenticates devices
- collects measurements
- records collection time
- may record location
- stores data locally if offline
- synchronizes data when server connectivity returns

The ESP32/Raspberry Pi gateway does NOT connect directly to PostgreSQL.

Use:

```text
Gateway → HTTP/MQTT/API → Backend → PostgreSQL
```

---

## 13. Offline Sync Rules

Backend sync APIs must be idempotent.

A gateway may resend records after a timeout or connection failure.

Therefore:

- duplicate synchronization must not create duplicate readings
- use client-generated/source record identifiers or deterministic duplicate keys
- persist sync batch/result information
- return per-record success/failure when appropriate
- rejected records should include an error reason

Never assume one HTTP request equals one sensor reading.

---

## 14. Alert Rules

Alert sources include:

- sensor threshold violation
- sensor data timeout
- low sensor/device battery
- UAV mission issue
- gateway issue

Gateway-related alerts must keep `gateway_id` when the alert belongs to a gateway.

Alerts need a lifecycle supporting:

```text
OPEN
ACKNOWLEDGED
CLOSED
```

Alert handling should preserve history rather than overwriting all previous actions.

---

## 15. Error Handling

Use centralized exception handling middleware.

Recommended response shape:

```json
{
  "success": false,
  "message": "Mission not found.",
  "errors": [],
  "traceId": "..."
}
```

Do not expose:

- stack traces
- SQL statements
- DB credentials
- JWT secrets
- internal exception details

to production API clients.

---

## 16. Testing Rules

Prioritize tests for:

- authentication
- refresh-token rotation
- mission status transitions
- mission scheduling validation
- sensor reading validation
- duplicate prevention
- offline sync idempotency
- alert generation
- authorization by role

Unit tests:

```text
Application business logic
Domain rules
Validation
```

Integration tests:

```text
API + PostgreSQL
EF Core queries
authentication
sync endpoints
```

---

## 17. Security Rules

Never commit:

```text
appsettings.Production.json containing secrets
.env files containing secrets
database passwords
JWT signing keys
SMTP passwords
device API keys
Wi-Fi credentials
```

Use configuration/environment variables or development user secrets.

---

## 18. Change Discipline

Before changing code:

1. inspect relevant existing code
2. inspect `AGENTS.md`
3. inspect `ARCHITECTURE.md`
4. inspect `DATABASE.md` for DB changes
5. inspect `API.md` for endpoint changes
6. inspect `BUSINESS_RULES.md` for domain behavior

When changing the database:

- update entity
- update EF configuration
- create migration
- update `DATABASE.md`
- update affected tests

When changing an API:

- update controller/contract
- update `API.md`
- update Swagger behavior
- update tests

When changing business behavior:

- update `BUSINESS_RULES.md`
- update tests

---

## 19. What Codex Must Avoid

Do NOT:

- bypass Application and query DbContext directly from controllers
- expose EF entities as API responses
- add UUID IDs
- let ESP32/UAV connect directly to PostgreSQL
- place flight-control logic in backend
- hard-delete critical mission or alert history without explicit requirement
- add a new status enum without updating business rules
- silently change database naming conventions
- silently upgrade the .NET target framework
- store plaintext passwords or plaintext refresh tokens
- create duplicate sensor readings during retry/sync


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
