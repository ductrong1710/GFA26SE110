import test from 'node:test'
import assert from 'node:assert/strict'
import { createManagementState, applyManagementChange } from '../src/data/mock/managementState.js'
import { createFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { getAdminDashboard } from '../src/data/mock/adminDashboard.js'
import { getFarmOwnerDashboard } from '../src/data/mock/farmOwnerDashboard.js'
import { getAlertDetails, filterAlerts, localDate } from '../src/data/mock/alertSelectors.js'
import { getAlertsForUser } from '../src/data/mock/selectors.js'
import { ROLES } from '../src/config/roles.js'

const admin = { id: 1, activeRole: ROLES.ADMINISTRATOR }
const update = (state, change, actor = admin) => applyManagementChange(state, change, actor)

test('users support multiple human roles, validation and activation without mutating fixtures', () => {
  const original = createManagementState()
  let state = update(original, { kind: 'user', values: { fullName: '  New Owner  ', email: ' NEW@farm.test ', roles: [ROLES.FARM_OWNER, ROLES.AGRICULTURAL_ENGINEER], status: 'ACTIVE' } })
  const user = state.users.at(-1)
  assert.equal(user.fullName, 'New Owner')
  assert.equal(user.email, 'new@farm.test')
  assert.equal(user.roles.length, 2)
  assert.equal(user.lastLoginAt, null)
  assert.equal(original.users.length, 5)
  state = update(state, { kind: 'user', id: user.id, values: { status: 'INACTIVE' } })
  assert.equal(state.users.at(-1).status, 'INACTIVE')
  state = update(state, { kind: 'user', id: user.id, values: { roles: [ROLES.AGRICULTURAL_ENGINEER], status: 'ACTIVE' } })
  assert.equal(state.users.at(-1).activeRole, ROLES.AGRICULTURAL_ENGINEER)
  for (const invalid of [{ email: 'ADMIN@smartfarm.test' }, { email: 'wrong' }, { fullName: '  ' }, { roles: [] }, { roles: ['SENSOR_COLLECTION_ENGINE'] }]) assert.throws(() => update(state, { kind: 'user', id: user.id, values: invalid }))
  assert.equal(getAdminDashboard(createFarmWorkspace(), state).totalUsers, 6)
})

test('alert handling preserves chronological history and follows lifecycle transitions', () => {
  const original = createManagementState()
  let state = update(original, { kind: 'alert', id: 1, action: 'ACKNOWLEDGED' })
  state = update(state, { kind: 'alert', id: 1, action: 'NOTE', note: ' Ventilation checked. ' })
  state = update(state, { kind: 'alert', id: 1, action: 'CLOSED' })
  assert.equal(state.alerts[0].history.at(-2).note, 'Ventilation checked.')
  assert.equal(state.alerts[0].closedByUserId, 1)
  assert.equal(original.alerts[0].status, 'OPEN')
  assert.equal(getAdminDashboard(createFarmWorkspace(), state).openAlerts, 5)
  assert.equal(getFarmOwnerDashboard(1, createFarmWorkspace(), state.alerts).openAlertCount, 5)
  assert.throws(() => update(state, { kind: 'alert', id: 1, action: 'ACKNOWLEDGED' }), /not available/)
  state = update(state, { kind: 'alert', id: 1, action: 'REOPENED' })
  assert.equal(state.alerts[0].status, 'OPEN')
  assert.equal(state.alerts[0].closedAt, null)
  assert.equal(state.alerts[0].history.length, 5)
  assert.equal(state.alerts[0].history[3].action, 'CLOSED')
  assert.throws(() => update(state, { kind: 'alert', id: 1, action: 'NOTE', note: '  ' }), /handling note/)
  const ids = state.alerts.flatMap(({ history }) => history.map(({ id }) => id))
  assert.equal(new Set(ids).size, ids.length)
})

test('settings enforce ranges and preserve captured alert thresholds', () => {
  const state = createManagementState()
  const settings = structuredClone(state.settings)
  settings.thresholds[0].min = 36
  assert.throws(() => update(state, { kind: 'settings', values: settings }), /minimum must be below/)
  settings.thresholds[0].min = 19
  settings.lowBatteryPercent = '25'
  settings.emailNotifications = true
  const next = update(state, { kind: 'settings', values: settings })
  assert.equal(next.settings.lowBatteryPercent, 25)
  assert.equal(next.settings.emailNotifications, true)
  assert.equal(next.alerts[0].thresholdValue, 35)
  assert.equal(state.settings.thresholds[0].min, 18)
  assert.throws(() => update(state, { kind: 'settings', values: { ...settings, dataTimeoutMinutes: 0 } }))
})

test('read-only roles cannot mutate any management data; operator alert scope stays related-only', () => {
  const state = createManagementState()
  for (const role of [ROLES.FARM_OWNER, ROLES.AGRICULTURAL_ENGINEER, ROLES.UAV_DEVICE_OPERATOR]) {
    for (const change of [{ kind: 'user', id: 1, values: { status: 'INACTIVE' } }, { kind: 'alert', id: 1, action: 'CLOSED' }, { kind: 'settings', values: state.settings }]) assert.throws(() => update(state, change, { id: 2, activeRole: role }), /cannot make/)
  }
  assert.ok(!getAlertsForUser(2, ROLES.UAV_DEVICE_OPERATOR, state.alerts).some(({ id }) => id === 5))
})

test('alert filters use Vietnam dates and linked farm/zone/device metadata', () => {
  const records = createManagementState().alerts.map((alert) => getAlertDetails(alert, createFarmWorkspace()))
  assert.equal(localDate('2026-10-05T23:24:00.000Z'), '2026-10-06')
  assert.equal(filterAlerts(records, { date: '2026-10-06' }).length, 7)
  assert.equal(filterAlerts(records, { date: '2026-10-05' }).length, 0)
  assert.equal(filterAlerts(records, { farm: '2' }).length, 0)
  assert.deepEqual(filterAlerts(records, { severity: 'WARNING', type: 'SENSOR_THRESHOLD', status: 'OPEN', farm: '1', zone: '2' }).map(({ id }) => id), [1])
  assert.equal(records[0].deviceName, 'NODE_005')
  assert.equal(records[0].reading.value, records[0].triggeredValue)
})
