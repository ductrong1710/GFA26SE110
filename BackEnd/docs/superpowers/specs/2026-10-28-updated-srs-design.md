# Updated operational lifecycle design

## Authority and scope

Authoritative source: `D:/Learning/SEP/Code/Report3_Software_Requirement_Specification_Updated_Flow.docx`, revision 28 October 2026, SHA256 `5759E5EACE4F2A2DD294880D6E7226B90BD6901FF315F5FF8B17ACAF379FFADA`.
All 1,727 extracted paragraphs were read before implementation. The document explicitly retains old diagrams; updated prose, tables and BR-01–48 govern when those diagrams disagree. The requested deliverable spans both backend and frontend, all UC-01–48, NSF-01–16, business rules, applicable UI/security/data requirements and five end-to-end chains. A working subset is not completion.

Preserve the established .NET 10/Application/Domain/Infrastructure layers, PostgreSQL, React components, gateway ingestion, synchronization and flight safety. Do not change firmware, payment processing, warehouse inventory, AI diagnosis/prediction, or the sample administrator provisioning excluded by the user. New migrations only; run them on disposable test databases, not the application database.

## Roles and authorization

Human roles: Administrator, Farm Owner, Technician, Agricultural Engineer. Retain `FarmAdministrator`, `FarmOwner`, `FarmEngineer` API identifiers where compatible; introduce `Technician`, retire `UavDeviceOperator` from assignable/authenticated role contracts. Do not grant new technician privileges automatically to legacy operators: migrate the role catalog safely and require explicit assignment. Preserve users and operational history.

Admin manages public content, users, farms/zones, physical equipment, assignments, thresholds, jobs, contracts, maintenance schedules and alert resolution. Farm Owner reads authorized farms and creates/manages own missions, incidents, support and crop records; no farm/device catalog administration. Admin monitors routine missions, not their normal operator. Technician reads/processes assigned jobs and only the context necessary for those jobs. Engineer processes assigned consultations/surveys/proposals/crop support and read-only supporting data; cannot configure thresholds or operate missions.

Keep role gates at controllers and enforce resource scope in services/repository queries before paging. Separate owner farm membership from technician job scope and engineer consultation scope. Do not make a generic farm membership grant all operational resources. Assignment changes and disabled users must affect already-issued sessions through current database checks.

## Architecture and relationships

Separate application modules for PublicContent, Consultations, Installations, Rentals, TechnicalSupport and CropManagement, each with contracts/validators/service/repository interface and infrastructure repository. Do not create a single workflow service for all modules. Reuse PageQuery, exceptions, transaction patterns, access helpers and current notification/email queue.

ConsultationRequest -> many FarmSurveys -> many versioned DeviceProposals -> InstallationRequest -> InstallationJob. Proposal lines are relational device type/quantity/location rows. Record deposit as actor/time/reference confirmation only. Link the customer to an authenticated owner and authorized farm before installation handover/rental; unauthenticated request submission alone must never grant account/farm access.

InstallationJob stores assigned technician, schedule, report versions, checks, owner confirmation and admin decision/history. Approved installation permits a distinct RentalContract to be activated by Admin with explicit dates. RentalContract -> many DeviceAssignments with concrete SensorNode/Gateway/Uav references and location/history. Preserve these existing device entities instead of rewriting ingestion into a new generic device table. Exactly one concrete device reference per assignment, nonoverlapping active assignments, bounded dates and farm/zone consistency are enforced transactionally.

Rental eligibility is checked server-side when selecting equipment, creating/updating/scheduling/starting/launching missions and device claims where applicable, without preventing safe landing, terminal-result ingestion or backlog sync after expiration. Expiration/retrieval prevents new operations but preserves history. Existing reservation locks remain authoritative for availability.

DeviceIncident / TechnicalSupportRequest -> ServiceJob; MaintenanceSchedule -> due ServiceJob independently of incident. Reports include cause, work, components, checks, images, notes and optional replacement assignment. Replacement closes the prior assignment and creates its successor, retaining links and old device history. Technician submits; owner confirms; admin closes or returns for rework.

CropConditionReport -> many human AgriculturalConsultation updates, scoped engineer assignment, optional requested owner information and on-site survey recommendation. CropSeason belongs to a farm and optionally zone, with CultivationActivity children and nullable recorded yield/cost/revenue. Comparison derives only quantities supported by known stored values; missing values stay null.

