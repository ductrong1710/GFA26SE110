import assert from 'node:assert/strict'
import test from 'node:test'
import { getActiveRole, HUMAN_ROLES, ROLES, USER_ASSIGNABLE_ROLES, isHumanRole } from '../src/config/roles.js'
import { PERMISSIONS as P } from '../src/config/permissions.js'
import { hasBackendPermission as can } from '../src/config/backendPermissions.js'
import { getNavigationForRole } from '../src/config/navigation.js'
import { appRoutes } from '../src/config/appRoutes.js'

test('current supported roles match BE, including Technician and excluding retired operator',()=>{
  assert.deepEqual([...HUMAN_ROLES].sort(),['ADMINISTRATOR','AGRICULTURAL_ENGINEER','FARM_OWNER','TECHNICIAN'])
  assert.equal(isHumanRole('UAV_DEVICE_OPERATOR'),false)
  assert.equal(isHumanRole('SENSOR_COLLECTION_ENGINE'),false)
})
for(const role of HUMAN_ROLES) test(role+': navigation and direct routes use current BE permissions',()=>{
  const nav=getNavigationForRole(role,p=>p.some(permission=>can(role,permission)))
  for(const item of nav) assert.ok(!item.permissions.length||item.permissions.some(p=>can(role,p)))
  for(const route of appRoutes) if(route.path.includes('dispatch')) assert.equal(route.permissions.some(p=>can(role,p)),[ROLES.ADMINISTRATOR,ROLES.TECHNICIAN].includes(role))
})
test('Technician and Engineer have contextual workspaces without generic farm/sensor access',()=>{
  for(const role of [ROLES.TECHNICIAN,ROLES.AGRICULTURAL_ENGINEER]) for(const p of [P.FARMS_VIEW,P.SENSORS_VIEW,P.SENSOR_DATA_VIEW,P.DEVICES_MANAGE,P.USERS_MANAGE]) assert.equal(can(role,p),false)
  assert.equal(can(ROLES.TECHNICIAN,P.WORK_ORDERS),true)
  assert.equal(can(ROLES.AGRICULTURAL_ENGINEER,P.CONSULTATIONS),true)
})
test('owner operates UAV; admin manages catalog and approvals',()=>{
  assert.equal(can(ROLES.FARM_OWNER,P.MISSIONS_CREATE),true)
  assert.equal(can(ROLES.FARM_OWNER,P.PUBLIC_CONTENT),false)
  assert.equal(can(ROLES.ADMINISTRATOR,P.PUBLIC_CONTENT),true)
  assert.equal(can(ROLES.ADMINISTRATOR,P.MISSIONS_CREATE),false)
})
test('user management assigns Technician but not administrator or retired operator',()=>{
  assert.deepEqual(USER_ASSIGNABLE_ROLES,[ROLES.FARM_OWNER,ROLES.TECHNICIAN,ROLES.AGRICULTURAL_ENGINEER])
})
test('role switching cannot activate an unassigned or retired role',()=>{
  const user={roles:[ROLES.ADMINISTRATOR,ROLES.TECHNICIAN],activeRole:ROLES.TECHNICIAN}
  assert.equal(getActiveRole(user),ROLES.TECHNICIAN)
  assert.equal(getActiveRole({...user,activeRole:ROLES.FARM_OWNER}),null)
  assert.equal(getActiveRole({...user,activeRole:ROLES.UAV_DEVICE_OPERATOR}),null)
})
test('unknown and retired roles have no production permissions',()=>{
  for(const role of ['UNKNOWN',null,ROLES.UAV_DEVICE_OPERATOR]) for(const permission of Object.values(P)) assert.equal(can(role,permission),false)
})
