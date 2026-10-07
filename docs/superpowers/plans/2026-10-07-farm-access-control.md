# Farm-level access implementation plan

**Goal:** Implement all requirements in the user objective at C:/Users/ductr/.codex/attachments/87d416cb-7fff-4469-86e0-caae213429cb/goal-objective.md.
**Architecture:** Existing named role policies remain. Application FarmAccessService reads current user/roles and resolves resource farm IDs through IFarmAccessRepository. List repositories receive a FarmAccessScope and filter by a database membership subquery before counting/paging. No EF in controllers or global query filters that affect devices/jobs.
**Execution:** Inline because shared access contracts and cross-cutting services are tightly coupled; standing master-prompt approval applies. Preserve existing uncommitted role-model work. No commit, production DB update or historical migration edits.

- [x] Membership/farm regression tests first: admin-only CRUD, eligibility/conflict, two farms, auto-assignment/rollback, removal/stale roles. Execute to prove old implementation fails.
- [x] Add UserFarm entity, EF config, navigation/DbSet, IFarmAccessRepository, ICurrentUser, FarmAccessService, FarmAccessScope and resource resolution. Admin global, others eligible+assigned. Missing resource404; existing denied403. Add membership DTO/service/controller with existing envelopes and paginated members. Atomic farm+creator assignment in one SaveChanges.
- [x] Scope farms/zones/sensor nodes/channels/readings/latest/history/compare/thresholds; validate source and destination on moving sensors. Filter list counts in SQL. Check every explicit resource filter.
- [x] Scope mission list/details/targets/waypoints/logs/results/attempts/telemetry and every mutation; validate both original and requested farm. Device ingress remains unchanged.
- [x] Scope alerts/detail/actions/reports/dashboard. Resolve alerts from sensor channel/node or mission; exclude unscoped alerts for non-admin. Scope newly generated recipients to eligible assigned users; private notification ownership unchanged. Equipment catalog stays global; farm-filtered device aggregates use only explicit mission links, documented as association not ownership.
- [x] Generate AddUserFarmAssignments migration; preserve old migrations, no existing user assignments. Test schema/FKs/unique/indexes, old-data upgrade and atomic rollback.
- [x] Expand PostgreSQL integration coverage for all 50 objective cases and cross-farm route matrix. Update existing tests by adding explicit fixture assignments, never broad production auto-assignment or bypasses.
- [x] Update six current docs/schema/API/limitations; preserve historical docs. Full restore/build/tests, EF drift check, route security audit, independent read-only review, final evidence report.

## Decisions
- Source of identity is JWT sub only; role/membership truth is database per operation, no permission cache.
- UserFarm stores no role. Administrators cannot be assigned even if they also have an eligible role.
- Membership management uses ManageUsers. Duplicate409; missing404; invalid assignment422; malformed400.
- Global equipment catalog keeps role rules; farm-scoped dashboards/reports only count equipment linked through matching missions. Do not add equipment farm_id.
- No farm membership filter on private notification reads; new alert recipient generation must not send other farms' alerts to unrelated users.

Verification: full PostgreSQL runner restored and built the solution with zero warnings/errors; 48 unit and 95 integration tests passed. EF reports no pending model changes and generated an idempotent SQL script. Independent review finding concerning moved sensors in historical mission targets was reproduced, fixed and re-reviewed. See docs/farm-access-control-report.md.
