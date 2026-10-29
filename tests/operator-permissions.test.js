import test from 'node:test'
import assert from 'node:assert/strict'
import { hasBackendPermission } from '../src/config/backendPermissions.js'
import { PERMISSIONS as P } from '../src/config/permissions.js'
import { ROLES } from '../src/config/roles.js'
import { normalizeUser, toBackendRoles } from '../src/services/api/roles.js'
test('operator can register equipment without acquiring administrative or flight controls', () => {
  for (const permission of [P.SENSORS_VIEW, P.SENSORS_CREATE, P.DEVICES_VIEW, P.DEVICES_CREATE])
    assert.equal(hasBackendPermission(ROLES.UAV_DEVICE_OPERATOR, permission), true)
  for (const permission of [P.USERS_MANAGE, P.FARMS_MANAGE, P.SENSORS_MANAGE, P.DEVICES_MANAGE, P.MISSIONS_CREATE, P.MISSIONS_MANAGE])
    assert.equal(hasBackendPermission(ROLES.UAV_DEVICE_OPERATOR, permission), false)
  assert.deepEqual(toBackendRoles([ROLES.UAV_DEVICE_OPERATOR]), ['UavDeviceOperator'])
  assert.equal(normalizeUser({roles:['UavDeviceOperator']}).activeRole, ROLES.UAV_DEVICE_OPERATOR)
})