Notifications are committed with the business transaction; the existing email worker delivers configured email asynchronously. Add workflow links/deduplication keys without fabricating Alert records. Periodic rental/maintenance processing must be idempotent and concurrency-safe. Files/images need authorized upload/download, bounded size/type checks, opaque storage identifiers and links to the owning record; no public sensitive URLs or production localStorage.

## State-machine decisions where SRS omits literal status codes

These are implementation assumptions, not quoted SRS enums. All transitions use named actions and history with actor/time/reason, never unrestricted status PATCH.

- Consultation: NEW -> IN_PROGRESS -> COMPLETED; cancellation allowed before a submitted installation, with reason. Claim/assign establishes responsible engineer; unassigned inbox exposes only needed triage information.
- Survey/proposal: drafts may be revised; submitting a confirmed proposal freezes that version. A later revision creates a new version rather than overwriting accepted evidence.
- Installation request: DRAFT -> SUBMITTED -> APPROVED or RETURNED; RETURNED -> SUBMITTED after correction. Customer plan and deposit confirmation required for submission. Admin return requires reason.
- Installation/service job: ASSIGNED -> IN_PROGRESS -> SUBMITTED -> OWNER_CONFIRMED -> APPROVED/CLOSED. Admin may request REWORK with reason; rework creates another report version and requires fresh confirmation. Assign/reschedule operations retain history. Technician report never activates rental.
- Rental: DRAFT -> ACTIVE -> EXPIRED -> ENDED; explicit return/retrieval recorded before ending. Confirmed extension changes end date/history and may reactivate an expired contract only if assignments remain valid. No payment integration.
- Crop support: OPEN -> IN_REVIEW -> WAITING_FOR_INFORMATION -> IN_REVIEW -> RESOLVED -> CLOSED; recommendation and human actor required for resolution, information request needs explanatory note.
- Crop season: ACTIVE -> COMPLETED; supplied start/end dates must be chronological, unknown dates/financial/agricultural results remain null. Activity types: WATERING, FERTILIZING, PESTICIDE, INSPECTION, OTHER.

## Frontend approach

Reuse AppShell, ProtectedRoute, useApiQuery/apiClient, DataTable, SectionCard, ManagementDialog, ApiState, existing form feedback and status badges. Add role-aware list/detail/form routes for each operational module, technician My Jobs and engineer work queue. Admin/owner dashboards aggregate real workflow counts and historical series. Public consultation CTA opens a working persisted form; published device/policy content is read from API. Password recovery/reset and authenticated password change connect to existing endpoints. Remove unused ecommerce routes/pages only after verifying they have no required purpose.

## Verification contract

Each phase must pass build and relevant tests before the next. Final evidence includes complete Release unit and disposable-PostgreSQL integration suites, role and cross-farm/job/engineer IDOR tests, invalid transitions, rollback/concurrency/idempotency cases, frontend tests/lint/build, real API/browser integration for five requested business chains, route and notification-link checks. Operational NFRs such as availability/backups/load targets require explicit evidence or remain unverified; local green unit tests do not prove production availability.

### Installation report/acceptance implementation design
Continue Phase 3 with immutable InstallationReport revisions and relational InstalledDeviceCheck rows. Each row references one concrete registered sensor node/UAV/gateway and a proposal line, records actual location, accessory check, connectivity/operation outcomes and test evidence. Sensor nodes must belong to the job farm. Each report must cover proposed line quantities; no duplicate physical device may appear. Capture failed checks with issues rather than fabricating success; final approval must reject failed checks. Owner confirmation and admin decision bind to the exact report, with reviewer/time/note retained on returned versions. Images remain a required follow-up through authorized upload storage, not arbitrary URLs.
Transitions: IN_PROGRESS or REWORK -> REPORT_SUBMITTED -> OWNER_CONFIRMED -> APPROVED. Admin return from submitted/owner-confirmed -> REWORK; technician correction inserts new report and resets confirmation. Stale job revision or old report ID conflicts. Technician completion does not activate rental. Require explicit linked current owner handover confirmation (no bypass in current bound-customer flow). Treat literals as implementation assumptions, preserve all SRS responsibilities. Add API chain checks before implementation; test currently running against missing report routes.
