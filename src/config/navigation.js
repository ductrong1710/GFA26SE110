import { hasAnyPermission, PERMISSIONS as P } from './permissions.js'

export const navigation = [
  { path: '/app/dashboard', label: 'Dashboard', icon: 'grid', group: 'Workspace', permissions: [P.DASHBOARD_VIEW] },
  { path: '/app/consultations', label: 'Consultations', icon: 'users', group: 'Workspace', permissions: [P.CONSULTATIONS_VIEW] },
  { path: '/app/installation-requests', label: 'Installation requests', icon: 'mission', group: 'Operations', permissions: [P.INSTALLATIONS_VIEW] },
  { path: '/app/installation-jobs', label: 'Installation jobs', icon: 'mission', group: 'Operations', permissions: [P.INSTALLATION_JOBS_VIEW] },
  { path: '/app/rental-contracts', label: 'Rental contracts', icon: 'report', group: 'Operations', permissions: [P.RENTALS_VIEW] },
  { path: '/app/service-jobs', label: 'Service jobs', icon: 'devices', group: 'Operations', permissions: [P.SERVICE_JOBS_VIEW] },
  { path: '/app/maintenance-schedules', label: 'Maintenance schedules', icon: 'devices', group: 'Operations', permissions: [P.MAINTENANCE_MANAGE] },
  { path: '/app/device-incidents', label: 'Device incidents', icon: 'alert', group: 'Operations', permissions: [P.INCIDENTS_VIEW] },
  { path: '/app/crop-seasons', label: 'Crop seasons', icon: 'report', group: 'Operations', permissions: [P.CROP_SEASONS_VIEW] },
  { path: '/app/crop-support', label: 'Crop support', icon: 'report', group: 'Operations', permissions: [P.CROP_SUPPORT_VIEW] },
  { path: '/app/farms', label: 'Farms & Zones', icon: 'map', group: 'Workspace', permissions: [P.FARMS_VIEW] },
  { path: '/app/sensors', label: 'Sensor Nodes', icon: 'sensor', group: 'Workspace', permissions: [P.SENSORS_VIEW] },
  { path: '/app/sensor-data', label: 'Sensor Data', icon: 'database', group: 'Workspace', permissions: [P.SENSOR_DATA_VIEW] },
  { path: '/app/devices', label: 'UAVs & Gateways', icon: 'drone', group: 'Operations', permissions: [P.DEVICES_VIEW] },
  { path: '/app/missions', label: 'Missions', icon: 'mission', group: 'Operations', permissions: [P.MISSIONS_VIEW] },
  { path: '/app/alerts', label: 'Alerts', icon: 'alert', group: 'Operations', permissions: [P.ALERTS_VIEW, P.ALERTS_VIEW_RELATED] },
  { path: '/app/reports', label: 'Reports', icon: 'report', group: 'Operations', permissions: [P.REPORTS_VIEW] },
  { path: '/app/users', label: 'Users & Roles', icon: 'users', group: 'Administration', permissions: [P.USERS_MANAGE] },
]

export function getNavigationForRole(role, canAny = permissions => hasAnyPermission(role, permissions)) {
  return navigation.filter(({ permissions }) => canAny(permissions))
}
