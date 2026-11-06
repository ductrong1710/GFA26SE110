# Current functional evidence

Compared with section 2.2.4 of Report3_Software_Requirement_Specification_Updated_Flow. The October gap analysis records the baseline. November browser checks use Edge, Kestrel and disposable PostgreSQL. The table includes the earlier five business scenarios and later focused checks for account management, email recovery, telemetry, sensor refresh and role dashboards. Each row identifies the scope of its evidence.

| UC | Actor and entry point | Path to stored state | Executed evidence and remaining checks |
|---|---|---|---|
| 01 | Visitor / homepage | PublicContent → published entries | Public browser sees published device, prices and removal after withdrawal. PublicContentApiTests checks visibility. |
| 02 | Visitor / consultation form | Consultations → consultation and staff notifications | Public browser submits and receives request ID. ConsultationTests rejects invalid intake. |
| 03 | Admin / settings | PublicContentAdmin → public content revision | Browser creates draft, publishes and withdraws it. Independent visitor session verifies visibility. |
| 04 | Engineer / consultations | ConsultationService → assignment and follow-up history | Browser claims and follows up. ConsultationTests exercises competing claims and other-engineer denial. |
| 05 | Assigned engineer / survey | ConsultationService → survey revisions, zones and evidence | Browser saves survey revision. ConsultationTests checks nullable values, history, scope and stale revisions. |
| 06 | Assigned engineer / proposal | Proposals → proposal revision and device lines | Browser stores proposed devices and confirms customer plan. |
| 07 | Engineer or Admin / proposal | Proposals → external deposit confirmation | Engineer browser records external confirmation. ConsultationTests checks original confirmation survives dossier submission. |
| 08 | Assigned engineer / dossier | InstallationDossierService → submitted request and bill | Browser submits. InstallationDossierTests rejects missing evidence and unauthorized actors. |
| 09 | Admin / dossier review | InstallationDossierService → request, customer, farm and membership | Browser approves bound dossier. Integration tests cover return and deferred account provisioning for unbound customers. |
| 10 | Admin / installation requests | InstallationJobs → job and assignment history | Browser assigns technician. OperationalWorkflowTests exercises schedule conflicts. |
| 11 | Assigned technician / installation jobs | InstallationReports → checks, report and evidence | Browser starts assigned job, submits tests and handover images. |
| 12 | Owner / installation jobs | InstallationReports → owner confirmation | Browser confirms handover before final review. |
| 13 | Admin / installation jobs | InstallationReports → decision and rental draft | Browser approves. Integration suite rejects approval without owner confirmation and preserves returned reports. |
| 14 | Admin / rental contracts | RentalService and worker → contracts, dates, assignments and history | Browser confirms agreed start and waits for activation. ConsultationTests covers extension, expiration and ending. |
| 15 | Owner / rental contracts | RentalContracts → scoped contract and assignments | Browser opens active contract and sees the assigned UAV. |
| 16 | Admin / installation allocation and service review | InstallationDevices and service plans → allocation and replacement records | Browser saves allocation. OperationalWorkflowTests verifies replacement commits only at final approval. |
| 17 | Owner / rental and device pages | RentalContracts and equipment reads → scoped assignment | Browser sees assigned equipment under contract. FarmScopeSecurityTests and MissionTests exercise owner boundaries. |
| 18 | Owner or Admin / sensor detail | SensorData → stored readings by channel | Browser verifies the accepted value 21 in both latest readings and channel history. |
| 19 | Owner or Admin / channel history | SensorData → chronological stored readings | Monitoring browser reads 21 from the seeded channel history. |
| 20 | Owner or Admin / comparison | SensorData and analytics → type-filtered readings | Browser verifies per-sensor and per-zone min/max/mean and counts for South orchard (21) and North orchard (27). api-zone-comparison.test.js directly exercises production analytics: Celsius and humidity remain separate, other-farm data is excluded, hourly gaps align as null, and real zero readings survive. |
| 21 | Admin / sensor configuration | ThresholdService → thresholds and change history | Browser writes maximum 35 and sees persisted history. Owner has no write control. ThresholdTests checks API boundaries. |
| 22 | Owner or Admin / alerts | AlertService → alert, state history and private notifications | AlertTests verifies deduplication, timeout, battery and lifecycle. Release monitoring.spec.js triggers a threshold alert through real gateway sync, verifies Owner has no acknowledge action, and checks Admin acknowledgement, note and closure persist in the Owner's readable history. |
| 23 | Owner / incidents | DeviceIncidents → incident and evidence | Browser reports actual rented UAV fault. DeviceIncidentTests covers lost membership, returned equipment and report history. |
| 24 | Admin / incident detail | ServiceJobService → incident-linked assignment | Browser assigns repair. Scheduler creates maintenance assignment independently. |
| 25 | Assigned technician / service jobs | ServiceJobService and ServiceReportService → maintenance job and report | Browser starts the generated maintenance job, obtains approval for the service plan, submits the signed report, confirms operation as owner and closes as Admin. The schedule history shows CLOSED. |
| 26 | Assigned technician / service plan and report | Operations and ServiceReports → plan, signed evidence, replacement and report | Browser completes repair twice. OperationalWorkflowTests and ServiceReplacementTests verify replacement and history. A later inspection references the same replacement ID after approval and creates no second exchange. |
| 27 | Linked owner / service report | ServiceReports → owner confirmation | Browser confirms operation in both cycles. |
| 28 | Admin / service review and assignment | ServiceReports and ServiceJobRepository → closure, reopening, incident state and history | Browser closes, reopens and closes again. Integration tests reject stale revision, returned successor and ended contract. |
| 29 | Admin / maintenance schedules | MaintenanceScheduleService and worker → schedule, occurrence and job | Browser waits for real due job. MaintenanceDueTests verifies one occurrence and notifications under concurrency. |
| 30 | Owner / crop support | CropSupport → report, zone, crop, sensor references and evidence | Browser submits abnormal symptoms. CropSupportApiTests covers access restrictions. |
| 31 | Assigned engineer / crop support | CropSupport → consultation updates and recommendations | Browser requests information, reads owner reply, recommends survey and resolves. |
| 32 | Owner / mission creation | MissionService → farm, zone, mission, waypoints and targets | Browser plans mission. MissionTests rejects inactive rental, another owner's equipment and incorrect zone. |
| 33 | Mission owner / detail | Missions → state history and notes | Browser updates progress and terminal status. MissionTests checks conflicting schedules and invalid edits. |
| 34 | Owner or Admin / mission monitor | Telemetry and Missions → mission-scoped samples | Release public-consultation browser sends two real gateway-authenticated telemetry samples during an Owner mission. Without reloading, it sees the first position marker, current waypoint and 73% battery, then the second location and unavailable battery/altitude/waypoint values, retaining both history rows. TelemetryTests verifies ingestion, assignment and ordered history. Physical UAV transmission remains unverified. |
| 35 | Owner or Admin / mission results | Missions → terminal outcome, logs and targets | Browser verifies manual completion, pending target and failure reason. MissionTests covers collected, failed and skipped target results. |
| 36 | Owner / failed mission and incident form | DeviceIncidents → mission-linked incident | Browser follows failure to linked incident using the same rented device. |
| 37 | Owner / crop seasons | CropSeasons → scoped season | Browser creates season. CropSeasonApiTests verifies current-owner rights and revision conflicts. |
| 38 | Owner / cultivation log | CropSeasons → activity and evidence | Browser records activity. API tests check evidence and revision scope. |
| 39 | Owner / season completion | CropSeasons → end date and recorded financial/yield values | Browser completes seasons. API tests check unknown values. |
| 40 | Owner / comparison | CropSeasons → comparison of authorized selected seasons | Browser compares completed harvest results. API tests reject inaccessible selected seasons. |
| 41 | Admin / user management | Users → account, roles and active flag | UserManagementTests covers replacement, duplicate email, lock and secret exposure. User-management component suite passed. Release users.spec.js browser creates and edits an account, replaces Owner with Technician, locks it and verifies both login and the existing access token are rejected. |
| 42 | All human roles / login | Auth → session and refresh token | Browser authenticates operational roles. AuthFlowTests and AccountLifecycleTests exercise invalid credentials and disabled accounts. |
| 43 | Account owner / password pages | Auth → password hash, reset token and session revocation | Release password browser changes a password, signs out, requests recovery from login, receives the production SMTP message in a temporary local mailbox, and resets the password through the UI. The consumed token returns 400 on reuse, the previous password fails, and the recovered password signs in. PasswordTests covers expiration and concurrency. |
| 44 | Authenticated user / notification panel | Notifications → user-scoped WEB notification read state | Crop-support browser marks one and all read, reloads and verifies persistence. AlertTests covers notification privacy. |
| 45 | Admin or owner / dashboard | ReportService → scoped aggregates over persisted events | Admin browser sees repair, alert and mission meters. Owner dashboard browser compares sensor values and counts in two zones, shows recent cultivation, and compares the last two owned seasons in the selected farm. Recorded profit is visible and missing harvest results remain blank. ReportTests checks current owner/farm scope. |
| 46 | Registered gateway / sync API | Sync → batch, per-record outcomes and readings | SyncTests verifies retry and partial records. Monitoring browser setup sends real authenticated batch and checks resulting values. |
| 47 | Registered UAV gateway / telemetry API | Telemetry → mission samples | TelemetryTests exercises identity, assignment and ordered history. Physical device path unverified. |
| 48 | Email service / worker | EmailDeliveryService → notification delivery state and SMTP transport | EmailTests verifies worker and failure handling using a substitute sender. SmtpTransportTests sends from the production SMTP adapter to a loopback receiver and checks envelope addresses, subject and body. Deployment TLS and external mailbox receipt remain unverified. |

