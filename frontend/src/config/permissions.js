import { ROLES } from './roles.js'

export const PERMISSIONS = Object.freeze({
  DASHBOARD_VIEW: 'dashboard.view',
  FARMS_VIEW: 'farms.view',
  FARMS_MANAGE: 'farms.manage',
  SENSORS_VIEW: 'sensors.view',
  SENSORS_MANAGE: 'sensors.manage',
  SENSOR_DATA_VIEW: 'sensorData.view',
  ZONES_COMPARE: 'sensorData.compareZones',
  DEVICES_VIEW: 'devices.view',
  DEVICES_MANAGE: 'devices.manage',
  MISSIONS_VIEW: 'missions.view',
  MISSIONS_CREATE: 'missions.create',
  MISSIONS_MANAGE: 'missions.manage',
  MISSIONS_MONITOR: 'missions.monitor',
  SYNC_MANAGE: 'sync.manage',
  ALERTS_VIEW: 'alerts.view',
  ALERTS_VIEW_RELATED: 'alerts.viewRelated',
  ALERTS_MANAGE: 'alerts.manage',
  REPORTS_VIEW: 'reports.view',
  REPORTS_GENERATE: 'reports.generate',
  USERS_MANAGE: 'users.manage',
  SETTINGS_MANAGE: 'settings.manage',
})

const P = PERMISSIONS

export const ROLE_PERMISSIONS = Object.freeze({
  [ROLES.FARM_OWNER]: Object.freeze([
    P.DASHBOARD_VIEW, P.FARMS_VIEW, P.SENSORS_VIEW, P.SENSOR_DATA_VIEW,
    P.MISSIONS_VIEW, P.ALERTS_VIEW, P.REPORTS_VIEW, P.REPORTS_GENERATE,
  ]),
  [ROLES.ADMINISTRATOR]: Object.freeze([
    P.DASHBOARD_VIEW, P.FARMS_VIEW, P.FARMS_MANAGE, P.SENSOR_DATA_VIEW,
    P.DEVICES_VIEW, P.MISSIONS_VIEW, P.ALERTS_VIEW, P.ALERTS_MANAGE,
    P.REPORTS_VIEW, P.REPORTS_GENERATE, P.USERS_MANAGE, P.SETTINGS_MANAGE,
  ]),
  [ROLES.UAV_DEVICE_OPERATOR]: Object.freeze([
    P.DASHBOARD_VIEW, P.SENSORS_VIEW, P.SENSORS_MANAGE, P.DEVICES_VIEW,
    P.DEVICES_MANAGE, P.MISSIONS_VIEW, P.MISSIONS_CREATE, P.MISSIONS_MANAGE,
    P.MISSIONS_MONITOR, P.SYNC_MANAGE, P.ALERTS_VIEW_RELATED,
  ]),
  [ROLES.AGRICULTURAL_ENGINEER]: Object.freeze([
    P.DASHBOARD_VIEW, P.FARMS_VIEW, P.SENSORS_VIEW, P.SENSOR_DATA_VIEW,
    P.ZONES_COMPARE, P.ALERTS_VIEW, P.REPORTS_VIEW,
  ]),
})

export function hasPermission(role, permission) {
  return Object.hasOwn(ROLE_PERMISSIONS, role) && ROLE_PERMISSIONS[role].includes(permission)
}

export function hasAnyPermission(role, permissions) {
  return permissions.some((permission) => hasPermission(role, permission))
}

// Future alert queries must respect this scope, not just the ability to open Alerts.
export function getAlertScope(role) {
  if (hasPermission(role, P.ALERTS_VIEW)) return 'all'
  if (hasPermission(role, P.ALERTS_VIEW_RELATED)) return 'related'
  return null
}
