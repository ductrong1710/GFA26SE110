import { PERMISSIONS as P } from './permissions.js'
import { hasBackendPermission } from './backendPermissions.js'
import { HUMAN_ROLES } from './roles.js'

export const navigation = [
  { path: '/app/crop-seasons', label: 'Mùa vụ & nhật ký', ownerLabel: 'Mùa vụ & nhật ký', icon: 'sensor', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.FARMS_VIEW] },
  { path: '/app/consultations', label: 'Khách cần tư vấn', ownerLabel: 'Khách cần tư vấn', icon: 'users', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.CONSULTATIONS] },
  { path: '/app/work-orders', label: 'Lắp đặt & dịch vụ', ownerLabel: 'Lắp đặt & dịch vụ', icon: 'mission', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.WORK_ORDERS] },
  { path: '/app/dispatch', label: 'Lịch công việc', ownerLabel: 'Lịch công việc', icon: 'grid', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.DISPATCH] },
  { path: '/app/rentals', label: 'Hồ sơ thuê', ownerLabel: 'Hồ sơ thuê', icon: 'report', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.RENTALS] },
  { path: '/app/support', label: 'Báo cáo hỗ trợ', ownerLabel: 'Báo cáo hỗ trợ', icon: 'alert', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.SUPPORT] },
  { path: '/app/maintenance', label: 'Bảo trì định kỳ', ownerLabel: 'Bảo trì định kỳ', icon: 'sensor', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.MAINTENANCE] },
  { path: '/app/device-history', label: 'Lịch sử thiết bị', ownerLabel: 'Lịch sử thiết bị', icon: 'database', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.DEVICE_HISTORY] },
  { path: '/app/public-content', label: 'Nội dung Homepage', ownerLabel: 'Nội dung Homepage', icon: 'grid', group: 'Dịch vụ nông trại', ownerGroup: 'Dịch vụ nông trại', permissions: [P.PUBLIC_CONTENT] },
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

export function getNavigationForRole(role, canAny = permissions => permissions.some(permission => hasBackendPermission(role, permission))) {
  return navigation.filter(({ permissions }) => permissions.length === 0 ? HUMAN_ROLES.includes(role) : canAny(permissions))
}
