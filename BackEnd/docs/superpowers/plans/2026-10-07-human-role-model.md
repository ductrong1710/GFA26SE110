# Human role model implementation plan

**Goal:** Implement the user's 2026-10-07 role change on BackEnd: exactly FarmAdministrator, FarmOwner and FarmEngineer, with separate device credentials.

**Architecture:** Keep the existing six policies and controller surface. Share supported-role checks across validation and token issuance. Replace role seed data with a new migration that removes only deprecated memberships/role, preserving users/passwords and existing valid assignments. No farm membership schema.

**Approved specification:** The current user request, sections 1–13; it overrides the previous two-role documentation. Master prompt authorizes inline execution without another approval cycle.

**Tech stack:** Existing .NET10, EF Core/Npgsql, PostgreSQL, xUnit/WebApplicationFactory infrastructure.

- [x] Add role-validation and HTTP authorization/authentication/device-boundary regression tests; execute and inspect failures.
- [x] Update RoleNames, validators, role seeds, policies, supported-role login/refresh/me/JWT handling, recipients and report access. Keep DevelopmentAdminSeeder admin-only.
- [x] Generate a new role migration/snapshot. Replace generated fixed-ID role data mutations with name-based cleanup/inserts where needed to protect existing databases. Test fresh install and upgrade from the previous migration with legacy accounts, existing target roles, passwords, assignments and refresh tokens.
- [x] Update prior tests to use owner or engineer according to each test's intended privilege boundary, preserving security assertions.
- [x] Update current AGENTS, ARCHITECTURE, DATABASE, API, BUSINESS_RULES, README and master role list; preserve historical plans/migrations. Document global role authorization and device headers.
- [x] Restore/build/run all tests on isolated PostgreSQL; inspect migration/model drift and remaining deprecated-role references. Request read-only review and resolve material findings.
- [x] Report files, migration, policy matrix, verification and per-farm limitation. No real application database migration is required to prove the migration applies; test it in isolated databases.
