# Approved authentication foundation

Implement only Authentication and its shared backend foundation. User approved the design in conversation on 2026-09-18. Do not edit AGENTS.md, ARCHITECTURE.md, DATABASE.md, API.md, or BUSINESS_RULES.md; list proposed additions separately.

Use net10.0 and the documented FarmMonitoring.Domain, Application, Infrastructure and Api projects. Application depends on Domain; Infrastructure implements Application interfaces; API references Infrastructure only for composition. Controllers stay thin. Use simple services, not MediatR or generic repositories.

Persist only users, roles, user_roles and refresh_tokens with integer identity keys, explicit snake_case mappings and UTC timestamps. Normalize email before persistence and lookup. Store ASP.NET Core password hashes and SHA-256 hashes of random 64-byte refresh tokens. Preserve documented replacement foreign key and optional IP fields. Restrict foreign-key deletes. The user DTO contains a roles array as documented, not a single-role property.

Expose POST /api/auth/login, POST /api/auth/refresh, authenticated POST /api/auth/logout and GET /api/auth/me. Login/refresh responses contain accessToken, refreshToken, expiresIn, accessTokenExpiresAt and user within the standard success envelope. Logout receives refreshToken and checks ownership. Invalid credentials and inactive users produce generic 401. Validation produces 400; authentication/authorization failures use consistent 401/403 errors. No registration or other modules.

JWT validates signature, algorithm, issuer, audience and lifetime, with sub, email, role(s) and jti claims. Configuration is validated at startup. Secret and database connection are provided externally. Access tokens live 15 minutes by default, refresh tokens 7 days. Logout revokes only the provided refresh token; existing access tokens remain valid until expiry, but inactive users are rejected by Auth services.

Refresh rotation and logout use database transactions and row locks on refresh_tokens. A second simultaneous rotation waits, then sees revocation and returns 401; successor insertion and predecessor revocation commit together. Refresh rechecks active user status and loads current roles. Refresh reuse is rejected; automatic token-family revocation is outside this phase.

Seed role names through migrations. Optional development startup seed requires supplied email/password, hashes password, adds the first administrator only, and never resets/promotes existing accounts. Migrations are applied explicitly, never automatically on ordinary startup. Swagger is enabled in Development and supports HTTP Bearer.

Validate with real PostgreSQL integration tests and focused unit tests. Cover login success/failures, normalization, refresh expiration/revocation/rotation/concurrency, logout ownership, JWT integrity/expiry, /me, role authorization, validation envelopes, and credential-free errors. Generate/review the initial migration, run restore/build/test and document setup and examples.
