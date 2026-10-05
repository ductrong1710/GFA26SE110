import { hasAnyPermission, PERMISSIONS as P } from './permissions.js'

export const navigation = [
  { path: '/app/dashboard', label: 'Dashboard', icon: 'grid', group: 'Workspace', permissions: [P.DASHBOARD_VIEW] },
  { path: '/app/farms', label: 'Farms & Zones', icon: 'map', group: 'Workspace', permissions: [P.FARMS_VIEW] },
  { path: '/app/sensors', label: 'Sensor Nodes', icon: 'sensor', group: 'Workspace', permissions: [P.SENSORS_VIEW] },
  { path: '/app/sensor-data', label: 'Sensor Data', icon: 'database', group: 'Workspace', permissions: [P.SENSOR_DATA_VIEW] },
  { path: '/app/devices', label: 'UAVs & Gateways', icon: 'drone', group: 'Operations', permissions: [P.DEVICES_VIEW] },
  { path: '/app/missions', label: 'Missions', icon: 'mission', group: 'Operations', permissions: [P.MISSIONS_VIEW] },
  { path: '/app/sync', label: 'Data Sync', icon: 'cloud', group: 'Operations', permissions: [P.SYNC_MANAGE] },
  { path: '/app/alerts', label: 'Alerts', icon: 'alert', group: 'Operations', permissions: [P.ALERTS_VIEW, P.ALERTS_VIEW_RELATED] },
  { path: '/app/reports', label: 'Reports', icon: 'report', group: 'Operations', permissions: [P.REPORTS_VIEW] },
  { path: '/app/users', label: 'Users & Roles', icon: 'users', group: 'Administration', permissions: [P.USERS_MANAGE] },
  { path: '/app/settings', label: 'Settings', icon: 'settings', group: 'Administration', permissions: [P.SETTINGS_MANAGE] },
]

export function getNavigationForRole(role) {
  return navigation.filter(({ permissions }) => hasAnyPermission(role, permissions))
}
