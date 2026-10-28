# Scheduled launch implementation plan

Goal: approve a scheduled Tello mission from the main web app without switching the laptop's Wi-Fi.

Architecture: persist plan revision, device boot session, readiness and human approval on the mission. The gateway polls while online and atomically consumes approval before switching to Tello. Existing durable preflight checkpoints prevent replay after reboot.

- [x] Backend: time-window rules, scoped human approval/read endpoints, authenticated device readiness/consume endpoints, migration, tests.
- [x] Firmware: persist downloaded revision, boot session, bounded polling, one-shot remote start; retain local start compatibility and flight safety checks.
- [x] Frontend: readiness/status polling and explicit aircraft/area confirmation in mission details.
- [x] Verify backend integration tests, frontend lint/build/tests, firmware host tests/build; document migration and deployment.

Verification: Release backend build without warnings; 100 unit and 170 PostgreSQL integration tests passed. Frontend lint/build, 108 Node tests, 41 component tests and one user-management test passed. Firmware mission host suite (including remote launch, lost response, reboot recovery and simulated flight) and PlatformIO mission_flight build passed. No migration applied to the user's database, hardware uploaded, or physical flight initiated.

Constraints: confirmation from start minus 10 minutes through start plus 60 seconds (capped by mission end); no automatic catch-up. Reboot or plan edit invalidates approval. RUNNING records accepted execution, not confirmed takeoff. No live emergency stop over the backend while disconnected for Tello flight. Never send flight commands during development verification.
