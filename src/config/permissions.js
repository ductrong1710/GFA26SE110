import { ROLES } from './roles.js'

export const PERMISSIONS = Object.freeze({
  CROP_SEASONS_VIEW: 'cropSeasons.view',
  CROP_SEASONS_MANAGE: 'cropSeasons.manage',
  CROP_SUPPORT_VIEW: 'cropSupport.view',
  CROP_SUPPORT_CREATE: 'cropSupport.create',
  CROP_SUPPORT_PROCESS: 'cropSupport.process',
  CROP_SUPPORT_ASSIGN: 'cropSupport.assign',
  MAINTENANCE_MANAGE: 'maintenance.manage',
  DASHBOARD_VIEW: 'dashboard.view',
  CONSULTATIONS_MANAGE: 'consultations.manage',
  CONSULTATIONS_VIEW: 'consultations.view',
  INSTALLATIONS_VIEW: 'installations.view',
  INSTALLATIONS_REVIEW: 'installations.review',
  INSTALLATION_JOBS_VIEW: 'installationJobs.view',
  INSTALLATIONS_WORK: 'installations.work',
  INSTALLATIONS_CONFIRM: 'installations.confirm',
  RENTALS_VIEW: 'rentals.view',
  RENTALS_MANAGE: 'rentals.manage',
  SERVICE_JOBS_VIEW: 'serviceJobs.view',
  SERVICE_JOBS_MANAGE: 'serviceJobs.manage',
  SERVICE_JOBS_WORK: 'serviceJobs.work',
  SERVICE_JOBS_CONFIRM: 'serviceJobs.confirm',
  INCIDENTS_VIEW: 'incidents.view',
  INCIDENTS_CREATE: 'incidents.create',
  FARMS_VIEW: 'farms.view',
  FARMS_MANAGE: 'farms.manage',
  SENSORS_VIEW: 'sensors.view',
  SENSORS_CREATE: 'sensors.create',
  DEVICES_CREATE: 'devices.create',
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
  THRESHOLDS_MANAGE: 'thresholds.manage',
})

const P = PERMISSIONS

export const ROLE_PERMISSIONS = Object.freeze({
  [ROLES.FARM_OWNER]: Object.freeze([
    P.CROP_SEASONS_VIEW, P.CROP_SEASONS_MANAGE,
    P.CROP_SUPPORT_VIEW, P.CROP_SUPPORT_CREATE,
    P.SERVICE_JOBS_VIEW, P.SERVICE_JOBS_CONFIRM, P.INCIDENTS_VIEW, P.INCIDENTS_CREATE,
    P.RENTALS_VIEW,
    P.INSTALLATIONS_CONFIRM,
    P.INSTALLATION_JOBS_VIEW,
    P.INSTALLATIONS_VIEW,
    P.DASHBOARD_VIEW, P.FARMS_VIEW, P.SENSORS_VIEW, P.SENSOR_DATA_VIEW,
    P.MISSIONS_VIEW, P.ALERTS_VIEW, P.REPORTS_VIEW, P.REPORTS_GENERATE,
    P.DEVICES_VIEW, P.ZONES_COMPARE, P.MISSIONS_CREATE, P.MISSIONS_MANAGE, P.MISSIONS_MONITOR,
  ]),
  [ROLES.ADMINISTRATOR]: Object.freeze([
    P.CROP_SEASONS_VIEW,
    P.CROP_SUPPORT_VIEW, P.CROP_SUPPORT_ASSIGN,
    P.MAINTENANCE_MANAGE,
    P.SERVICE_JOBS_VIEW, P.SERVICE_JOBS_MANAGE, P.INCIDENTS_VIEW,
    P.RENTALS_VIEW, P.RENTALS_MANAGE,
    P.INSTALLATION_JOBS_VIEW,
    P.INSTALLATIONS_VIEW, P.INSTALLATIONS_REVIEW,
    P.CONSULTATIONS_VIEW,
    P.DASHBOARD_VIEW, P.FARMS_VIEW, P.FARMS_MANAGE, P.SENSOR_DATA_VIEW,
    P.DEVICES_VIEW, P.MISSIONS_VIEW, P.ALERTS_VIEW, P.ALERTS_MANAGE,
    P.REPORTS_VIEW, P.REPORTS_GENERATE, P.USERS_MANAGE, P.SETTINGS_MANAGE,
    P.SENSORS_VIEW, P.SENSORS_CREATE, P.SENSORS_MANAGE, P.DEVICES_CREATE,
    P.DEVICES_MANAGE, P.ZONES_COMPARE, P.MISSIONS_MONITOR, P.THRESHOLDS_MANAGE,
  ]),
  [ROLES.TECHNICIAN]: Object.freeze([
    P.SERVICE_JOBS_VIEW, P.SERVICE_JOBS_WORK, P.INSTALLATION_JOBS_VIEW, P.INSTALLATIONS_WORK,
    P.DASHBOARD_VIEW,
  ]),
  [ROLES.AGRICULTURAL_ENGINEER]: Object.freeze([
    P.CROP_SUPPORT_VIEW, P.CROP_SUPPORT_PROCESS,
    P.RENTALS_VIEW,
    P.INSTALLATION_JOBS_VIEW,
    P.INSTALLATIONS_VIEW,
    P.DASHBOARD_VIEW, P.CONSULTATIONS_MANAGE, P.CONSULTATIONS_VIEW,
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
