import { PERMISSIONS as P } from './permissions.js'
import { ROLES } from './roles.js'

const read = [P.DASHBOARD_VIEW, P.FARMS_VIEW, P.SENSORS_VIEW, P.SENSOR_DATA_VIEW, P.ZONES_COMPARE, P.DEVICES_VIEW, P.MISSIONS_VIEW, P.MISSIONS_MONITOR, P.ALERTS_VIEW, P.ALERTS_MANAGE, P.REPORTS_VIEW, P.REPORTS_GENERATE]
const manage = [P.SENSORS_CREATE, P.DEVICES_CREATE, P.FARMS_MANAGE, P.SENSORS_MANAGE, P.DEVICES_MANAGE, P.MISSIONS_CREATE, P.MISSIONS_MANAGE]
const permissions = {
  [ROLES.ADMINISTRATOR]: [...read, ...manage, P.USERS_MANAGE, P.SETTINGS_MANAGE],
  [ROLES.FARM_OWNER]: [...read, ...manage],
  [ROLES.AGRICULTURAL_ENGINEER]: read,
  [ROLES.UAV_DEVICE_OPERATOR]: [...read, P.SENSORS_CREATE, P.DEVICES_CREATE],
}
export const hasBackendPermission = (role, permission) => permissions[role]?.includes(permission) ?? false
