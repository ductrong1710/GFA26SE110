import test from 'node:test'
import assert from 'node:assert/strict'
import { createFarmWorkspace, updateFarmWorkspace, getDeletionBlock } from '../src/data/mock/farmWorkspace.js'
import { getAdminDashboard } from '../src/data/mock/adminDashboard.js'
import { getFarmOwnerDashboard } from '../src/data/mock/farmOwnerDashboard.js'

const values = { name: 'Test Farm', description: 'New growing site', location: 'Can Tho', latitude: 10.1, longitude: 105.7, areaHectares: 4 }
const change = (state, operation, allowed = true) => updateFarmWorkspace(state, { ...operation, userId: 1 }, allowed)

test('local CRUD preserves immutable fixtures and monotonically increasing identities', () => {
  const original = createFarmWorkspace()
  let state = change(original, { kind: 'farm', values })
  const farm = state.farms.at(-1)
  assert.equal(original.farms.length, 2)
  state = change(state, { kind: 'zone', values: { ...values, name: 'Test Zone', farmId: farm.id, boundarySummary: 'Northern field' } })
  const zoneId = state.zones.at(-1).id
  assert.equal(getFarmOwnerDashboard(farm.id, state).zones.length, 1)
  assert.equal(getFarmOwnerDashboard(farm.id, state).nodes.length, 0)
  assert.match(getDeletionBlock(state, 'farm', farm.id), /contains zones/)
  state = change(state, { kind: 'farm', id: farm.id, values: { ...values, name: 'Renamed Farm' } })
  assert.equal(state.farms.at(-1).name, 'Renamed Farm')
  assert.equal(state.farms.at(-1).id, farm.id)
  state = change(state, { kind: 'zone', id: zoneId, remove: true })
  state = change(state, { kind: 'farm', id: farm.id, remove: true })
  state = change(state, { kind: 'farm', values })
  assert.ok(state.farms.at(-1).id > farm.id)
  assert.equal(state.activity.length, 6)
  assert.equal(createFarmWorkspace().farms.length, 2)
})

test('mutations reject read-only roles, invalid forms, and orphaning records', () => {
  const state = createFarmWorkspace()
  assert.throws(() => change(state, { kind: 'farm', values }, false), /permission/)
  assert.throws(() => change(state, { kind: 'zone', id: 1, remove: true }), /linked sensors/)
  assert.throws(() => change(state, { kind: 'farm', id: 1, remove: true }), /contains zones/)
  for (const invalid of [{ name: '  ' }, { latitude: 91 }, { longitude: '' }, { areaHectares: -1 }, { name: ' green valley farm ' }]) {
    assert.throws(() => change(state, { kind: 'farm', values: { ...values, ...invalid } }))
  }
  assert.throws(() => change(state, { kind: 'zone', values: { ...values, farmId: 999 } }), /existing farm/)
  assert.throws(() => change(state, { kind: 'farm', id: 999, values }), /no longer exists/)
})

test('administrator indicators and activity derive from linked records and local edits', () => {
  let state = createFarmWorkspace()
  const dashboard = getAdminDashboard(state)
  assert.equal(dashboard.totalUsers, 5)
  assert.equal(dashboard.totalSensors, 15)
  assert.equal(dashboard.offlineDevices, 1)
  assert.equal(dashboard.openAlerts, 6)
  assert.equal(dashboard.activeMissions, 1)
  assert.equal(dashboard.pendingSync, 2)
  assert.ok(dashboard.criticalAlerts.every(({ severity, status }) => severity === 'CRITICAL' && status !== 'CLOSED'))
  state = change(state, { kind: 'farm', values })
  assert.equal(getAdminDashboard(state).farms.length, 3)
  assert.equal(getAdminDashboard(state).activity[0].title, 'Added farm')
  assert.equal(getAdminDashboard(state).activity[0].userName, 'Alex Nguyen')
})
