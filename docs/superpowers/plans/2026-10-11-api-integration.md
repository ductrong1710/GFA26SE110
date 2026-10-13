# Frontend/backend API integration execution plan

User specification and CODEX_MASTER_PROMPT.md authorize routine autonomous integration. Preserve BackEnd/fe separation and existing frontend relocation; no commits/pushes.

1. Audit all actual controllers/DTOs and screens/mocks; create initial FRONTEND_API_INTEGRATION.md with complete endpoint inventory and honest pending statuses.
2. Add named configurable backend Frontend CORS policy before auth. Test allowed/denied-origin preflight on real endpoint without weakening JWT/device boundaries.
3. Build frontend VITE_API_BASE_URL configuration, native-fetch envelope/page/error/204/query support, session abstraction and real auth. Test login/error/logout, role normalization, rotated refresh, one retry, concurrency and failure clearing.
4. Integrate screen-specific reads and async mutations through adapted Management/Operations contexts: users; farms/zones/members; sensors/channels/thresholds; equipment; missions/results/logs/attempts/telemetry; readings/compare; alerts/notifications; dashboard/reports. Preserve visual design and add loading/error/empty states.
5. Extract pure fixture helpers, isolate explicit mock/demo mode, disable unsupported browser device sync, public registration/commerce and operator-role actions; record exact contract mismatches and resolutions.
6. Run full backend tool restore/restore/build/test and frontend npm ci/lint/all node tests/build. Use PostgreSQL/Testcontainers as designed. Run browser login/me/list/safe mutation/logout/refresh/CORS smoke where environment allows; state limitations precisely.
7. Re-scan controllers and runtime data/API usage; update report during progress and final counts, unresolved details, CORS/auth evidence and verification. Completion remains PARTIAL if supported features still have pending/mismatch/blocked statuses.