Deployment availability, backup recovery, TLS, production response times and physical hardware still require environment-specific evidence. The sample administrator provisioning remains the requested exception.

NFR-P02: the Release monitoring browser synchronized new readings while the Owner's sensor detail and analytics pages were open. The latest value and channel history both showed 31 within ten seconds of acceptance. The analytics table then showed 32 within ten seconds of a separate sync. Selecting an earlier historical end retained the earlier value 21 and excluded 32. The test uses Edge, the local API and disposable PostgreSQL. Deployment latency under expected project load remains unmeasured.


UI-02 browser check: the six Release scenarios passed on Chrome for Testing 157.0.8103.38 using the actual local API and disposable PostgreSQL. Edge also passed the six scenarios. Firefox 157.0 passed five scenarios in the full run; crop-support timed out after both pages reloaded during a README edit (Vite reconnected at the edit timestamp). An unchanged, focused crop-support rerun passed in 23.5 seconds on a fresh database with no concurrent frontend edits. Thus each of the six scenarios has passed on Firefox, but this was not a single all-green Firefox suite run. Keep frontend files unchanged during browser tests to avoid development-server reloads. This evidence covers these scenarios on Windows, not every browser/OS combination.

UI-01: Release responsive.spec.js passed on Edge at viewport widths 390 and 768 pixels. It visits the public homepage, signs in as each of Administrator/Owner/Technician/Engineer, checks page width on dashboards and job/consultation lists, opens the mobile navigation, closes it with Escape and verifies focus returns, then selects a permitted route and verifies the menu closes. This is representative navigation/layout evidence with mostly empty operational lists, not comprehensive populated-form or physical-device coverage.

