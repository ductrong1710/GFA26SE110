# Updated SRS gap analysis

Baseline: backend `1ab5fc0`, frontend `a0685aa`, clean working trees before this task. Source and decisions: `../specs/2026-10-28-updated-srs-design.md`. This checklist records inspected source, not a claim of completed execution. DONE requires verified end-to-end evidence; no new lifecycle UC is DONE at baseline.

Paths below are relative to backend or the sibling frontend repository. `MISSING` in a row means no corresponding production UI/API/service/entity/table/auth/test flow exists; it does not mean an illustrative mock supplies that functionality.

| UC | Frontend | API / Service | Entity / Database | Authorization | Tests / baseline classification |
|---|---|---|---|---|---|
| 01 Public homepage | HomePage/static sections | No published-content API | Static frontend data | Public | PARTIAL; policy/catalog publication missing |
| 02 Consultation submission | ConsultationButton external contact links | MISSING | MISSING | Public submission needed | MISSING |
| 03 Homepage content management | MISSING | MISSING | MISSING | Admin | MISSING |
| 04 Consultation processing | MISSING | MISSING | MISSING | Assigned engineer | MISSING |
| 05 Farm survey | MISSING | MISSING | MISSING | Assigned engineer; owner/admin read | MISSING |
| 06 Device proposal | MISSING | MISSING | MISSING | Assigned engineer; owner/admin read | MISSING |
| 07 Deposit confirmation | MISSING | MISSING | MISSING | Engineer/admin | MISSING |
| 08 Installation request | MISSING | MISSING | MISSING | Assigned engineer | MISSING |
| 09 Installation review | MISSING | MISSING | MISSING | Admin | MISSING |
| 10 Installation assignment | MISSING | MISSING | MISSING | Admin | MISSING |
| 11 Installation processing | MISSING | MISSING | MISSING | Assigned technician only | MISSING |
| 12 Installation confirmation | MISSING | MISSING | MISSING | Authorized owner | MISSING |
| 13 Installation approval | MISSING | MISSING | MISSING | Admin | MISSING |
| 14 Rental management | MISSING | MISSING | MISSING | Admin | MISSING |
| 15 Rental viewing | MISSING | MISSING | MISSING | Owner farm/engineer relevant scope | MISSING |
| 16 Device assignments | DevicesPage gateway/UAV association | EquipmentService metadata CRUD | Uav/Gateway/SensorNode; no rental assignments | Global catalog and owner CRUD conflict | WRONG BUSINESS RULE |
| 17 Assigned devices | DevicesPage/SensorsPage | EquipmentService/SensorService | Existing devices only, no rental period/history | Global UAV/gateway reads conflict | PARTIAL / WRONG SCOPE |
| 18 Current readings | SensorDetailPage/SensorDataPage | SensorDataController/SensorDataService | SensorReading/channel/node/zone | FarmAccessService/user_farms | PARTIAL; existing API tests, new contextual roles not tested |
| 19 Reading history | ChannelHistory/EnvironmentalChart | channel history/readings API | SensorReading | Farm scope | PARTIAL; preserve core and verify integrated scope |
| 20 Sensor comparison | ZoneComparison/analytics | SensorDataService.CompareAsync | Stored compatible readings | Farm scope | PARTIAL; verify type AND unit boundary |
| 21 Threshold configuration | SensorConfiguration | ThresholdController/ThresholdService | SensorThreshold | Engineer write allowed incorrectly | WRONG ROLE |
| 22 Alerts | AlertsPage | AlertsController/AlertService | Alert/AlertHistory | ReadFarmData permits broad mutation | WRONG ROLE; preserve history |
| 23 Device incident | DeviceIncidentsPage/list/detail/evidence/resolution badge | DeviceIncidentsController/Service/Repository + service approval resolution | DeviceIncident/RentalDeviceAssignment/EvidenceImage | Current Owner creation, Admin/linked Owner reads | PARTIAL; intake API regression passed, browser E2E pending |
| 24 Service job creation | ServiceJobAssignmentForm / ServiceJobsPage | ServiceJobsController / ServiceJobService / ServiceJobRepository | ServiceJob / ServiceJobHistory; migration 20261029114933 | Current Admin creates/reassigns; active Technician target | PARTIAL; incident-based assignment, revision conflicts and reassignment verified; due-maintenance creation and browser E2E pending |
| 25 Maintenance processing | Assigned queue, start, ServiceReportForm and ServiceReports | ServiceJobService / ServiceReportService; submit/list/detail | Versioned ServiceReport, bounded EvidenceImage; migration 20261029165058 | Current assigned Technician; reassignment revokes previous access | PARTIAL; report implementation and component/unit tests present; real PostgreSQL integration chain passed 2026-10-30, scheduled maintenance/browser E2E pending |
| 26 Repair/replacement | REPAIR reports, scoped replacement form/history and report snapshot | ServiceReplacementService/options/replace plus report review | ServiceDeviceReplacement old/new assignment links; immutable report snapshot; migration 20261030070236 | Current assigned Technician; same-kind/farm, reservation and flight guards | PARTIAL; UAV/gateway/sensor exchange, lineage, rework and rental integration passed; browser E2E and distinct-job allocation race test pending |
| 27 Service confirmation | Latest report confirmation with required note | POST service-jobs/{id}/confirm-operation | Report-specific Owner actor/time/note; OWNER_CONFIRMED | Linked current Owner and current farm membership | PARTIAL; component test passed, real PostgreSQL integration chain passed 2026-10-30; browser E2E pending |
| 28 Service approval | Latest report approve/return; failed checks disable approval | POST service-jobs/{id}/approve or return | Report-specific decision; CLOSED/REWORK; retained old versions | Current Admin; revision/latest-report guards and passing checks required | PARTIAL; component/unit tests passed, real PostgreSQL integration chain passed 2026-10-30; browser E2E pending |
| 29 Maintenance scheduling | MISSING | No due-work worker | MISSING | Admin manages; assigned technician processes due job | MISSING |
| 30 Crop-condition report | MISSING | MISSING | MISSING | Authorized owner | MISSING |
| 31 Agricultural consultation | EngineerDashboard is analytics | MISSING | MISSING | Assigned engineer | MISSING |
| 32 Mission creation | CreateMissionPage/planning with rental selectors | MissionService/MissionLifecycle active rental gate | Mission/waypoints/targets/rental assignments | Current Owner + farm/rental scope | PARTIAL; API regression verified, browser E2E pending |
| 33 Mission progress | MissionDetailPage/reportMission + operational notes | MissionLifecycle + dedicated append-note action | Mission/history | Current Owner + farm scope | PARTIAL; standalone notes implemented, generic manual mission completion remains pending |
| 34 Mission monitoring | MissionDetailPage/polling | Mission/Telemetry APIs | TelemetryRecord/mission | Engineer broad read; technician context missing | PARTIAL / WRONG ROLE |
| 35 Mission results | MissionDetailPage + note image viewer | results/logs/readings + scoped note-images API | Mission/CollectionAttempt/readings/EvidenceImage | Current Admin/Owner farm scope | PARTIAL; human note image evidence implemented, support links/browser E2E pending |
| 36 Technical support | Failed mission link to incident form | Incident API validates mission/device association | DeviceIncident optional MissionId | Owner assigned equipment/mission | PARTIAL; intake linked, service processing/browser E2E pending |
| 37 Crop season | MISSING | MISSING | MISSING | Owner farm/zone | MISSING |
| 38 Cultivation activities | MISSING | MISSING | MISSING | Owner season scope | MISSING |
| 39 Season completion | MISSING | MISSING | MISSING | Owner; nullable actual results | MISSING |
| 40 Season comparison | MISSING | MISSING | MISSING | Owner; admin/engineer contextual read | MISSING |
| 41 User roles | UsersPage/ApiManagementProvider | UsersController/UserService | User/Role/UserRole | UavDeviceOperator still supported; no Technician | WRONG ROLE; role tests need new contract |
| 42 Login | AccountPage/AuthProvider/session | AuthController/AuthService | User/RefreshToken | Current-role checks; Technician unsupported | PARTIAL; existing authentication suites reusable |
| 43 Passwords | mailto recovery; Settings placeholder | PasswordRecoveryService/endpoints exist | hashed reset/version fields on User | Account owner/session invalidation | PARTIAL backend / MISSING frontend |
| 44 Notifications | AppTopbar/useNotifications latest five | notification endpoints/AlertService | Notification | User ownership exists | PARTIAL; business events/deep links/history missing |
| 45 Dashboard | useDashboard/role dashboards | ReportService/dashboard | Existing aggregate tables | Old role/scope matrix | PARTIAL; jobs/contracts/crop history missing |
| 46 Sync ingestion | Device-side; UI consumes results | SyncService/device auth | SyncBatch/readings/archived records | Gateway identity | PARTIAL verification; preserve idempotency and archive tests |
| 47 Telemetry ingestion | Mission UI consumes | TelemetryService/device auth | TelemetryRecord | Gateway+mission association | PARTIAL verification; preserve device-only ingestion |
| 48 Email delivery | No frontend sending required | EmailDelivery worker/repository | Notification EMAIL queue | Relevant recipients | PARTIAL; operational events not connected |

