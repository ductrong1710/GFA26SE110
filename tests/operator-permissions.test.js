import test from 'node:test'
import assert from 'node:assert/strict'
import { hasBackendPermission } from '../src/config/backendPermissions.js'
import { PERMISSIONS as P } from '../src/config/permissions.js'
import { ROLES } from '../src/config/roles.js'
import { normalizeUser, toBackendRoles } from '../src/services/api/roles.js'

test('technician identity does not inherit retired operator or global administrative powers', () => {
  for (const permission of [P.USERS_MANAGE, P.FARMS_MANAGE, P.SENSORS_CREATE, P.DEVICES_CREATE, P.SENSORS_MANAGE, P.DEVICES_MANAGE, P.MISSIONS_CREATE, P.MISSIONS_MANAGE])
    assert.equal(hasBackendPermission(ROLES.TECHNICIAN, permission), false)
  assert.deepEqual(toBackendRoles([ROLES.TECHNICIAN]), ['Technician'])
  assert.equal(normalizeUser({ roles: ['Technician'] }).activeRole, ROLES.TECHNICIAN)
  assert.equal(normalizeUser({ roles: ['UavDeviceOperator'] }).activeRole, null)
})
