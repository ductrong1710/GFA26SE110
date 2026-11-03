import test from 'node:test'
import assert from 'node:assert/strict'
import { hasBackendPermission } from '../src/config/backendPermissions.js'
import { PERMISSIONS as P } from '../src/config/permissions.js'
import { ROLES } from '../src/config/roles.js'
import { normalizeUser, toBackendRoles } from '../src/services/api/roles.js'
test('Technician has job context without general farm access; retired operator grants nothing',()=>{
  for(const p of [P.DASHBOARD_VIEW,P.WORK_ORDERS,P.DISPATCH]) assert.equal(hasBackendPermission(ROLES.TECHNICIAN,p),true)
  for(const p of [P.FARMS_VIEW,P.USERS_MANAGE,P.SENSORS_VIEW,P.MISSIONS_CREATE]) assert.equal(hasBackendPermission(ROLES.TECHNICIAN,p),false)
  assert.deepEqual(toBackendRoles([ROLES.TECHNICIAN]),['Technician'])
  assert.equal(normalizeUser({roles:['Technician']}).activeRole,ROLES.TECHNICIAN)
  assert.equal(normalizeUser({roles:['UavDeviceOperator']}).activeRole,null)
})