NFR-R02 local recovery: the Release crop-season browser populated a disposable PostgreSQL 18 database, then `Test-Postgres.ps1 -VerifyBackup` completed a custom-format pg_dump and restored it into a separate database. Row counts, sorted row-content fingerprints and sequence values matched across all public tables. No application database was touched. Deployment backup scheduling/retention/off-site recovery remain unverified; the user confirmed there is no staging environment and requested local testing first.

## Nonfunctional completion audit

These rows supplement the 48 use cases; evidence is limited to the inspected code and executed local checks described above. They are not deployment certifications.

| Requirement | Current evidence | Remaining verification |
|---|---|---|
| NFR-U01 | Shared navigation, status components and browser workflow chains | Terminology/validation consistency across every form |
| NFR-U02 | Operational browser journeys use visible controls | Usability with intended users |
| NFR-U03 | Component tests include loading, empty, conflict and validation states | Exhaustive route/state matrix |
| NFR-U04 | Monitoring browser and comparison tests distinguish units, ranges and missing values | All chart variants |
| NFR-R01 | No staging environment | 99% deployed availability observation |
| NFR-R02 | Local logical backup/restore and row/sequence comparison passed | Deployment policy, scheduling, retention and off-site recovery |
| NFR-R03 | SyncTests covers partial results and retry/duplicate prevention | Physical gateway offline/reconnect |
| NFR-R04 | Browser reloads preserve workflow outcomes; API tests inspect histories | Recovery of a complete operational dataset |
| NFR-R05 | EmailTests checks failed delivery and persistence of the committed alert | External service failure in deployment |
| NFR-P01 | Local workflows pass | Defined expected load and measured response-time distribution |
| NFR-P02 | Monitoring browser asserts new accepted readings visible within ten seconds | Expected-load/deployment latency |
| NFR-P03 | Report/sensor comparison services cap ranges at 366 days; report repository uses SQL aggregates and bounded history | Query plans and volume tests |
| NFR-P04 | Relational farm/zone/device/job/mission/season scopes and multi-owner API tests | Expected data-volume validation |
| NFR-S01 | Login/refresh/disabled-account API coverage and browser sessions | Deployment transport/session review |
| NFR-S02 | Password service/hash-upgrade tests and secret-response tests | End-to-end deployed logging audit; retained sample admin is the user exception |
| NFR-S03 | Repository scope checks, cross-farm and assignment-denial API tests | Maintain checks as workflow permissions change |
| NFR-S04 | Real authenticated gateway ingestion in monitoring/mission browser fixtures | Physical gateway credential provisioning |
| NFR-S05 | API enables HTTPS redirection | Deployment certificate, proxy and TLS verification |
| NFR-S06 | Unexpected API errors use generic text; email failure tests reject secret details | Full client/server/deployment log review |
| NFR-M01 | Existing Controller/Application/Repository/Domain boundaries retained | Future device extension is not demonstrated by current tests |
| NFR-M02 | Backend unit and integration suites cover business rules and authorization | Final consolidated suite/CI result at delivery |
| NFR-M03 | Frontend Node/API-adapter, component and real-browser tests exist and have run | Final consolidated suite/CI result at delivery |
| NFR-M04 | API regressions and browser chains exercise invalid and valid transitions | Check each SRS alternative against its named test |

