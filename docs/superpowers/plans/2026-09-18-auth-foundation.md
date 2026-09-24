# Authentication Foundation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver the approved authentication-only backend foundation with verified PostgreSQL persistence.

**Architecture:** Four documented Clean Architecture layers. Application owns authentication orchestration and persistence/security abstractions; Infrastructure owns EF Core, password hashing, JWT and atomic token persistence. API owns transport and composition.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, Npgsql 10, FluentValidation, Swagger, xUnit.

**Spec:** ../specs/2026-09-18-auth-design.md

## Global constraints

- Do not modify the five existing documentation files.
- Use integer identity IDs, snake_case SQL, UTC timestamps, and /api without version segments.
- Implement only users, roles, user_roles and refresh_tokens; no public registration.
- Secrets are external; never store raw passwords or refresh tokens.
- Work in the approved backend checkout; do not publish or commit automatically.

## Task 1: Executable authentication acceptance tests

Create FarmMonitoring.slnx, Directory.Build.props, global.json, src/FarmMonitoring.{Domain,Application,Infrastructure,Api}/*.csproj and tests/FarmMonitoring.{UnitTests,IntegrationTests}/*.csproj. Create IntegrationTests/AuthApiTests.cs and ApiFactory.cs, with real PostgreSQL isolated by a unique test database. Use a minimal API bootstrap for the initial red test.

- [x] Restore test dependencies.
- [x] Write /api/auth/me unauthenticated test: `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);` and run to observe 404 before adding controllers.
- [x] Add login, refresh, logout, validation and JWT tests before the corresponding implementation.

## Task 2: Authentication domain and application service

Create Domain/Entities/{User,Role,UserRole,RefreshToken}.cs and Constants/RoleNames.cs; Application/Features/Auth/{AuthService,AuthContracts,AuthValidators}.cs, Interfaces/{IAuthRepository,ITokenService,IPasswordService}.cs and Common/AuthException.cs.

Interfaces: AuthService.LoginAsync(LoginRequest,CancellationToken), RefreshAsync(RefreshRequest,CancellationToken), LogoutAsync(int,RefreshRequest,CancellationToken), GetCurrentUserAsync(int,CancellationToken). Repository performs GetUserByEmailAsync, GetUserByIdAsync, SaveLoginAsync, RotateAsync and RevokeAsync. Token service creates token pairs; password service verifies/hashes ASP.NET Core compatible passwords.

- [x] Add behavioral tests rejecting empty email/password/token, incorrect credentials, inactive users and missing sessions.
- [x] Implement normalization, validation, generic authentication errors and user DTO projection.
- [x] Verify tests catch the missing behavior before implementation and pass after integration wiring.

## Task 3: PostgreSQL and security implementations

Create Infrastructure/Persistence/AppDbContext.cs, Configurations/{User,Role,UserRole,RefreshToken}Configuration.cs, Repositories/AuthRepository.cs, Authentication/{JwtOptions,JwtTokenService,PasswordService}.cs, Persistence/DevelopmentAdminSeeder.cs and DependencyInjection.cs.

- [x] Implement explicit schema mappings and seed both role names.
- [x] Implement random refresh tokens with hash-only storage, configured JWT signing and password rehash support.
- [x] Rotate/revoke inside transactions with PostgreSQL row locks, preserving atomic replacement and ownership checks.
- [x] Test expiration, sequential reuse, concurrent reuse and logout ownership against PostgreSQL.
- [x] Generate InitialAuth via Infrastructure project and API startup project; inspect schema and apply to isolated PostgreSQL database.

## Task 4: API and runtime configuration

Create Api/Controllers/AuthController.cs, Contracts/ApiResponse.cs, Middleware/ExceptionHandlingMiddleware.cs, Extensions/ApiServiceExtensions.cs, Program.cs, appsettings.json, appsettings.Development.json and Properties/launchSettings.json.

- [x] Wire startup configuration validation, FluentValidation, error envelopes and Swagger Bearer support.
- [x] Require authorization on logout/me; configure sub/role claim mapping and authentication before authorization middleware.
- [x] Implement development-only optional admin seed and verify idempotency without resetting existing accounts.
- [x] Test normal/forged/expired JWTs, multiple roles, current user, malformed requests and secret-free errors.

## Task 5: Verification and handoff

Create README.md, scripts/Test-Postgres.ps1 and docs/auth-implementation-report.md with exact setup, commands, dependency versions, examples, inventory and proposed source-of-truth documentation updates.

- [x] Run `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build` using isolated PostgreSQL connection.
- [x] Check migration has no pending model changes and generate SQL for review.
- [x] Run dependency vulnerability audit, inspect diff and compare hashes of the five protected docs.
- [x] Record test evidence and any remaining limitations. Leave changes reviewable in the checkout.