## Concrete source findings

- `frontend/src/App.jsx` contains no new lifecycle routes. `ManagementProvider` and `OperationsProvider` wrap real API providers; `src/data/mock` is test data, not a production persistence solution.
- `frontend/src/context/AuthProvider.jsx` delegates actual permission decisions to `config/backendPermissions.js`; `permissions.js` is a second divergent matrix. Both and inline role checks must be reconciled.
- `frontend/src/components/app/SensorConfiguration.jsx` permits FarmEngineer writes. `backend/src/FarmMonitoring.Api/Extensions/ApiServiceExtensions.cs` similarly permits engineer thresholds, owner device/farm administration, admin mission mutations and legacy operator roles.
- `FarmAccessQuery.AssignedFarmIds` derives all non-admin access from user_farms; job/consultation context does not exist. `EquipmentRepository` catalog access is global. Simply renaming an operator would preserve inappropriate access.
- `Notification.AlertId` is already nullable and existing email delivery can be reused for operational events; avoid new alert rows for jobs/contracts.
- `MissionLifecycle` combines technical targeted collection with manual mission behavior. Preserve targeted collection safety while adding explicit generic/manual mission support required by SRS 3.8.2; never fabricate collection completion.
- `frontend/src/pages/EquipmentPage.jsx`, `ProductDetailPage.jsx`, `CartPage.jsx` are not routed; rental/catalog/cart/sample pricing must become published information and consultation, not checkout. Unused registration forms and static public mission examples also need review.

