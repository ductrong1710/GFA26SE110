import { PERMISSIONS as P } from './permissions.js'

export const appRoutes = [
  { path: 'dashboard', title: 'Tổng quan', permissions: [P.DASHBOARD_VIEW] },
  { path: 'farms', title: 'Nông trại', permissions: [P.FARMS_VIEW] },
  { path: 'sensors', title: 'Cảm biến', permissions: [P.SENSORS_VIEW] },
  { path: 'sensors/:id', title: 'Chi tiết cảm biến', permissions: [P.SENSORS_VIEW] },
  { path: 'sensor-data', title: 'Dữ liệu cảm biến', permissions: [P.SENSOR_DATA_VIEW] },
  { path: 'sensor-data/compare-zones', title: 'So sánh khu vực', permissions: [P.ZONES_COMPARE] },
  { path: 'devices', title: 'Thiết bị', permissions: [P.DEVICES_VIEW] },
  { path: 'missions', title: 'Nhiệm vụ', permissions: [P.MISSIONS_VIEW] },
  { path: 'missions/create', title: 'Tạo nhiệm vụ', permissions: [P.MISSIONS_CREATE] },
  { path: 'missions/:id', title: 'Chi tiết nhiệm vụ', permissions: [P.MISSIONS_VIEW] },
  { path: 'sync', title: 'Đồng bộ', permissions: [P.SYNC_MANAGE] },
  { path: 'alerts', title: 'Cảnh báo', permissions: [P.ALERTS_VIEW, P.ALERTS_VIEW_RELATED] },
  { path: 'reports', title: 'Báo cáo', permissions: [P.REPORTS_VIEW] },
  { path: 'users', title: 'Người dùng', permissions: [P.USERS_MANAGE] },
  { path: 'settings', title: 'Cài đặt', permissions: [P.SETTINGS_MANAGE] },
]