UC08 alternative-flow browser evidence: the Release public-consultation scenario now returns the first dossier with an administrator reason. The Engineer reloads, opens revision 1 and sees the reason and retained bill, prepares revision 2 with a corrected location, repeats the external confirmations and submits a new bill. Admin sees the corrected location before approval; the same browser chain continues through installation, rental, mission and two service cycles. This scenario passed in 2.9 minutes. Backup/restore afterward matched all public-table row fingerprints and sequences for the resulting operational dataset, extending the earlier crop-season-only recovery check.

Installation/rental alternative-flow API mapping: `ConsultationTests.External_dossier_bill_is_reviewed_before_installation_assignment_and_paper_rental` exercises revision conflicts, reassignment, failed connectivity, owner rejection, administrator return for repeated checks, preserved report history, rental expiry/extension and final equipment return. `InstallationDossierTests.Returned_dossier_keeps_old_bill_and_requires_a_new_revision` specifically asserts immutable returned-bill history. Browser evidence above covers dossier correction; it does not separately establish every installation-report rejection and rental-end UI variant.

UC11 installation rework browser evidence: the Release public-consultation scenario passed with two report revisions. After the first Owner confirmation, Admin requests rework with a reason. The assigned Technician reopens revision 1, sees that returned decision, and submits revision 2 with fresh signed-document evidence. Owner confirms the new revision; Admin opens the old revision and sees its original notes without an approval action, then opens and approves revision 2. The same scenario continues through rental activation, missions, repair/reopening and scheduled maintenance. The complete scenario passed in 3.5 minutes. The first test attempt encountered duplicate evidence-image selectors while both the old report and the new form were visible; scoping upload assertions to the Technician installation report region resolved the test ambiguity without changing production logic.

Completion audit follow-up (2026-11-06): SRS appendix 5.3 specifies MSG01–MSG31, not just the presence of feedback. Current API client uses a generic Vietnamese success toast and its network/500/session fallbacks differ from MSG30/MSG31. SurveyPanel shows a revision-specific inline notice rather than the MSG06 success-toast wording. Existing successful workflows prove feedback is present, but do not prove alignment with the full message catalog. Audit the corresponding call sites/tests before treating this appendix as complete; preserve useful revision/receipt details while aligning action feedback.

Consolidated local verification 2026-11-06, backend ac6af02/frontend 9778d6a: Release backend build completed with zero warnings/errors and 157 unit + 218 integration tests passed (`.local/srs-consolidated.log`). Frontend passed 117 Node + 127 component + 3 user-management tests; lint passed with the existing PaperEvidence Fast Refresh warning and production build passed with the existing bundle-size warning. The full Edge browser suite passed 7/7 in 5.7 minutes, including responsive navigation and both dossier/report rework additions. Post-suite pg_dump/pg_restore verification matched all public-table fingerprints and sequence values. Both commit heads had successful GitHub Actions runs. These results do not close the message-catalog audit or environment-specific limitations above.