## Required completion evidence beyond the UC table

- BR-01–48: role/scoped access, guards, explicit rental activation, history, idempotency and actual-data-only rules mapped to integration/unit cases.
- NSF-03–06,13,15,16: consultation routing, assignment/schedule audit, rental expiration, due maintenance, transactional notifications, histories and real dashboards.
- Images: upload validation, record association, authorized download and cross-owner/job rejection.
- Frontend: server field errors, success feedback, loading/empty/error states, responsive layouts, route reachability, contextual actions and browser-backed API chains.
- Five requested chains: consultation-to-installation request; approval-to-active rental; active rental-to-mission results; incident-to-service closure; crop report-to-engineer closure.
- Run complete Release unit/integration suite on ephemeral PostgreSQL plus frontend test/lint/build. Record exact counts and failures, migration/model consistency, no modified historical migrations, no firmware changes, unchanged sample-admin provisioning.
- Record assumptions and any requirements not directly verifiable locally (production uptime, backups, TLS deployment and expected-load timing); do not equate compilation with those guarantees.

Current delta: UC02 now has persisted public submission, actual Hero CTA/form, engineer notification enqueue and API/component evidence in ConsultationTests/consultation.test.jsx. UC04 has assigned claim/detail/follow-up history with concurrency and cross-engineer denial evidence; completion and downstream workflow remain partial. Baseline table above is retained as the original gap inventory, not current completion status. Full browser E2E evidence is pending.

