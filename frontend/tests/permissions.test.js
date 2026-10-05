import assert from 'node:assert/strict'
import test from 'node:test'
import { getActiveRole, HUMAN_ROLES, ROLES, isHumanRole } from '../src/config/roles.js'
import { getAlertScope, hasAnyPermission, hasPermission, PERMISSIONS as P } from '../src/config/permissions.js'
import { getNavigationForRole, navigation } from '../src/config/navigation.js'
import { appRoutes } from '../src/config/appRoutes.js'
import { mockAccounts } from '../src/data/mock/accounts.js'

const expectedModules = {
  FARM_OWNER: ['dashboard', 'farms', 'sensors', 'sensor-data', 'missions', 'alerts', 'reports'],
  ADMINISTRATOR: ['dashboard', 'farms', 'sensor-data', 'devices', 'missions', 'alerts', 'reports', 'users', 'settings'],
  UAV_DEVICE_OPERATOR: ['dashboard', 'sensors', 'devices', 'missions', 'sync', 'alerts'],
  AGRICULTURAL_ENGINEER: ['dashboard', 'farms', 'sensors', 'sensor-data', 'alerts', 'reports'],
}

for (const [role, modules] of Object.entries(expectedModules)) {
  test(`${role}: exact modules in sidebar and direct routes`, () => {
    assert.deepEqual(getNavigationForRole(role).map(({ path }) => path.slice('/app/'.length)), modules)
    for (const route of appRoutes) {
      const expected = route.path === 'missions/create' ? role === ROLES.UAV_DEVICE_OPERATOR
        : route.path === 'sensor-data/compare-zones' ? role === ROLES.AGRICULTURAL_ENGINEER
          : modules.includes(route.path.split('/')[0])
      assert.equal(hasAnyPermission(role, route.permissions), expected, `${role}: ${route.path}`)
    }
  })
}

test('read-only roles cannot acquire management permissions', () => {
  const writes = [P.FARMS_MANAGE, P.SENSORS_MANAGE, P.DEVICES_MANAGE, P.MISSIONS_CREATE,
    P.MISSIONS_MANAGE, P.MISSIONS_MONITOR, P.SYNC_MANAGE, P.ALERTS_MANAGE, P.USERS_MANAGE, P.SETTINGS_MANAGE]
  for (const role of [ROLES.FARM_OWNER, ROLES.AGRICULTURAL_ENGINEER]) {
    for (const permission of writes) assert.equal(hasPermission(role, permission), false, `${role}: ${permission}`)
  }
  assert.equal(hasPermission(ROLES.AGRICULTURAL_ENGINEER, P.REPORTS_GENERATE), false)
  assert.equal(hasPermission(ROLES.FARM_OWNER, P.REPORTS_GENERATE), true)
})

test('administration and operational management remain separate', () => {
  for (const permission of [P.FARMS_MANAGE, P.ALERTS_MANAGE, P.USERS_MANAGE, P.SETTINGS_MANAGE]) {
    assert.equal(hasPermission(ROLES.ADMINISTRATOR, permission), true)
    assert.equal(hasPermission(ROLES.UAV_DEVICE_OPERATOR, permission), false)
  }
  for (const permission of [P.SENSORS_MANAGE, P.DEVICES_MANAGE, P.MISSIONS_CREATE, P.MISSIONS_MANAGE, P.MISSIONS_MONITOR, P.SYNC_MANAGE]) {
    assert.equal(hasPermission(ROLES.UAV_DEVICE_OPERATOR, permission), true)
    assert.equal(hasPermission(ROLES.ADMINISTRATOR, permission), false)
  }
})

test('operator alerts are related-only; engineer reports and alerts are read-only', () => {
  assert.equal(getAlertScope(ROLES.UAV_DEVICE_OPERATOR), 'related')
  assert.equal(hasPermission(ROLES.UAV_DEVICE_OPERATOR, P.ALERTS_VIEW), false)
  for (const role of [ROLES.FARM_OWNER, ROLES.ADMINISTRATOR, ROLES.AGRICULTURAL_ENGINEER]) {
    assert.equal(getAlertScope(role), 'all')
  }
  assert.equal(hasPermission(ROLES.AGRICULTURAL_ENGINEER, P.ALERTS_MANAGE), false)
})

test('multiple assigned roles do not merge permissions', () => {
  const user = { roles: [ROLES.ADMINISTRATOR, ROLES.UAV_DEVICE_OPERATOR], activeRole: ROLES.UAV_DEVICE_OPERATOR }
  assert.equal(hasPermission(getActiveRole(user), P.USERS_MANAGE), false)
  assert.equal(hasPermission(getActiveRole(user), P.MISSIONS_CREATE), true)
  assert.equal(hasPermission(getActiveRole({ ...user, activeRole: ROLES.ADMINISTRATOR }), P.USERS_MANAGE), true)
  assert.equal(getActiveRole({ ...user, activeRole: ROLES.FARM_OWNER }), null)
})

test('unassigned role preview needs both an explicit demo option and a mock user', () => {
  const user = { roles: [ROLES.ADMINISTRATOR], activeRole: ROLES.FARM_OWNER, isMock: true }
  assert.equal(getActiveRole(user), null)
  assert.equal(getActiveRole(user, { allowDemoRole: true }), ROLES.FARM_OWNER)
  assert.equal(getActiveRole({ ...user, isMock: false }, { allowDemoRole: true }), null)
  assert.deepEqual(user.roles, [ROLES.ADMINISTRATOR])
})

test('unknown actors and Sensor Collection Engine have no dashboard or permissions', () => {
  for (const role of ['SENSOR_COLLECTION_ENGINE', 'Sensor Collection Engine', 'UNKNOWN', 'toString', '__proto__', null, undefined]) {
    assert.equal(isHumanRole(role), false)
    assert.equal(getActiveRole({ roles: [role], activeRole: role, isMock: true }, { allowDemoRole: true }), null)
    assert.deepEqual(getNavigationForRole(role), [])
    assert.equal(getAlertScope(role), null)
    for (const permission of Object.values(P)) assert.equal(hasPermission(role, permission), false)
  }
  assert.equal(hasPermission(ROLES.ADMINISTRATOR, 'unknown.permission'), false)
})

test('every menu entry matches its protected route and no route has empty permissions', () => {
  for (const item of navigation) {
    const route = appRoutes.find(({ path }) => `/app/${path}` === item.path)
    assert.ok(route, item.path)
    assert.deepEqual(route.permissions, item.permissions)
  }
  for (const { permissions, path } of appRoutes) {
    assert.ok(permissions.length > 0, path)
    for (const permission of permissions) assert.ok(Object.values(P).includes(permission), path)
  }
})

test('fixtures cover exactly the human UI roles and have valid active-role assignments', () => {
  assert.deepEqual(mockAccounts.map(({ activeRole }) => activeRole).sort(), [...HUMAN_ROLES].sort())
  for (const user of mockAccounts) assert.equal(getActiveRole(user), user.activeRole)
})
