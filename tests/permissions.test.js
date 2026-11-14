import assert from 'node:assert/strict'
import test from 'node:test'
import { getActiveRole, HUMAN_ROLES, ROLES, USER_ASSIGNABLE_ROLES, isHumanRole } from '../src/config/roles.js'
import { getAlertScope, hasAnyPermission, hasPermission, PERMISSIONS as P } from '../src/config/permissions.js'
import { hasBackendPermission } from '../src/config/backendPermissions.js'
import { getNavigationForRole, navigation } from '../src/config/navigation.js'
import { appRoutes } from '../src/config/appRoutes.js'

const expectedModules = {
  FARM_OWNER: ['dashboard', 'installation-requests', 'installation-jobs', 'rental-contracts', 'service-jobs', 'device-incidents', 'crop-seasons', 'crop-support', 'farms', 'sensors', 'sensor-data', 'devices', 'missions', 'alerts', 'reports'],
  ADMINISTRATOR: ['dashboard', 'consultations', 'installation-requests', 'installation-jobs', 'rental-contracts', 'service-jobs', 'maintenance-schedules', 'device-incidents', 'crop-seasons', 'crop-support', 'farms', 'sensors', 'sensor-data', 'devices', 'missions', 'alerts', 'reports', 'users', 'settings'],
  TECHNICIAN: ['dashboard', 'installation-jobs', 'service-jobs'],
  AGRICULTURAL_ENGINEER: ['dashboard', 'consultations', 'installation-requests', 'installation-jobs', 'rental-contracts', 'crop-support'],
}
for (const [role, modules] of Object.entries(expectedModules)) {
  test(`${role}: navigation and route access match the operational role`, () => {
    assert.deepEqual(getNavigationForRole(role).map(({ path }) => path.slice('/app/'.length)), modules)
    for (const route of appRoutes) {
      const expected = route.path === 'missions/create' ? role === ROLES.FARM_OWNER
        : route.path === 'sensor-data/compare-zones' ? [ROLES.FARM_OWNER, ROLES.ADMINISTRATOR].includes(role)
          : modules.includes(route.path.split('/')[0]) || (role === ROLES.ADMINISTRATOR && route.path === 'settings')
      assert.equal(hasAnyPermission(role, route.permissions), expected, `${role}: ${route.path}`)
    }
  })
}
test('one permission source drives production and navigation', () => {
  for (const role of HUMAN_ROLES) for (const permission of Object.values(P))
    assert.equal(hasBackendPermission(role, permission), hasPermission(role, permission))
})
test('only administrators manage farms, devices, thresholds and alert resolution', () => {
  for (const permission of [P.MAINTENANCE_MANAGE, P.SERVICE_JOBS_MANAGE, P.RENTALS_MANAGE, P.INSTALLATIONS_REVIEW, P.FARMS_MANAGE, P.SENSORS_CREATE, P.SENSORS_MANAGE, P.DEVICES_CREATE, P.DEVICES_MANAGE, P.THRESHOLDS_MANAGE, P.ALERTS_MANAGE, P.USERS_MANAGE])
    for (const role of HUMAN_ROLES) assert.equal(hasPermission(role, permission), role === ROLES.ADMINISTRATOR)
})
test('owner operates routine missions; technician and engineer need assigned context', () => {
  for (const permission of [P.CROP_SUPPORT_CREATE, P.SERVICE_JOBS_CONFIRM, P.INCIDENTS_CREATE, P.MISSIONS_CREATE, P.MISSIONS_MANAGE])
    for (const role of HUMAN_ROLES) assert.equal(hasPermission(role, permission), role === ROLES.FARM_OWNER)
  for (const role of [ROLES.TECHNICIAN, ROLES.AGRICULTURAL_ENGINEER])
    for (const permission of [P.FARMS_VIEW, P.DEVICES_VIEW, P.MISSIONS_VIEW, P.ALERTS_VIEW]) assert.equal(hasPermission(role, permission), false)
  for (const role of HUMAN_ROLES) assert.equal(hasPermission(role, P.SYNC_MANAGE), false)
})
test('role assignment and active-role switching never grant unassigned privileges', () => {
  assert.deepEqual(USER_ASSIGNABLE_ROLES, [ROLES.FARM_OWNER, ROLES.TECHNICIAN, ROLES.AGRICULTURAL_ENGINEER])
  const user = { roles: [ROLES.ADMINISTRATOR, ROLES.FARM_OWNER], activeRole: ROLES.FARM_OWNER }
  assert.equal(hasPermission(getActiveRole(user), P.USERS_MANAGE), false)
  assert.equal(hasPermission(getActiveRole(user), P.MISSIONS_CREATE), true)
  assert.equal(getActiveRole({ ...user, activeRole: ROLES.TECHNICIAN }), null)
  assert.equal(hasPermission(getActiveRole({ ...user, activeRole: ROLES.ADMINISTRATOR }), P.MISSIONS_CREATE), false)
})
test('unsupported and retired actors cannot access routes', () => {
  for (const role of ['UAV_DEVICE_OPERATOR', 'SENSOR_COLLECTION_ENGINE', 'UNKNOWN', 'toString', '__proto__', null, undefined]) {
    assert.equal(isHumanRole(role), false)
    assert.equal(hasPermission(role, P.DASHBOARD_VIEW), false)
    assert.deepEqual(getNavigationForRole(role), [])
  }
  assert.equal(getAlertScope(ROLES.TECHNICIAN), null)
  assert.equal(getAlertScope(ROLES.AGRICULTURAL_ENGINEER), null)
})
test('every protected navigation and route entry declares known permissions', () => {
  for (const { path, permissions } of [...navigation, ...appRoutes]) {
    assert.ok(permissions.length > 0, path)
    for (const permission of permissions) assert.ok(Object.values(P).includes(permission), path)
  }
})