UC05 delta: SurveyPanel -> /api/consultations/{id}/surveys -> ConsultationService -> IConsultationRepository/ConsultationRepository -> farm_surveys/survey_zones; immutable revisions, nullable unknown crop age/area, assigned Engineer checks. Evidence: ConsultationTests.Survey_revisions_preserve_unknown_values_history_and_reject_stale_or_unassigned_writes; ConsultationValidationTests; survey.test.jsx. PARTIAL: images and contextual Admin/Owner reads plus browser E2E pending.

UC05 scope delta: explicit admin customer/farm binding and read-only Admin/Owner survey access now implemented; current owner membership required. UI: ConsultationBinding, FarmSurveys. API evidence: ConsultationTests.Administrator_binds_customer_farm_and_survey_reads_follow_current_owner_membership. Images and browser E2E remain open.

UC29 current delta (2026-10-30): MaintenanceSchedulesController -> MaintenanceScheduleService -> IMaintenanceScheduleRepository/MaintenanceScheduleRepository -> MaintenanceSchedule + MaintenanceOccurrence EF/migration. Admin policy and repository current-role checks, bounded request validation, list/detail/create/update/disable, optimistic revision with row locks and assignment/Owner eligibility are implemented. Recurrence and persistence tests cover the preceding foundation; MaintenanceScheduleApiTests covers API boundaries/concurrent edits and assignment eligibility. PARTIAL: due-job worker, occurrence/history read UI, transactional notifications, frontend and browser E2E remain pending. Existing disabled/old schedules and generated work must remain traceable; no hard-delete endpoint.

UC30/31 foundation delta (2026-11-01): CropConditionReport with optional zone, reporter/assigned Engineer, crop/symptoms/status/revision; relational sensor references and appendable CropConsultationUpdate history; images separately linked to report or update under shared exactly-one-parent rule. Request validators cover required/bounded content, positive IDs, distinct bounded sensor references and optimistic revision. Migration20261101100505 generated, applied only to disposable test DBs. Still MISSING: intake/claim/process/follow-up/recommend/close API/service/repository authorization and transitions, contextual read-only sensor API, notifications, frontend and browser Flow5. Foundation is not a completed use case.

UC30/31 intake delta (2026-11-01): CropConditionReportsController -> CropSupportService -> ICropSupportRepository/CropSupportRepository now implement POST and paged GET/detail. Current Owner role/membership, own-report reads, Admin monitoring, Engineer unclaimed/assigned scope and denial of Technician access are enforced in repository queries. Report images, initial SUBMITTED history and Engineer notifications are transactional. API regression covers actual request/response, cross-farm access, invalid image/zone/channel, same-farm wrong-zone sensor, moved cross-farm sensor and revoked membership. PARTIAL: Engineer actions/state transitions, contextual sensor readings, frontend and actual browser Flow5 still pending.

UC31 processing delta (2026-11-01): fixed claim/request-information/reply/recommend/resolve endpoints now reach CropSupportService -> repository transaction -> report revision/state + attributed consultation update/evidence + counterpart notifications. Current Engineer/Owner role and assigned Engineer/reporter/current-membership checks; report row locks and optimistic revision. Integration regression covers one-winner claims/recommendations, wrong actor/state/stale revision, preserved ordered communication/images/site-survey flag, unassigned Engineer, removed role/membership and forced notification failure rollback. Crop report read-only sensor endpoint uses the same scope and only attached channels still matching farm/zone. Frontend and actual browser Flow5 remain missing; this is not end-to-end completion.

UC30/31 frontend delta (2026-11-01): /app/crop-support route/navigation/role permissions -> CropSupportPage -> CropReportForm/CropSupportDetail -> real intake, list/detail, explicit processing and contextual readings endpoints. Owner catalog selection follows pages and resets sensor scope on farm/zone changes. Evidence, immutable communication display, revision-aware actions, failed-write draft retention/reload and shared busy controls implemented. Engineer never loads generic farms/sensors from this page; Admin view-only; Technician menu/route absent. Six new component tests cover intake contract/scope reset, recommendation payload, conflict draft, monitoring controls, Owner reply and page claim refresh. Full113 Node+112 component(31files) pass; lint/build pass with existing large-main-chunk warning (~956kB). PARTIAL until actual browser Flow5 and remaining authorization variants; API/component tests are not browser proof.
