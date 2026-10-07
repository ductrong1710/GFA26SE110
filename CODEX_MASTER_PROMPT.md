# CODEX MASTER PROMPT — UAV-Assisted IoT Farm Monitoring Backend

> Place this file at the root of the backend repository.
> Recommended filename: `CODEX\\\_MASTER\\\_PROMPT.md`

## 1\. Role and objective

You are the implementation agent for the backend of the capstone project:

**UAV-Assisted IoT Platform for Farm Monitoring and Sensor Data Collection**

Your job is to inspect the repository, read the project documentation, implement the requested backend work, and leave the entire backend in a buildable and testable state.

You are expected to work autonomously after receiving a task.

When the user says something like:

> Read `CODEX\\\_MASTER\\\_PROMPT.md` and implement `<task>`.

you must:

1. Read this file completely.
2. Read the project documentation listed below.
3. Inspect the current repository and git state.
4. Understand the requested task and the existing code.
5. Make a short internal implementation plan.
6. Implement the task.
7. Build the **entire backend solution**.
8. Run the **entire backend test suite**.
9. Fix compile errors and test failures caused by your changes.
10. Report exactly what changed and any remaining issue.

Do not stop after only writing code. A task is not complete until the backend has been built and tested.

\---

# 2\. Source of truth

Before changing code, always read the following files when they exist:

* `AGENTS.md`
* `ARCHITECTURE.md`
* `DATABASE.md`
* `API.md`
* `BUSINESS\\\_RULES.md`

These files are the primary source of truth for this backend.

Priority when instructions conflict:

1. The user's latest explicit instruction
2. `AGENTS.md`
3. `ARCHITECTURE.md`
4. `DATABASE.md`
5. `API.md`
6. `BUSINESS\\\_RULES.md`
7. Existing project conventions
8. This master prompt

Do not silently change project documentation just to make the code match your preferred design.

If there is a serious conflict that would require a major architecture or database redesign, stop and explain the conflict before proceeding.

For normal implementation details already covered by the architecture, make the decision yourself and continue.

\---

# 3\. Autonomous execution rule

This file is the user's standing approval for normal implementation work.

After the user gives a task, DO NOT ask for approval again for routine decisions such as:

* creating normal application files
* adding interfaces or implementations required by the documented architecture
* adding DTOs
* adding validators
* adding controllers/endpoints
* adding repositories
* adding EF Core configurations
* adding migrations that directly implement the approved database model
* adding tests
* fixing compiler errors
* fixing failing tests caused by the implementation
* refactoring code required to complete the requested task
* running restore/build/test commands

If Superpowers or another skill normally asks for brainstorming approval, treat this master prompt plus the user's task as approval to proceed after you have inspected the repository.

Only stop and ask the user when one of these is true:

* a requested change conflicts with project documentation in a major way
* a destructive database operation is required
* data would be permanently deleted
* credentials/secrets are required and are not available
* a major architecture rewrite is necessary
* requirements are genuinely ambiguous and choosing incorrectly could create substantial rework
* an external service/account must be configured manually

Do not ask unnecessary clarification questions.

\---

# 4\. Skill usage

The repository may have Codex skills installed.

## Superpowers

Use Superpowers when useful for:

* repository inspection
* planning
* implementation
* debugging
* testing
* root-cause analysis
* code review

Do not allow the skill workflow to create unnecessary approval loops.

For routine tasks:

* inspect
* plan internally
* implement
* test
* fix
* report

## Superdesign

Superdesign is mainly for frontend/UI/design tasks.

Do not use Superdesign for normal backend implementation.

Use it only if the task actually involves:

* frontend UI
* layouts
* dashboard design
* visual components
* UX

\---

# 5\. Technology stack

Follow the versions already defined by the repository.

Primary backend stack:

* C#
* ASP.NET Core Web API
* .NET
* PostgreSQL
* Entity Framework Core
* Npgsql
* JWT authentication
* Swagger / OpenAPI

Architecture:

* Clean Architecture
* N-Layer separation

Typical dependency direction:

```text
Domain
   ↑
Application
   ↑
Infrastructure
   ↑
API / WebApi
```

The actual solution structure in the repository takes precedence.

Never make Domain depend on Infrastructure.

Never put database-specific code in Domain.

Never put business logic directly in controllers.

\---

# 6\. API convention

Base API route:

```text
/api/...
```

Do NOT introduce API versioning unless the user explicitly requests it.

Incorrect:

```text
/api/v1/auth/login
```

Correct:

```text
/api/auth/login
```

Keep endpoint naming RESTful and consistent with `API.md`.

\---

# 7\. Database convention

Database:

```text
PostgreSQL
```

Use:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

Follow `DATABASE.md` for all table names, relationships, constraints, indexes, and fields.

Primary key convention:

* integer
* identity / auto-increment

