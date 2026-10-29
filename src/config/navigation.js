import { hasAnyPermission, PERMISSIONS as P } from './permissions.js'
import { HUMAN_ROLES } from './roles.js'

export const navigation = [
  { path: '/app/dashboard', label: 'Dashboard', icon: 'grid', group: 'Workspace', permissions: [P.DASHBOARD_VIEW], ownerLabel: 'Tổng quan', ownerGroup: 'Không gian làm việc' },
  { path: '/app/farms', label: 'Farms & Zones', icon: 'map', group: 'Workspace', permissions: [P.FARMS_VIEW], ownerLabel: 'Nông trại & Khu vực', ownerGroup: 'Không gian làm việc' },
  { path: '/app/sensors', label: 'Sensor Nodes', icon: 'sensor', group: 'Workspace', permissions: [P.SENSORS_VIEW], ownerLabel: 'Trạm cảm biến', ownerGroup: 'Không gian làm việc' },
  { path: '/app/sensor-data', label: 'Sensor Data', icon: 'database', group: 'Workspace', permissions: [P.SENSOR_DATA_VIEW], ownerLabel: 'Dữ liệu cảm biến', ownerGroup: 'Không gian làm việc' },
  { path: '/app/devices', label: 'UAVs & Gateways', icon: 'drone', group: 'Operations', permissions: [P.DEVICES_VIEW], ownerLabel: 'UAV & Gateway', ownerGroup: 'Vận hành' },
  { path: '/app/missions', label: 'Missions', icon: 'mission', group: 'Operations', permissions: [P.MISSIONS_VIEW], ownerLabel: 'Nhiệm vụ', ownerGroup: 'Vận hành' },
  { path: '/app/alerts', label: 'Alerts', icon: 'alert', group: 'Operations', permissions: [P.ALERTS_VIEW, P.ALERTS_VIEW_RELATED], ownerLabel: 'Cảnh báo', ownerGroup: 'Vận hành' },
  { path: '/app/reports', label: 'Reports', icon: 'report', group: 'Operations', permissions: [P.REPORTS_VIEW], ownerLabel: 'Báo cáo', ownerGroup: 'Vận hành' },
  { path: '/app/users', label: 'Users & Roles', icon: 'users', group: 'Administration', permissions: [P.USERS_MANAGE], ownerLabel: 'Người dùng & Vai trò', ownerGroup: 'Quản trị' },
  { path: '/app/profile', label: 'Profile', icon: 'profile', group: 'Account', permissions: [], ownerLabel: 'Hồ sơ', ownerGroup: 'Tài khoản' },
]

export function getNavigationForRole(role, canAny = permissions => hasAnyPermission(role, permissions)) {
  return navigation.filter(({ permissions }) => permissions.length === 0 ? HUMAN_ROLES.includes(role) : canAny(permissions))
}
