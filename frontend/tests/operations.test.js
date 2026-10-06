import test from 'node:test'
import assert from 'node:assert/strict'
import { createOperationsState, changeOperationsState, sensorDisplayStatus } from '../src/data/mock/operationsState.js'
import { createFarmWorkspace, getDeletionBlock, updateFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { getOperationalSensorDetails, getOperatorDashboard } from '../src/data/mock/operationsSelectors.js'
import { getAdminDashboard } from '../src/data/mock/adminDashboard.js'
import { ROLES } from '../src/config/roles.js'

const operator = { id: 2, activeRole: ROLES.UAV_DEVICE_OPERATOR }
const workspace = createFarmWorkspace()
const update = (state, change, actor = operator, farms = workspace) => changeOperationsState(state, change, farms, actor)
const sensor = { deviceCode: 'NODE_TEST', name: 'Test field sensor', zoneId: 1, protocol: 'LORA', serialNumber: 'SERIAL_TEST', macAddress: '', isActive: true }

test('sensor register/edit/assign/disable preserve unknown telemetry and immutable history', () => {
  const original = createOperationsState()
  let state = update(original, { kind: 'sensor', values: sensor })
  const id = state.sensorNodes.at(-1).id
  assert.equal(original.sensorNodes.length, 15)
  assert.equal(state.sensorNodes.at(-1).status, 'OFFLINE')
  assert.equal(state.sensorNodes.at(-1).batteryPercent, null)
  assert.equal(getOperationalSensorDetails(id, state, workspace).lastCollectedAt, null)
  assert.deepEqual(getOperationalSensorDetails(id, state, workspace).channels, [])
  state = update(state, { kind: 'sensor', id, values: { name: 'Renamed sensor', zoneId: 2, isActive: false } })
  assert.equal(state.sensorNodes.at(-1).zoneId, 2)
  assert.equal(sensorDisplayStatus(state.sensorNodes.at(-1)), 'INACTIVE')
  state = update(state, { kind: 'sensor', id, values: { isActive: true } })
  assert.equal(sensorDisplayStatus(state.sensorNodes.at(-1)), 'OFFLINE')
  assert.equal(getAdminDashboard(workspace, undefined, state).totalSensors, 16)
  assert.throws(() => update(state, { kind: 'sensor', values: sensor }), /already registered/)
  assert.throws(() => update(state, { kind: 'sensor', id, values: { zoneId: 4 } }), /current farm/)
  assert.throws(() => update(state, { kind: 'sensor', id, values: { deviceCode: 'NEW_CODE' } }), /cannot be changed/)
})

test('invalid inventory values and active mission changes are rejected', () => {
  const state = createOperationsState()
  for (const values of [{ ...sensor, zoneId: 999 }, { ...sensor, protocol: 'BAD' }, { ...sensor, serialNumber: '', macAddress: '' }, { ...sensor, macAddress: 'not-a-mac' }, { ...sensor, serialNumber: state.sensorNodes[0].serialNumber }]) assert.throws(() => update(state, { kind: 'sensor', values }))
  assert.throws(() => update(state, { kind: 'sensor', id: 1, values: { isActive: false } }), /active mission/)
  assert.throws(() => update(state, { kind: 'sensor', id: 1, values: { zoneId: 2 } }), /active mission/)
  assert.throws(() => update(state, { kind: 'gateway', id: 1, action: 'assign', values: { uavId: null } }), /locked/)
})

test('gateway assignments enforce existing, same-farm, available UAV relationships', () => {
  let state = createOperationsState()
  state = update(state, { kind: 'gateway', values: { code: 'GW_TEST', name: 'Test gateway', farmId: 1, gatewayType: 'ESP32', softwareVersion: '1.0' } })
  const id = state.gateways.at(-1).id
  assert.equal(state.gateways.at(-1).uavId, null)
  assert.throws(() => update(state, { kind: 'gateway', id, action: 'assign', values: { uavId: 2 } }), /already has a gateway/)
  state = update(state, { kind: 'gateway', id: 2, action: 'assign', values: { uavId: null } })
  state = update(state, { kind: 'gateway', id, action: 'assign', values: { uavId: 2 } })
  assert.equal(state.gateways.at(-1).uavId, 2)
  state = update(state, { kind: 'uav', values: { code: 'UAV_TEST', name: 'Test UAV', farmId: 2, platform: 'VTOL', model: 'Survey model', gpsSupport: true, telemetrySupport: false } })
  assert.equal(state.uavs.at(-1).status, 'OFFLINE')
  assert.throws(() => update(state, { kind: 'gateway', id, action: 'assign', values: { uavId: state.uavs.at(-1).id } }), /same farm/)
})

test('new sensor registrations participate in farm deletion safeguards', () => {
  const farms = updateFarmWorkspace(workspace, { kind: 'zone', values: { farmId: 1, name: 'New zone', latitude: 10, longitude: 105, areaHectares: 1 }, userId: 1 }, true)
  const zoneId = farms.zones.at(-1).id
  const state = update(createOperationsState(), { kind: 'sensor', values: { ...sensor, zoneId } }, operator, farms)
  assert.match(getDeletionBlock(farms, 'zone', zoneId, state), /linked sensors/)
  assert.throws(() => updateFarmWorkspace(farms, { kind: 'zone', id: zoneId, remove: true }, true, state), /linked sensors/)
})

test('operator dashboard derives readiness, failed targets, buffered data and farm scope', () => {
  const state = createOperationsState()
  const data = getOperatorDashboard(1, state, workspace)
  assert.deepEqual([data.availableUavs, data.onlineGateways, data.activeNodes, data.readyMissions, data.activeMissions, data.pending.length], [1, 2, 12, 1, 1, 2])
  assert.equal(data.mission.progress.collectedTargets, 8)
  assert.equal(data.mission.progress.completedWaypoints, 6)
  assert.equal(data.failures.length, 2)
  assert.ok(!data.failures.some(({ sensorNodeId }) => sensorNodeId === 3))
  const detail = getOperationalSensorDetails(3, state, workspace)
  assert.ok(detail.history.some(({ status }) => status === 'FAILED'))
  assert.ok(detail.history.some(({ status }) => status === 'SUCCESS'))
  assert.ok(detail.channels.some(({ latestReading }) => latestReading && !latestReading.receivedAt))
  assert.equal(getOperationalSensorDetails(999, state, workspace), null)
  assert.equal(getOperatorDashboard(2, state, workspace).activeMissions, 0)
})

test('all non-operator roles are read-only for inventory mutations', () => {
  for (const role of [ROLES.ADMINISTRATOR, ROLES.FARM_OWNER, ROLES.AGRICULTURAL_ENGINEER]) {
    assert.throws(() => update(createOperationsState(), { kind: 'sensor', values: sensor }, { id: 1, activeRole: role }), /cannot manage/)
    assert.throws(() => update(createOperationsState(), { kind: 'gateway', id: 2, action: 'assign', values: { uavId: null } }, { id: 1, activeRole: role }), /cannot manage/)
  }
})