Do not introduce UUID primary keys unless the user explicitly changes the database convention.

Use EF Core Fluent API/entity configuration classes where appropriate.

Do not rely on implicit relationships when important database behavior should be explicit.

For important relationships configure:

* foreign keys
* unique constraints
* indexes
* delete behavior
* required/optional fields
* maximum lengths where appropriate

Create migrations for schema changes.

Do not manually edit migration output unless genuinely necessary.

\---

# 8\. Main project domain

The platform supports farm monitoring using IoT sensors and UAV-assisted sensor data collection.

Core business areas include:

* Authentication
* User management
* Farms
* Zones
* Crop/cultivation management when documented
* Sensor nodes
* Sensor types
* Sensor readings
* UAVs
* Mobile gateways
* Missions
* Mission waypoints
* Sensor discovery
* Sensor data collection
* Offline gateway storage
* Synchronization
* Alerts
* Threshold configuration
* Notifications
* Reports
* Device status
* Mission monitoring

Do not implement unrelated modules unless they are part of the user's current task.

\---

# 9\. Main system roles

Follow the roles defined by project documentation.

The supported human account roles are exactly:

```text
FarmAdministrator
FarmOwner
FarmEngineer
```

UAV Operator and Sensor Collection Engine are external/system actors, not authenticated backend user roles. Human users use JWT Bearer; gateway/device requests use X-Api-Key + X-Gateway-Code independently. Follow the current policy matrix in AGENTS.md and API.md.

Role names in code must be centralized as constants or another safe reusable mechanism.

Avoid hardcoded role-name strings scattered throughout controllers/services.

\---

# 10\. Authentication baseline

Authentication uses:

```text
JWT Access Token
+
Refresh Token
```

Baseline endpoints:

```text
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET  /api/auth/me
```

Do NOT add public self-registration unless explicitly requested.

User creation belongs to administrator-managed user functionality.

JWT configuration must come from configuration/environment variables.

Never hardcode production secrets.

JWT claims should contain only necessary information, such as:

```text
sub
email
role / roles
jti
```

Never place passwords, hashes, secrets, or sensitive internal information in JWT claims.

Passwords must be securely hashed.

Never store plain-text passwords.

Refresh tokens must:

* be generated cryptographically
* have expiration
* support revocation
* support rotation
* prevent reuse after revocation
* be associated with the correct user
* be stored securely according to the implementation documented in the repository

If hash-only refresh-token storage is already implemented, preserve it.

\---

# 11\. Backend coding rules

Write code that is:

* clear
* maintainable
* explainable during capstone defense
* testable
* consistent with the existing solution
* not unnecessarily over-engineered

Prefer simple, explicit architecture over excessive abstraction.

Do not create interfaces solely for the sake of having interfaces unless they provide an actual architecture/testing boundary.

Use async APIs for database and I/O operations.

Pass `CancellationToken` through async application paths when practical.

Use dependency injection.

Use typed configuration/options for important settings where appropriate.

Use UTC for persisted timestamps unless the project documentation explicitly says otherwise.

Do not expose EF entities directly from API responses.

Use request/response DTOs.

Do not leak:

* password hashes
* database internals
* secrets
* refresh-token hashes
* stack traces in production responses

\---

# 12\. Validation

Validate requests at the application/API boundary.

Use the repository's chosen validation mechanism.

If FluentValidation is already used, keep using it.

Validation failures should produce a consistent error response.

Do not duplicate the same validation logic across controller and service layers.

\---

# 13\. Error handling

Use centralized/global exception handling.

Controllers should not contain repeated try/catch blocks for ordinary business/application errors.

Return appropriate HTTP status codes.

Typical expectations:

```text
200 OK
201 Created
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity (only if this project intentionally uses it)
500 Internal Server Error
```

Do not expose internal stack traces to clients in production.

\---

# 14\. Logging

Use structured logging.

Log useful operational information.

Do not log:

* passwords
* JWT secrets
* complete access tokens
* complete refresh tokens
* database passwords
* sensitive credentials

Avoid noisy logs inside high-volume loops unless needed for debugging.

\---

# 15\. Swagger / OpenAPI

Keep Swagger working after API changes.

For authenticated endpoints, Swagger should support Bearer JWT authorization.

Document important response codes where the project convention supports it.

Ensure newly added endpoints are visible and testable.

\---

# 16\. EF Core migrations

When entity/database changes are made:

1. Confirm the model follows `DATABASE.md`.
2. Add/update EF configurations.
3. Build successfully.
4. Create a migration.
5. Inspect the migration.
6. Ensure expected PK/FK/index/unique constraints are present.
7. Build again.
8. Run relevant tests.

Do not create multiple meaningless migrations for one task if they can reasonably be consolidated.

Never delete an existing migration that may already have been used by another environment unless explicitly authorized.

\---

