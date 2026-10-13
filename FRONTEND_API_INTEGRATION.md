# Frontend API Integration Report

## Summary

**IMPLEMENTED — 2026-10-12. Source audit and automated verification completed. Live browser success-path verification remains NOT_TESTED because the local HTTPS certificate is untrusted. This report distinguishes code integration from end-to-end browser proof.**

| Status | Count |
|---|---:|
| Total backend endpoints reviewed | 85 |
| Connected to frontend | 71 |
| Backend available but not connected | 0 pending implementation; 6 intentionally unused alternatives below |
| Frontend needs API but backend missing | 0 confirmed |
| Contract mismatch | 6 feature groups resolved through adapters and explicit unsupported-field handling |
| Role mismatch | Operator unsupported as human JWT role; real path disabled |
| Intentionally not integrated | 6 endpoint alternatives + 5 demo feature groups |
| Device-only | 8 |
| Blocked / pending | Browser certificate trust only (verification limitation) |

Endpoint totals are exclusive: CONNECTED + NOT_REQUIRED + DEVICE_ONLY =85. Demo/contract findings overlap feature areas. Backend and frontend remain separate branches; no commits, pushes, merges, or schema changes. The pre-existing frontend relocation is preserved.

## Endpoint Matrix

| Area | Method | Endpoint | Backend Status | Frontend Screen/Feature | Frontend Integration Status | Reason / Issue | Required Action |
|---|---|---|---|---|---|---|---|
| Alerts | GET | /api/alerts | Implemented | AlertsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Alerts | GET | /api/alerts/{id} | Implemented | AlertsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Alerts | POST | /api/alerts/{id}/acknowledge | Implemented | AlertsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Alerts | POST | /api/alerts/{id}/notes | Implemented | AlertsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Alerts | POST | /api/alerts/{id}/close | Implemented | AlertsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Auth | POST | /api/auth/login | Implemented | AccountPage / AuthProvider | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Auth | POST | /api/auth/refresh | Implemented | AccountPage / AuthProvider | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Auth | POST | /api/auth/logout | Implemented | AccountPage / AuthProvider | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Auth | GET | /api/auth/me | Implemented | AccountPage / AuthProvider | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| DeviceMissions | GET | /api/device/gateways/{gatewayId}/missions/next | Implemented | Device firmware | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| DeviceMissions | POST | /api/device/gateways/{gatewayId}/missions/{missionId}/downloaded | Implemented | Device firmware | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| DeviceMissions | POST | /api/device/gateways/{gatewayId}/missions/{missionId}/start | Implemented | Device firmware | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| DeviceMissions | POST | /api/device/gateways/{gatewayId}/missions/{missionId}/complete | Implemented | Device firmware | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| DeviceMissions | POST | /api/device/gateways/{gatewayId}/missions/{missionId}/fail | Implemented | Device firmware | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| DeviceMissions | POST | /api/device/gateways/{gatewayId}/missions/{missionId}/cancel | Implemented | Device firmware | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| FarmMembers | GET | /api/farms/{farmId}/members | Implemented | FarmsPage (membership workflow absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| FarmMembers | POST | /api/farms/{farmId}/members | Implemented | FarmsPage (membership workflow absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| FarmMembers | DELETE | /api/farms/{farmId}/members/{userId} | Implemented | FarmsPage (membership workflow absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Farms | GET | /api/farms | Implemented | FarmsPage / AppShell | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Farms | GET | /api/farms/{id} | Implemented | FarmsPage / AppShell | NOT_REQUIRED | FarmsPage uses the same FarmResponse from the farm catalog. | None |
| Farms | POST | /api/farms | Implemented | FarmsPage / AppShell | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Farms | PUT | /api/farms/{id} | Implemented | FarmsPage / AppShell | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Farms | PATCH | /api/farms/{id}/status | Implemented | FarmsPage / AppShell | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Farms | GET | /api/farms/{farmId}/zones | Implemented | FarmsPage / AppShell | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Farms | POST | /api/farms/{farmId}/zones | Implemented | FarmsPage / AppShell | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Gateways | GET | /api/gateways | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Gateways | GET | /api/gateways/{id} | Implemented | DevicesPage | NOT_REQUIRED | DevicesPage edits the complete GatewayResponse from the inventory list. | None |
| Gateways | POST | /api/gateways | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Gateways | PUT | /api/gateways/{id} | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Gateways | PATCH | /api/gateways/{id}/status | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Gateways | POST | /api/gateways/{id}/assign-uav | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | GET | /api/missions | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | GET | /api/missions/{id} | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | POST | /api/missions | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | PUT | /api/missions/{id} | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | POST | /api/missions/{id}/schedule | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | POST | /api/missions/{id}/start | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | POST | /api/missions/{id}/complete | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | POST | /api/missions/{id}/fail | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | POST | /api/missions/{id}/cancel | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | PATCH | /api/missions/{id}/status | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | NOT_REQUIRED | Mission lifecycle buttons call explicit start/complete/fail/cancel actions. The generic status alternative is unnecessary for these workflows. | None |
| Missions | GET | /api/missions/{id}/results | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | GET | /api/missions/{id}/waypoints | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Missions | GET | /api/missions/{id}/logs | Implemented | MissionsPage / CreateMissionPage / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Notifications | GET | /api/notifications | Implemented | AppTopbar (notification list absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Notifications | PATCH | /api/notifications/{id}/read | Implemented | AppTopbar (notification list absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Reports | GET | /api/dashboard/overview | Implemented | ReportsPage / AdminDashboard / FarmOwnerDashboard / EngineerDashboard | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Reports | GET | /api/reports/sensor-data | Implemented | ReportsPage / AdminDashboard / FarmOwnerDashboard / EngineerDashboard | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Reports | GET | /api/reports/missions | Implemented | ReportsPage / AdminDashboard / FarmOwnerDashboard / EngineerDashboard | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Reports | GET | /api/reports/alerts | Implemented | ReportsPage / AdminDashboard / FarmOwnerDashboard / EngineerDashboard | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Reports | GET | /api/reports/devices | Implemented | ReportsPage / AdminDashboard / FarmOwnerDashboard / EngineerDashboard | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorChannels | PUT | /api/sensor-channels/{id} | Implemented | SensorDetailPage (configuration workflow absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorData | GET | /api/sensor-readings | Implemented | SensorDataPage / ZoneComparison / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorData | GET | /api/sensor-channels/{id}/history | Implemented | SensorDataPage / ZoneComparison / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorData | GET | /api/sensor-nodes/{id}/latest | Implemented | SensorDataPage / ZoneComparison / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorData | GET | /api/zones/compare | Implemented | SensorDataPage / ZoneComparison / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorData | GET | /api/missions/{missionId}/collection-attempts | Implemented | SensorDataPage / ZoneComparison / MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | GET | /api/sensor-nodes | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | GET | /api/sensor-nodes/{id} | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | POST | /api/sensor-nodes | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | PUT | /api/sensor-nodes/{id} | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | PATCH | /api/sensor-nodes/{id}/status | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | GET | /api/sensor-nodes/{sensorNodeId}/channels | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorNodes | POST | /api/sensor-nodes/{sensorNodeId}/channels | Implemented | SensorsPage / SensorDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorTypes | GET | /api/sensor-types | Implemented | SensorsPage / SettingsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| SensorTypes | POST | /api/sensor-types | Implemented | SensorsPage / SettingsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Sync | POST | /api/device/gateways/{gatewayId}/sync | Implemented | SyncPage | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| Telemetry | POST | /api/device/telemetry | Implemented | MissionDetailPage | DEVICE_ONLY | Gateway/device authentication boundary; never called by the human browser | None |
| Telemetry | GET | /api/missions/{id}/telemetry/latest | Implemented | MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Telemetry | GET | /api/missions/{id}/telemetry | Implemented | MissionDetailPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Thresholds | GET | /api/sensor-channels/{id}/threshold | Implemented | SensorDataPage / SensorDetailPage (channel editor absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Thresholds | PUT | /api/sensor-channels/{id}/threshold | Implemented | SensorDataPage / SensorDetailPage (channel editor absent) | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Uavs | GET | /api/uavs | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Uavs | GET | /api/uavs/{id} | Implemented | DevicesPage | NOT_REQUIRED | DevicesPage edits the complete UavResponse from the inventory list. | None |
| Uavs | POST | /api/uavs | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Uavs | PUT | /api/uavs/{id} | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Uavs | PATCH | /api/uavs/{id}/status | Implemented | DevicesPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Users | GET | /api/users | Implemented | UsersPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Users | GET | /api/users/{id} | Implemented | UsersPage | NOT_REQUIRED | UsersPage uses the complete ManagedUserResponse returned by the paginated directory. There is no separate user-detail route. | None |
| Users | POST | /api/users | Implemented | UsersPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Users | PUT | /api/users/{id} | Implemented | UsersPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Users | PATCH | /api/users/{id}/status | Implemented | UsersPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Users | PUT | /api/users/{id}/roles | Implemented | UsersPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |
| Zones | GET | /api/zones/{id} | Implemented | FarmsPage | NOT_REQUIRED | FarmsPage uses the same ZoneResponse from the farm zones catalog. | None |
| Zones | PUT | /api/zones/{id} | Implemented | FarmsPage | CONNECTED | Real API request, DTO adaptation and async screen workflow implemented; live browser success path TLS-blocked | Browser smoke after certificate trust |

## Request, Response and Authorization Inventory

Application DTOs are defined in src/FarmMonitoring.Application/Features/<feature>/*Contracts.cs. API envelopes are ApiResponse<T> and ApiPageResponse<T>; page metadata must survive central unwrapping. JSON uses camelCase. PageQuery provides page/pageSize/search. Route identifiers are positive backend integers. All human farm-scoped calls also enforce current membership; administrators have global access. UAV/gateway catalogs and sensor types are global catalogs.

| Method / Endpoint | Controller action | Request/query DTO | Response contract | Authorization |
|---|---|---|---|---|
| GET /api/alerts | AlertsController.cs:List | AlertQuery | ApiPageResponse<AlertResponse> | ReadFarmData |
| GET /api/alerts/{id} | AlertsController.cs:Get | route identifiers / no JSON body | ApiResponse<AlertResponse> | ReadFarmData |
| POST /api/alerts/{id}/acknowledge | AlertsController.cs:Acknowledge | AlertNoteRequest | ApiResponse<AlertResponse> | ReadFarmData |
| POST /api/alerts/{id}/notes | AlertsController.cs:Note | AlertNoteRequest | ApiResponse<AlertResponse> | ReadFarmData |
| POST /api/alerts/{id}/close | AlertsController.cs:Close | AlertNoteRequest | ApiResponse<AlertResponse> | ReadFarmData |
| POST /api/auth/login | AuthController.cs:Login | LoginRequest | ApiResponse<AuthResponse> | Anonymous |
| POST /api/auth/refresh | AuthController.cs:Refresh | RefreshRequest | ApiResponse<AuthResponse> | Anonymous |
| POST /api/auth/logout | AuthController.cs:Logout | RefreshRequest | ApiResponse<object> | JWT authenticated |
| GET /api/auth/me | AuthController.cs:Me | route identifiers / no JSON body | ApiResponse<UserResponse> | JWT authenticated |
| GET /api/device/gateways/{gatewayId}/missions/next | DeviceMissionsController.cs:Next | route identifiers / no JSON body | ApiResponse<DeviceMissionResponse?> | DeviceKey: X-Api-Key + X-Gateway-Code |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/downloaded | DeviceMissionsController.cs:Downloaded | route identifiers / no JSON body | ApiResponse<DeviceMissionStateResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/start | DeviceMissionsController.cs:Start | route identifiers / no JSON body | ApiResponse<DeviceMissionStateResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/complete | DeviceMissionsController.cs:Complete | route identifiers / no JSON body | ApiResponse<DeviceMissionStateResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/fail | DeviceMissionsController.cs:Fail | DeviceMissionFailureRequest | ApiResponse<DeviceMissionStateResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/cancel | DeviceMissionsController.cs:Cancel | DeviceMissionCancelRequest | ApiResponse<DeviceMissionStateResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| GET /api/farms/{farmId}/members | FarmMembersController.cs:List | PageQuery | ApiPageResponse<FarmMemberResponse> | ManageUsers |
| POST /api/farms/{farmId}/members | FarmMembersController.cs:Assign | FarmMemberRequest | ApiResponse<FarmMemberResponse> | ManageUsers |
| DELETE /api/farms/{farmId}/members/{userId} | FarmMembersController.cs:Remove | route farmId / optional query farmId |  | ManageUsers |
| GET /api/farms | FarmsController.cs:List | PageQuery | ApiPageResponse<FarmResponse> | ReadFarmData |
| GET /api/farms/{id} | FarmsController.cs:Get | route identifiers / no JSON body | ApiResponse<FarmResponse> | ReadFarmData |
| POST /api/farms | FarmsController.cs:Create | FarmRequest | ApiResponse<FarmResponse> | ManageFarms |
| PUT /api/farms/{id} | FarmsController.cs:Update | FarmRequest | ApiResponse<FarmResponse> | ManageFarms |
| PATCH /api/farms/{id}/status | FarmsController.cs:Status | FarmStatusRequest | ApiResponse<FarmResponse> | ManageFarms |
| GET /api/farms/{farmId}/zones | FarmsController.cs:Zones | PageQuery | ApiPageResponse<ZoneResponse> | ReadFarmData |
| POST /api/farms/{farmId}/zones | FarmsController.cs:CreateZone | ZoneRequest | ApiResponse<ZoneResponse> | ManageFarms |
| GET /api/gateways | GatewaysController.cs:List | PageQuery | ApiPageResponse<GatewayResponse> | ReadFarmData |
| GET /api/gateways/{id} | GatewaysController.cs:Get | route identifiers / no JSON body | ApiResponse<GatewayResponse> | ReadFarmData |
| POST /api/gateways | GatewaysController.cs:Create | GatewayRequest | ApiResponse<GatewayResponse> | ManageDevices |
| PUT /api/gateways/{id} | GatewaysController.cs:Update | GatewayRequest | ApiResponse<GatewayResponse> | ManageDevices |
| PATCH /api/gateways/{id}/status | GatewaysController.cs:Status | EquipmentStatusRequest | ApiResponse<GatewayResponse> | ManageDevices |
| POST /api/gateways/{id}/assign-uav | GatewaysController.cs:Assign | AssignUavRequest | ApiResponse<GatewayResponse> | ManageDevices |
| GET /api/missions | MissionsController.cs:List | MissionQuery | ApiPageResponse<MissionSummary> | ReadFarmData |
| GET /api/missions/{id} | MissionsController.cs:Get | route identifiers / no JSON body | ApiResponse<MissionResponse> | ReadFarmData |
| POST /api/missions | MissionsController.cs:Create | MissionRequest | ApiResponse<MissionResponse> | ManageMissions |
| PUT /api/missions/{id} | MissionsController.cs:Update | MissionRequest | ApiResponse<MissionResponse> | ManageMissions |
| POST /api/missions/{id}/schedule | MissionsController.cs:Schedule | ScheduleMissionRequest | ApiResponse<MissionResponse> | ManageMissions |
| POST /api/missions/{id}/start | MissionsController.cs:Start | route identifiers / no JSON body | ApiResponse<MissionResponse> | ManageMissions |
| POST /api/missions/{id}/complete | MissionsController.cs:Complete | MissionActionRequest | ApiResponse<MissionResponse> | ManageMissions |
| POST /api/missions/{id}/fail | MissionsController.cs:Fail | MissionActionRequest | ApiResponse<MissionResponse> | ManageMissions |
| POST /api/missions/{id}/cancel | MissionsController.cs:Cancel | MissionActionRequest | ApiResponse<MissionResponse> | ManageMissions |
| PATCH /api/missions/{id}/status | MissionsController.cs:Status | MissionStatusRequest | ApiResponse<MissionResponse> | ManageMissions |
| GET /api/missions/{id}/results | MissionsController.cs:Results | route identifiers / no JSON body | ApiResponse<MissionResults> | ReadFarmData |
| GET /api/missions/{id}/waypoints | MissionsController.cs:Waypoints | route identifiers / no JSON body | ApiResponse<IReadOnlyList<WaypointResponse>> | ReadFarmData |
| GET /api/missions/{id}/logs | MissionsController.cs:Logs | PageQuery | ApiPageResponse<MissionLogResponse> | ReadFarmData |
| GET /api/notifications | NotificationsController.cs:List | NotificationQuery | ApiPageResponse<NotificationResponse> | ReadFarmData |
| PATCH /api/notifications/{id}/read | NotificationsController.cs:Read | route identifiers / no JSON body | ApiResponse<NotificationResponse> | ReadFarmData |
| GET /api/dashboard/overview | ReportsController.cs:Dashboard | route farmId / optional query farmId | ApiResponse<DashboardOverview> | ReadFarmData |
| GET /api/reports/sensor-data | ReportsController.cs:Sensors | ReportQuery | ApiPageResponse<SensorReportRow> | ReadFarmData |
| GET /api/reports/missions | ReportsController.cs:Missions | ReportQuery | ApiPageResponse<MissionReportRow> | ReadFarmData |
| GET /api/reports/alerts | ReportsController.cs:Alerts | ReportQuery | ApiPageResponse<AlertReportRow> | ReadFarmData |
| GET /api/reports/devices | ReportsController.cs:Devices | DeviceReportQuery | ApiPageResponse<DeviceReportRow> | ReadFarmData |
| PUT /api/sensor-channels/{id} | SensorChannelsController.cs:Update | SensorChannelRequest | ApiResponse<SensorChannelResponse> | ManageDevices |
| GET /api/sensor-readings | SensorDataController.cs:List | ReadingQuery | ApiPageResponse<ReadingResponse> | ReadFarmData |
| GET /api/sensor-channels/{id}/history | SensorDataController.cs:History | ReadingQuery | ApiPageResponse<ReadingResponse> | ReadFarmData |
| GET /api/sensor-nodes/{id}/latest | SensorDataController.cs:Latest | route identifiers / no JSON body | ApiResponse<IReadOnlyList<LatestChannelResponse>> | ReadFarmData |
| GET /api/zones/compare | SensorDataController.cs:Compare | ZoneComparisonQuery | ApiResponse<IReadOnlyList<ZoneComparisonResponse>> | ReadFarmData |
| GET /api/missions/{missionId}/collection-attempts | SensorDataController.cs:Attempts | PageQuery | ApiPageResponse<CollectionAttemptResponse> | ReadFarmData |
| GET /api/sensor-nodes | SensorNodesController.cs:List | SensorNodeQuery | ApiPageResponse<SensorNodeResponse> | ReadFarmData |
| GET /api/sensor-nodes/{id} | SensorNodesController.cs:Get | route identifiers / no JSON body | ApiResponse<SensorNodeResponse> | ReadFarmData |
| POST /api/sensor-nodes | SensorNodesController.cs:Create | SensorNodeRequest | ApiResponse<SensorNodeResponse> | ManageDevices |
| PUT /api/sensor-nodes/{id} | SensorNodesController.cs:Update | SensorNodeRequest | ApiResponse<SensorNodeResponse> | ManageDevices |
| PATCH /api/sensor-nodes/{id}/status | SensorNodesController.cs:Status | SensorNodeStatusRequest | ApiResponse<SensorNodeResponse> | ManageDevices |
| GET /api/sensor-nodes/{sensorNodeId}/channels | SensorNodesController.cs:Channels | PageQuery | ApiPageResponse<SensorChannelResponse> | ReadFarmData |
| POST /api/sensor-nodes/{sensorNodeId}/channels | SensorNodesController.cs:CreateChannel | SensorChannelRequest | ApiResponse<SensorChannelResponse> | ManageDevices |
| GET /api/sensor-types | SensorTypesController.cs:List | PageQuery | ApiPageResponse<SensorTypeResponse> | ReadFarmData |
| POST /api/sensor-types | SensorTypesController.cs:Create | SensorTypeRequest | ApiResponse<SensorTypeResponse> | ManageDevices |
| POST /api/device/gateways/{gatewayId}/sync | SyncController.cs:Synchronize | SyncRequest | ApiResponse<SyncResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| POST /api/device/telemetry | TelemetryController.cs:Record | TelemetryRequest | ApiResponse<TelemetryResponse> | DeviceKey: X-Api-Key + X-Gateway-Code |
| GET /api/missions/{id}/telemetry/latest | TelemetryController.cs:Latest | route identifiers / no JSON body | ApiResponse<TelemetryResponse> | ReadFarmData |
| GET /api/missions/{id}/telemetry | TelemetryController.cs:History | TelemetryQuery | ApiPageResponse<TelemetryResponse> | ReadFarmData |
| GET /api/sensor-channels/{id}/threshold | ThresholdsController.cs:Get | route identifiers / no JSON body | ApiResponse<ThresholdResponse> | ReadFarmData |
| PUT /api/sensor-channels/{id}/threshold | ThresholdsController.cs:Set | ThresholdRequest | ApiResponse<ThresholdResponse> | ConfigureThresholds |
| GET /api/uavs | UavsController.cs:List | PageQuery | ApiPageResponse<UavResponse> | ReadFarmData |
| GET /api/uavs/{id} | UavsController.cs:Get | route identifiers / no JSON body | ApiResponse<UavResponse> | ReadFarmData |
| POST /api/uavs | UavsController.cs:Create | UavRequest | ApiResponse<UavResponse> | ManageDevices |
| PUT /api/uavs/{id} | UavsController.cs:Update | UavRequest | ApiResponse<UavResponse> | ManageDevices |
| PATCH /api/uavs/{id}/status | UavsController.cs:Status | EquipmentStatusRequest | ApiResponse<UavResponse> | ManageDevices |
| GET /api/users | UsersController.cs:List | PageQuery | ApiPageResponse<ManagedUserResponse> | ManageUsers |
| GET /api/users/{id} | UsersController.cs:Get | route identifiers / no JSON body | ApiResponse<ManagedUserResponse> | ManageUsers |
| POST /api/users | UsersController.cs:Create | CreateUserRequest | ApiResponse<ManagedUserResponse> | ManageUsers |
| PUT /api/users/{id} | UsersController.cs:Update | UpdateUserRequest | ApiResponse<ManagedUserResponse> | ManageUsers |
| PATCH /api/users/{id}/status | UsersController.cs:Status | UserStatusRequest | ApiResponse<ManagedUserResponse> | ManageUsers |
| PUT /api/users/{id}/roles | UsersController.cs:Roles | UserRolesRequest | ApiResponse<ManagedUserResponse> | ManageUsers |
| GET /api/zones/{id} | ZonesController.cs:Get | route identifiers / no JSON body | ApiResponse<ZoneResponse> | ReadFarmData |
| PUT /api/zones/{id} | ZonesController.cs:Update | ZoneRequest | ApiResponse<ZoneResponse> | ManageFarms |

## APIs Required by Frontend but Missing in Backend

| Frontend Feature | Needed Endpoint | Reason Needed | Source Documentation | Recommended Backend Work |
|---|---|---|---|---|
| None confirmed | None | Existing supported reads/CRUD/lifecycle/report APIs are implemented | API.md actual controller inventory | Do not invent routes |

SyncPage exposes gateway-local queues, batch history and browser retry. Database documents sync_batches, but API.md only exposes device ingestion; no documented human batch-list/retry contract exists. Classify this specific workflow DEMO_ONLY / NOT_REQUIRED for browser and show an honest unavailable state. Do not call device sync with a human token. Farm/zone physical delete, public registration, commerce, notification preferences, global threshold defaults and PDF export similarly have no supported public contract; adapt or disable rather than inventing routes.

## Backend APIs Available but Not Yet Connected

| Method | Endpoint | Existing Frontend Page | Current Data Source | Work Needed |
|---|---|---|---|---|
| GET | /api/users/{id} | UsersPage | Equivalent real list/action endpoint | NOT_REQUIRED: UsersPage uses the complete ManagedUserResponse returned by the paginated directory. There is no separate user-detail route. |
| GET | /api/farms/{id} | FarmsPage / AppShell | Equivalent real list/action endpoint | NOT_REQUIRED: FarmsPage uses the same FarmResponse from the farm catalog. |
| GET | /api/zones/{id} | FarmsPage | Equivalent real list/action endpoint | NOT_REQUIRED: FarmsPage uses the same ZoneResponse from the farm zones catalog. |
| GET | /api/uavs/{id} | DevicesPage | Equivalent real list/action endpoint | NOT_REQUIRED: DevicesPage edits the complete UavResponse from the inventory list. |
| GET | /api/gateways/{id} | DevicesPage | Equivalent real list/action endpoint | NOT_REQUIRED: DevicesPage edits the complete GatewayResponse from the inventory list. |
| PATCH | /api/missions/{id}/status | MissionsPage / CreateMissionPage / MissionDetailPage | Equivalent real list/action endpoint | NOT_REQUIRED: Mission lifecycle buttons call explicit start/complete/fail/cancel actions. The generic status alternative is unnecessary for these workflows. |

No remaining supported feature intentionally uses a fixture instead of an available API. Code-level workflow/contract audit completed; browser execution remains unverified.

## Contract Mismatches

| Finding | Frontend expectation | Actual backend | Adapter sufficient? | Backend change required? | Chosen resolution |
|---|---|---|---|---|---|
| Farm/zone metadata | ownerId, area, crop, boundary polygon, rich nested objects, delete | FarmRequest name/description/latitude/longitude; ZoneRequest centerLatitude/centerLongitude; no delete | Yes for supported fields; extra fields unsupported | No | Adapt forms/maps; disable physical delete and unsupported fields |
| Users and authentication | fixture role/activeRole, profile fields, no real password requirement | roles array, fullName/email; CreateUserRequest requires password; roles server-authoritative | Yes | No | Normalize three human roles; add password to admin creation; never fabricate authenticated roles |
| Devices | farmId/device protocol/platform and per-farm ownership | sensor node belongs to zone; global UAV/gateway catalogs; gateway gatewayType and uavId | Yes for supported fields | No | Explicit mapping; no fabricated farm ownership; honor supported enums |
| Mission planning | READY/IN_PROGRESS and mock collection-point representation | PENDING/SCHEDULED/RUNNING; explicit waypoints + targetAssignments required for scheduling | Yes | No | Send real IDs and explicit target-to-waypoint assignments; normalize display only |
| Readings/thresholds | sensor-type warning min/max, buffered local values, implicit channel nesting | immutable received readings; per-channel minValue/maxValue/dataTimeoutMinutes/lowBatteryPercent/isEnabled | Yes for supported data | No | Fetch channels/latest/history/threshold; exclude invented gateway-local samples |
| Dashboard/reports | fixture KPIs, rich lists, hourly series, export | overview count groups; paginated report aggregates; no server CSV/PDF route | Yes through supported reads/client CSV | No | Combine actual supported reads per screen; clearly distinguish aggregates from raw history |

## Remaining Runtime Mock Usage

The following files remain reachable through explicit demo providers/pages (including transitive imports). They are not sources for real-auth API views. Static imports may still bundle fixtures; this is code retention, not a runtime fallback. Marketing products remain outside backend scope.

| Mock File | Used By | Why Still Used | Backend Alternative | Action |
|---|---|---|---|---|
| src/data/mock/accounts.js | src/services/mockAuth.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/alertSelectors.js | src/pages/app/DemoAlertsPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/alerts.js | src/data/mock/adminDashboard.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/index.js, src/data/mock/managementState.js, src/data/mock/notifications.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/collectionAttempts.js | src/data/mock/index.js, src/data/mock/missionMonitoring.js, src/data/mock/operationsSelectors.js, src/data/mock/reportSelectors.js, src/data/mock/selectors.js, src/data/mock/sensorReadings.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/farmWorkspace.js | src/layouts/AppShell.jsx, src/pages/app/FarmsPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/farms.js | src/data/mock/farmOwnerDashboard.js, src/data/mock/farmWorkspace.js, src/data/mock/index.js, src/data/mock/missionPlanning.js, src/data/mock/missionWaypoints.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/gateways.js | src/data/mock/adminDashboard.js, src/data/mock/alertSelectors.js, src/data/mock/farmWorkspace.js, src/data/mock/index.js, src/data/mock/operationsState.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/managementState.js | src/context/ManagementProvider.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/missionMonitoring.js | src/context/OperationsProvider.jsx, src/data/mock/operationsState.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/missionPlanning.js | src/context/OperationsProvider.jsx, src/data/mock/farmOwnerDashboard.js, src/data/mock/missionMonitoring.js, src/data/mock/operationsSelectors.js, src/data/mock/operationsState.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/missionTargets.js | src/data/mock/collectionAttempts.js, src/data/mock/index.js, src/data/mock/missionPlanning.js, src/data/mock/operationsSelectors.js, src/data/mock/operationsState.js, src/data/mock/selectors.js, src/data/mock/sensorReadings.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/missionWaypoints.js | src/data/mock/index.js, src/data/mock/missionMonitoring.js, src/data/mock/missionPlanning.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/missions.js | src/data/mock/adminDashboard.js, src/data/mock/alertSelectors.js, src/data/mock/collectionAttempts.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/farmWorkspace.js, src/data/mock/index.js, src/data/mock/missionPlanning.js, src/data/mock/missionTargets.js, src/data/mock/missionWaypoints.js, src/data/mock/operationsSelectors.js, src/data/mock/operationsState.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/notifications.js | src/data/mock/index.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/operationsSelectors.js | src/pages/app/OperatorDashboard.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/operationsState.js | src/context/OperationsProvider.jsx, src/data/mock/operationsSelectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/scenario.js | src/data/mock/collectionAttempts.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/farmWorkspace.js, src/data/mock/gateways.js, src/data/mock/index.js, src/data/mock/managementState.js, src/data/mock/missionMonitoring.js, src/data/mock/missionPlanning.js, src/data/mock/missionWaypoints.js, src/data/mock/notifications.js, src/data/mock/operationsState.js, src/data/mock/reportSelectors.js, src/data/mock/sensorAnalytics.js, src/data/mock/sensorNodes.js, src/data/mock/sensorReadings.js, src/data/mock/syncState.js, src/data/mock/uavs.js, src/pages/app/DemoAlertsPage.jsx, src/pages/app/SettingsPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/selectors.js | src/data/mock/farmOwnerDashboard.js, src/data/mock/index.js, src/data/mock/operationsSelectors.js, src/pages/app/DemoAlertsPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/sensorChannels.js | src/data/mock/alertSelectors.js, src/data/mock/collectionAttempts.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/index.js, src/data/mock/managementState.js, src/data/mock/operationsSelectors.js, src/data/mock/selectors.js, src/data/mock/sensorAnalytics.js, src/data/mock/sensorReadings.js, src/pages/app/SyncPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/sensorNodes.js | src/data/mock/adminDashboard.js, src/data/mock/alertSelectors.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/farmWorkspace.js, src/data/mock/index.js, src/data/mock/missionPlanning.js, src/data/mock/operationsState.js, src/data/mock/selectors.js, src/data/mock/sensorChannels.js, src/data/mock/sensorReadings.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/sensorReadings.js | src/data/mock/alerts.js, src/data/mock/alertSelectors.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/index.js, src/data/mock/operationsSelectors.js, src/data/mock/selectors.js, src/data/mock/sensorAnalytics.js, src/data/mock/syncBatches.js, src/data/mock/syncState.js, src/pages/app/SyncPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/sensorTypes.js | src/data/mock/alertSelectors.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/index.js, src/data/mock/managementState.js, src/data/mock/operationsSelectors.js, src/data/mock/selectors.js, src/data/mock/sensorAnalytics.js, src/data/mock/sensorChannels.js, src/data/mock/sensorReadings.js, src/pages/app/SettingsPage.jsx, src/pages/app/SyncPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/syncBatches.js | src/data/mock/adminDashboard.js, src/data/mock/farmOwnerDashboard.js, src/data/mock/index.js, src/data/mock/operationsSelectors.js, src/data/mock/syncState.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/syncState.js | src/context/OperationsProvider.jsx, src/data/mock/operationsState.js, src/pages/app/SyncPage.jsx | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/uavs.js | src/data/mock/adminDashboard.js, src/data/mock/alertSelectors.js, src/data/mock/farmWorkspace.js, src/data/mock/index.js, src/data/mock/operationsState.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/users.js | src/data/mock/accounts.js, src/data/mock/adminDashboard.js, src/data/mock/index.js, src/data/mock/managementState.js, src/data/mock/selectors.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |
| src/data/mock/zones.js | src/data/mock/farmOwnerDashboard.js, src/data/mock/farmWorkspace.js, src/data/mock/index.js, src/data/mock/selectors.js, src/data/mock/sensorNodes.js | Explicit demo/test graph | Real API services/providers for supported features | Retain only for explicit demo; real paths isolated |

## Device-only APIs

| Endpoint | Authentication | Intended Caller | Why Browser Must Not Call |
|---|---|---|---|
| GET /api/device/gateways/{gatewayId}/missions/next | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/downloaded | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/start | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/complete | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/fail | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/gateways/{gatewayId}/missions/{missionId}/cancel | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/gateways/{gatewayId}/sync | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |
| POST /api/device/telemetry | DeviceKey: X-Api-Key + X-Gateway-Code | Assigned gateway/device software | Device keys and execution/ingestion are separate from human JWT authorization |

## CORS Verification

- Frontend origin tested by ASP.NET integration tests: http://localhost:5173.
- Backend smoke origin: https://localhost:7043; http://localhost:5043 redirects to HTTPS.
- Named policy: Frontend. Development allowed origins: http://localhost:5173 and https://localhost:5173. Production origins configurable through Cors:AllowedOrigins, denied by default when absent.
- OPTIONS /api/farms: PASS, 204 and exact Access-Control-Allow-Origin for allowed origin. Authorization and Content-Type allowed; GET/POST/PUT/PATCH/DELETE/OPTIONS verified. No credentials or wildcard origin.
- Nine executable CORS tests pass, including denied origins and allowed production-origin headers on 401. Middleware is after routing and before authentication/authorization.
- Browser success-path verification: BLOCKED by local HTTPS certificate trust (ERR_CERT_AUTHORITY_INVALID). Login displays recoverable network error. Certificate warning was not bypassed. Automatic approval review rejected an attempted server stop/relaunch; no reason beyond blocked by policy was supplied, and the action was not retried.

## Authentication Integration Status

| Behavior | Status | Evidence / Required Work |
|---|---|---|
| login | PASS | Real backend request/token response; success and failure tested with mocked fetch |
| token storage/session abstraction | PASS | Central sessionStorage abstraction; no component token access |
| /api/auth/me | PASS | Startup restore coalescing and stale-response protection tested |
| refresh | PASS | Rotated token response stored, original request retried once |
| logout | PASS | Bearer plus refresh token body; offline local cleanup and refresh race tested |
| expired token handling | PASS | One refresh then one retry, retry failure clears matching session |
| refresh concurrency handling | PASS | Concurrent requests share one refresh; delayed 401 uses rotated token |
| redirect/clear session on refresh failure | PARTIAL | Session clearing/subscriber tested; ProtectedRoute wired; successful live browser flow blocked by TLS |
| role mapping | PASS | Three backend human roles mapped explicitly; operator never mapped |

These are unit/code verification statuses, not claims of successful live browser authentication. Nineteen API-client tests pass. Real auth is the default; mock auth requires explicit VITE_ENABLE_MOCK_AUTH=true. DemoRoleSwitcher only applies to explicit mock sessions. Mapping: FarmAdministrator -> ADMINISTRATOR; FarmOwner -> FARM_OWNER; FarmEngineer -> AGRICULTURAL_ENGINEER. UAV_DEVICE_OPERATOR is an external actor and its real-auth path is disabled.
## Frontend Screen Coverage

| Screens/features | Current data / behavior |
|---|---|
| AccountPage/AuthProvider | Real login/me/refresh/logout; explicit demo opt-in only |
| UsersPage | Paginated directory; create, basic edit, role assignment, activate/deactivate |
| FarmsPage/AppShell | Farm/zone catalogs and CRUD; farm activation; administrator membership list/assign/remove |
| SensorsPage/SensorDetailPage | Node CRUD/status, channels/types CRUD, latest values, channel history, thresholds |
| DevicesPage | Global UAV/gateway inventories, CRUD/status and gateway assignment |
| MissionsPage/CreateMissionPage/MissionDetailPage | Planning, scheduling, lifecycle, targets/results/waypoints/logs/attempts and latest/history telemetry |
| SensorDataPage/ZoneComparison | Received readings, actual channel/type metadata, configured thresholds, server zone aggregates |
| AlertsPage/AppTopbar | Alert list/detail/actions/history and private notifications/read state |
| Role dashboards | Dashboard overview plus screen-scoped catalog/reading data; no fixture fallback in real sessions |
| ReportsPage | Four paginated server report aggregates with local CSV export |
| SyncPage/SettingsPage | Demo-only local queue/global settings; real interactions disabled |
| OperatorDashboard | Demo-only; unsupported human role never mapped |
| Public registration | Disabled for real mode; administrator creates accounts |
| Public commerce/marketing | DEMO_ONLY; no farm-monitoring backend contract |


## Issue: GET /api/device/gateways/{gatewayId}/missions/next

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: Device firmware (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs; action: Next; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<DeviceMissionResponse?>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/gateways/{gatewayId}/missions/{missionId}/downloaded

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: Device firmware (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs; action: Downloaded; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<DeviceMissionStateResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/gateways/{gatewayId}/missions/{missionId}/start

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: Device firmware (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs; action: Start; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<DeviceMissionStateResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/gateways/{gatewayId}/missions/{missionId}/complete

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: Device firmware (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs; action: Complete; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<DeviceMissionStateResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/gateways/{gatewayId}/missions/{missionId}/fail

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: Device firmware (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs; action: Fail; request DTO/query: DeviceMissionFailureRequest; response DTO: ApiResponse<DeviceMissionStateResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/gateways/{gatewayId}/missions/{missionId}/cancel

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: Device firmware (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/DeviceMissionsController.cs; action: Cancel; request DTO/query: DeviceMissionCancelRequest; response DTO: ApiResponse<DeviceMissionStateResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: GET /api/farms/{id}

Status: OTHER (NOT_REQUIRED)

Frontend location:
- file/component: FarmsPage / AppShell (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/FarmsController.cs; action: Get; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<FarmResponse>.

Problem: FarmsPage uses the same FarmResponse from the farm catalog.

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: GET /api/gateways/{id}

Status: OTHER (NOT_REQUIRED)

Frontend location:
- file/component: DevicesPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/GatewaysController.cs; action: Get; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<GatewayResponse>.

Problem: DevicesPage edits the complete GatewayResponse from the inventory list.

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: PATCH /api/missions/{id}/status

Status: OTHER (NOT_REQUIRED)

Frontend location:
- file/component: MissionsPage / CreateMissionPage / MissionDetailPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/MissionsController.cs; action: Status; request DTO/query: MissionStatusRequest; response DTO: ApiResponse<MissionResponse>.

Problem: Mission lifecycle buttons call explicit start/complete/fail/cancel actions. The generic status alternative is unnecessary for these workflows.

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/gateways/{gatewayId}/sync

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: SyncPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/SyncController.cs; action: Synchronize; request DTO/query: SyncRequest; response DTO: ApiResponse<SyncResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: POST /api/device/telemetry

Status: OTHER (DEVICE_ONLY)

Frontend location:
- file/component: MissionDetailPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/TelemetryController.cs; action: Record; request DTO/query: TelemetryRequest; response DTO: ApiResponse<TelemetryResponse>.

Problem: Gateway/device authentication boundary; never called by the human browser

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: GET /api/uavs/{id}

Status: OTHER (NOT_REQUIRED)

Frontend location:
- file/component: DevicesPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/UavsController.cs; action: Get; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<UavResponse>.

Problem: DevicesPage edits the complete UavResponse from the inventory list.

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: GET /api/users/{id}

Status: OTHER (NOT_REQUIRED)

Frontend location:
- file/component: UsersPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/UsersController.cs; action: Get; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<ManagedUserResponse>.

Problem: UsersPage uses the complete ManagedUserResponse returned by the paginated directory. There is no separate user-detail route.

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Issue: GET /api/zones/{id}

Status: OTHER (NOT_REQUIRED)

Frontend location:
- file/component: FarmsPage (see screen coverage and service modules).
- current mock source: none for real mode.

Backend location: controller: src/FarmMonitoring.Api/Controllers/ZonesController.cs; action: Get; request DTO/query: route identifiers / no JSON body; response DTO: ApiResponse<ZoneResponse>.

Problem: FarmsPage uses the same ZoneResponse from the farm zones catalog.

Recommended fix: None for the current workflow; preserve the documented device boundary or use the equivalent connected API.

## Verification and Remaining Work

Backend dotnet tool restore/restore/full build/full PostgreSQL tests PASS: 59 unit +117 integration =176; zero failures/skips. Nine CORS tests included; no backend changes since that run. Frontend npm ci PASS (dependencies unchanged since install); final npm run lint PASS, node --test tests/*.test.js PASS (92 tests), npm run build PASS. The 25 new frontend tests comprise 19 API/auth tests and six adapter tests. Existing 67 tests retained. All scoped auth-review findings addressed.

Browser login error UX verified. Successful login/me/list/mutation/logout/refresh NOT_TESTED because local HTTPS certificate is untrusted; no certificate bypass was attempted. CORS protocol behavior is verified independently in ASP.NET integration tests.

Remaining verification: trust the local development certificate, then execute successful browser login, /me, protected list, mutation, refresh and logout. No supported mock-backed implementation work is intentionally left pending. This browser gap is not reported as a successful end-to-end test.


## Role and Unsupported Workflow Resolution

UAV_DEVICE_OPERATOR is an external/system actor in the backend, not a supported human JWT role. Frontend role normalization maps only FarmAdministrator → ADMINISTRATOR, FarmOwner → FARM_OWNER and FarmEngineer → AGRICULTURAL_ENGINEER. Operator dashboard/role preview remain explicit-demo only. No backend authorization was weakened.

Sensor-level cross-mission collection history has no sensor-filtered endpoint; the sensor detail links to mission detail, where actual collection-attempt history is displayed. Missing farm area/boundaries, equipment capabilities, waypoint completion, alert unit/threshold snapshots and gateway-local queues are not fabricated. CSV exports use actual report aggregates; PDF remains explicitly labeled a prototype. Public real-mode registration is disabled.

## Changed Files

Backend: README.md; src/FarmMonitoring.Api/Program.cs; src/FarmMonitoring.Api/appsettings.Development.json; tests/FarmMonitoring.IntegrationTests/CorsTests.cs; FRONTEND_API_INTEGRATION.md; docs/superpowers/plans/2026-10-11-api-integration.md. No schema or migration changes.

Frontend files differing from the original branch snapshot, normalized for the pre-existing frontend/ relocation (includes any pre-existing content edits; no files were discarded):

- .env.example
- README.md
- src/components/app/ApiState.jsx
- src/components/app/AppIcon.jsx
- src/components/app/AppPageBoundary.jsx
- src/components/app/AppSidebar.jsx
- src/components/app/AppTopbar.jsx
- src/components/app/ChannelHistory.jsx
- src/components/app/DemoRoleSwitcher.jsx
- src/components/app/EnvironmentalChart.jsx
- src/components/app/EquipmentDialog.jsx
- src/components/app/FarmAccess.jsx
- src/components/app/FarmOverviewMap.jsx
- src/components/app/FarmRecordDialog.jsx
- src/components/app/FarmZoneMap.jsx
- src/components/app/ManagementDialog.jsx
- src/components/app/MissionMonitoringMap.jsx
- src/components/app/MissionRouteMap.jsx
- src/components/app/NotificationsPanel.jsx
- src/components/app/ProtectedRoute.jsx
- src/components/app/SensorConfiguration.jsx
- src/components/app/WaypointEditor.jsx
- src/components/app/ZoneComparison.jsx
- src/config/auth.js
- src/config/backendPermissions.js
- src/config/navigation.js
- src/context/ApiManagementProvider.jsx
- src/context/ApiOperationsProvider.jsx
- src/context/AuthProvider.jsx
- src/context/ManagementProvider.jsx
- src/context/OperationsProvider.jsx
- src/layouts/AppShell.jsx
- src/pages/AccountPage.jsx
- src/pages/app/AccessDeniedPage.jsx
- src/pages/app/AdminDashboard.jsx
- src/pages/app/AlertsPage.jsx
- src/pages/app/CreateMissionPage.jsx
- src/pages/app/DashboardPage.jsx
- src/pages/app/DemoAlertsPage.jsx
- src/pages/app/DevicesPage.jsx
- src/pages/app/EngineerDashboard.jsx
- src/pages/app/FarmOwnerDashboard.jsx
- src/pages/app/FarmsPage.jsx
- src/pages/app/MissionDetailPage.jsx
- src/pages/app/MissionsPage.jsx
- src/pages/app/OperatorDashboard.jsx
- src/pages/app/ReportsPage.jsx
- src/pages/app/SensorDataPage.jsx
- src/pages/app/SensorDetailPage.jsx
- src/pages/app/SensorsPage.jsx
- src/pages/app/SettingsPage.jsx
- src/pages/app/SyncPage.jsx
- src/pages/app/UsersPage.jsx
- src/services/api/adapters.js
- src/services/api/analytics.js
- src/services/api/apiClient.js
- src/services/api/dashboard.js
- src/services/api/index.js
- src/services/api/monitoring.js
- src/services/api/planning.js
- src/services/api/reportCsv.js
- src/services/api/reports.js
- src/services/api/resources.js
- src/services/api/roles.js
- src/services/api/selectors.js
- src/services/api/session.js
- src/services/api/useAnalytics.js
- src/services/api/useApiQuery.js
- src/services/api/useDashboard.js
- src/services/api/useFarmWorkspace.js
- src/utils/display.js
- tests/api-adapters.test.js
- tests/api-client.test.js

No commit, push, branch merge, or destructive reset/clean was performed.

## Gateway credential management (2026-10-13)

The four human gateway credential endpoints are connected to the Mobile Gateways Credential dialog. Device sync/telemetry remain device-only. See [Gateway Credential implementation](GATEWAY_CREDENTIAL_IMPLEMENTATION.md) for the API contract, database transition, security evidence, verification and ESP32 setup steps.
