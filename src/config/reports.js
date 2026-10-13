import { ROLES } from './roles.js'
import { hasPermission, PERMISSIONS } from './permissions.js'

export const REPORT_TYPES = [
  { id: 'sensor', name: 'Sensor Data Report' }, { id: 'mission', name: 'Mission Report' },
  { id: 'device', name: 'Device Report' }, { id: 'alert', name: 'Alert Report' },
]
const roleReports = {
  [ROLES.FARM_OWNER]: ['sensor', 'mission', 'device', 'alert'],
  [ROLES.ADMINISTRATOR]: ['sensor', 'mission', 'device', 'alert'],
  [ROLES.UAV_DEVICE_OPERATOR]: ['mission', 'device'],
  [ROLES.AGRICULTURAL_ENGINEER]: ['sensor', 'alert'],
}
export const getReportTypes = (role) => hasPermission(role, PERMISSIONS.REPORTS_VIEW) ? REPORT_TYPES.filter(({ id }) => roleReports[role]?.includes(id)) : []
export const canExportReport = (role, type) => hasPermission(role, PERMISSIONS.REPORTS_GENERATE) && getReportTypes(role).some(({ id }) => id === type)