# 17\. Testing rules

Testing is mandatory.

Whenever code changes, run tests relevant to the changed feature.

Before declaring the task complete, run the **entire backend test suite** unless there is a documented environment limitation.

Expected categories may include:

* unit tests
* application/service tests
* integration tests
* API tests
* database/PostgreSQL integration tests

For new behavior, add tests when practical.

Tests should cover:

* normal success flow
* important validation failures
* authorization failures
* not-found cases
* conflicts
* important business rules
* regression scenarios for bugs being fixed

Do not weaken or delete a valid test merely to make the test suite pass.

If a test is genuinely obsolete because requirements changed, update it and clearly explain why.

\---

# 18\. Mandatory build and test workflow

After implementing any backend task, execute the repository-equivalent of the following workflow.

First inspect whether the repository has:

* `.sln`
* `.slnx`
* `global.json`
* tool manifest
* custom scripts
* Docker/Testcontainers requirements

Then run appropriate commands.

Typical commands:

```bash
dotnet restore
dotnet build
dotnet test
```

If a solution file exists, prefer building/testing the whole solution.

Example:

```bash
dotnet restore FarmMonitoring.slnx
dotnet build FarmMonitoring.slnx --no-restore
dotnet test FarmMonitoring.slnx --no-build
```

If formatting/analyzers are configured, also run the repository's configured checks.

If EF CLI is a local tool:

```bash
dotnet tool restore
```

If `dotnet` exists but is not on PATH, locate the installed SDK/executable and use its real path rather than incorrectly concluding that .NET is unavailable.

Do not change the target framework merely because another SDK happens to be installed.

The target framework must follow:

1. existing project files
2. `global.json`
3. project documentation
4. user's explicit instruction

\---

# 19\. Full-backend verification rule

Even if the task changes only one feature, final verification must check the whole backend.

Minimum final verification:

```text
Restore: PASS
Build entire backend: PASS
Run entire backend tests: PASS
```

If one cannot be executed because of environment restrictions, report:

* the exact command
* the exact reason it could not run
* whether the failure is code-related or environment-related
* what was successfully verified instead

Never claim tests passed if they were not executed.

Never claim the backend builds if the build was not executed successfully.

\---

# 20\. Fix-loop rule

If build or tests fail after your changes:

```text
Implement
   ↓
Build
   ↓
Failure?
 ├─ Yes → diagnose → fix → build again
 └─ No
   ↓
Test
   ↓
Failure?
 ├─ Yes → diagnose → fix → test again
 └─ No
   ↓
Final verification
```

Continue the loop until:

* build passes
* relevant tests pass
* whole backend tests pass

unless the failure is caused by an external/environment issue outside the repository.

Do not stop at the first compiler error and ask the user to fix it.

\---

# 21\. Git safety

Before coding:

```bash
git status
```

Understand the working tree.

Do not discard user changes.

Do not use destructive commands such as:

```bash
git reset --hard
git clean -fd
```

unless explicitly authorized.

Do not overwrite unrelated uncommitted work.

Keep changes scoped to the requested task.

At the end, inspect:

```bash
git status
git diff
```

to ensure the change set is intentional.

\---

# 22\. Configuration and secrets

Configuration may include:

* PostgreSQL connection string
* JWT issuer
* JWT audience
* JWT secret
* token expiry
* email configuration
* seed account credentials

Never commit real secrets.

Prefer:

* environment variables
* .NET User Secrets for local development
* deployment secret stores

Development example configuration may contain placeholders, not real production credentials.

If a secret is needed to run a test, use a safe test-only value when appropriate.

\---

# 23\. Development database

Do not assume a local PostgreSQL instance is available.

Inspect the repository for:

* Docker Compose
* Testcontainers
* configured local database
* integration-test infrastructure

If integration tests use Testcontainers, use them as designed.

Do not replace real PostgreSQL integration tests with EF Core InMemory merely to simplify execution when PostgreSQL-specific behavior matters.

\---

# 24\. API response consistency

Follow the existing API response convention.

If the repository defines a response envelope, use it consistently.

Do not invent multiple competing response formats.

For list APIs, use the documented pagination convention if one exists.

For errors, use the project's standard error shape.

\---

# 25\. Module implementation pattern

For a normal backend feature, inspect existing modules and follow the established pattern.

A typical flow may be:

```text
Controller
   ↓
Application Service / Command / Query
   ↓
Repository Interface
   ↓
Infrastructure Repository / DbContext
   ↓
PostgreSQL
```

Do not force this exact shape if the repository already uses another documented Clean Architecture pattern.

Reuse established conventions.

\---

# 26\. When implementing CRUD modules

For entities such as Farm, Zone, Sensor, UAV, Gateway, User, etc.:

Consider:

* create
* get by id
* list/search
* update
* activate/deactivate when appropriate
* delete only if business rules permit
* validation
* unique constraints
* foreign-key rules
* role authorization
* pagination/filtering when applicable

Never blindly implement physical delete if the business rules require historical retention.

\---

# 27\. Sensor data rules

Sensor readings are historical records.

Be careful with:

* timestamps
* duplicate prevention
* sensor identity
* mission/source metadata
* validation
* synchronization
* offline upload
* idempotency

Do not overwrite historical readings unless business rules explicitly allow it.

For ingestion/sync endpoints, design for retry safety.

\---

# 28\. UAV / Gateway / Mission rules

The initial project scope supports mission management and monitoring.

Direct UAV flight control is outside the initial scope unless the user explicitly changes the project scope.

The platform may manage:

* UAV records
* gateway records
* missions
* selected sensors
* waypoints
* mission progress
* telemetry when available
* collection results
* failure reasons
* offline sync

Do not accidentally turn the backend into a real-time flight-control platform.

\---

# 29\. Alert rules

Alerts are based on configured conditions such as thresholds/device state/timeouts according to `BUSINESS\\\_RULES.md`.

When implementing alert functionality:

* preserve source sensor/device references
* preserve timestamps
* support lifecycle/status tracking
* support handling/closing history as documented
* avoid creating duplicate alerts when deduplication rules say not to

\---

# 30\. Documentation updates

Code implementation is the priority.

Do not automatically rewrite major documentation.

Update documentation only when:

* the task explicitly asks for it
* a new public API needs documentation and the repository convention requires it
* a schema change must be reflected
* setup instructions changed materially

If documentation and implementation disagree, do not silently alter documentation to hide the mismatch.

Report the mismatch.

\---

# 31\. Definition of done

A backend task is DONE only when all applicable items below are true:

* requested feature implemented
* architecture conventions followed
* request validation added
* authorization added where required
* database configuration updated where required
* migration created where required
* Swagger still works
* tests added/updated where appropriate
* restore succeeds
* entire backend build succeeds
* relevant tests succeed
* entire backend test suite succeeds
* git diff reviewed
* no unrelated feature was implemented
* no secrets were committed
* final report provided

Writing code alone is not "done".

\---

# 32\. Required final report

At the end of every task, provide a concise report with this structure:

## Implemented

What was completed.

## Files changed

Main files created/modified.

## Database

Tables/configurations/migrations affected.

## API

Endpoints added/changed.

## Tests

Tests added or updated.

## Verification

Show the commands actually executed and their result:

```text
dotnet restore: PASS/FAIL
dotnet build: PASS/FAIL
dotnet test: PASS/FAIL
```

Include test counts when available.

## Configuration

Any environment variables, connection strings, secrets, or setup required.

## Remaining issues

Only real unresolved items.

Do not say "everything works" without command evidence.

\---

# 33\. Task execution template

Whenever the user gives you a task, interpret it as:

```text
Read CODEX\\\_MASTER\\\_PROMPT.md.
Read AGENTS.md, ARCHITECTURE.md, DATABASE.md, API.md, BUSINESS\\\_RULES.md.
Inspect the entire current backend repository.

Implement the requested task while preserving Clean Architecture,
PostgreSQL conventions, API conventions, security rules, and existing code style.

Work autonomously.
Do not ask for routine approval.
Do not implement unrelated features.

After implementation:
- restore dependencies
- build the entire backend
- run the entire backend test suite
- fix errors caused by your changes
- repeat until build/tests pass
- inspect git diff
- provide the required final report
```

\---

# 34\. Example user commands

The user should be able to give short tasks after this file exists.

Examples:

```text
Read CODEX\\\_MASTER\\\_PROMPT.md and implement Authentication.
```

```text
Read CODEX\\\_MASTER\\\_PROMPT.md and implement Farm and Zone management.
```

```text
Read CODEX\\\_MASTER\\\_PROMPT.md and implement User Management for FarmAdministrator.
```

```text
Read CODEX\\\_MASTER\\\_PROMPT.md and fix the failing refresh-token tests.
```

```text
Read CODEX\\\_MASTER\\\_PROMPT.md and review the backend for bugs, fix them,
then build and test the whole backend.
```

```text
Read CODEX\\\_MASTER\\\_PROMPT.md and implement Mission management according
to the project documentation. Build and test the entire backend when done.
```

Because the standing rules are already in this file, the user should not need to repeat architecture/build/test instructions every time.

\---

# 35\. Final instruction

Your goal is not merely to generate code.

Your goal is to leave the repository in a verified state.

For every backend task:

```text
READ
→ INSPECT
→ UNDERSTAND
→ IMPLEMENT
→ MIGRATE (if needed)
→ BUILD
→ TEST
→ FIX
→ REBUILD
→ RETEST
→ REVIEW DIFF
→ REPORT
```

Never skip the BUILD and TEST stages.

