import test from 'node:test'
import assert from 'node:assert/strict'
import { createOperationsState, changeOperationsState } from '../src/data/mock/operationsState.js'
import { createFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { getMissionMonitoring, updateMissionMonitoring, allowedMissionStatuses, supportsTelemetry } from '../src/data/mock/missionMonitoring.js'
import { emptyMissionPlan, groupCollectionPoints, makePlannedWaypoints, saveMissionPlan } from '../src/data/mock/missionPlanning.js'
import { getOperatorDashboard } from '../src/data/mock/operationsSelectors.js'
import { getFarmOwnerDashboard } from '../src/data/mock/farmOwnerDashboard.js'
import { collectionAttempts } from '../src/data/mock/collectionAttempts.js'

const workspace = createFarmWorkspace()
const actor = { id: 2, fullName: 'Minh Tran', activeRole: 'UAV_DEVICE_OPERATOR' }
const report = (state, missionId, status, note = 'Observed outcome from the field operator.') => updateMissionMonitoring(state, { missionId, status, note }, actor, workspace)

test('active mission progress, success rate, retry history and telemetry reconcile', () => {
  const state = createOperationsState(), data = getMissionMonitoring(1, state, workspace)
  assert.equal(data.progress.completedWaypoints, 6)
  assert.equal(data.progress.totalWaypoints, 10)
  assert.equal(data.progress.collectedTargets, 8)
  assert.equal(data.progress.totalTargets, 12)
  assert.equal(data.elapsed, '18m 0s')
  assert.equal(data.battery, 72)
  assert.equal(data.currentWaypoint.sequenceNo, 7)
  assert.equal(data.rows.find(({ status }) => status === 'IN_PROGRESS').node.deviceCode, 'NODE_009')
  assert.equal(data.successRate, 89)
  const retry = data.rows.find(({ node }) => node.id === 3)
  assert.equal(retry.records, 3)
  assert.deepEqual(retry.attempts.map(({ displayStatus }) => displayStatus), ['TIMEOUT', 'SUCCESS'])
  assert.equal(retry.errorRecovered, true)
  assert.ok(data.timeline.some(({ title }) => title === 'Retry'))
  assert.ok(data.timeline.every((event, index) => !index || event.at >= data.timeline[index - 1].at))
})

test('no telemetry, no attempts, historical failures and unknown missions are handled', () => {
  const state = createOperationsState()
  assert.equal(supportsTelemetry({ supports_telemetry: false, telemetrySupport: true }), false)
  const ready = getMissionMonitoring(2, state, workspace)
  assert.equal(supportsTelemetry(ready.uav), false)
  assert.equal(ready.telemetry, null)
  assert.equal(ready.successRate, null)
  assert.equal(ready.elapsed, 'Not started')
  const failed = getMissionMonitoring(5, state, workspace)
  assert.equal(failed.elapsed, '4m 0s')
  assert.equal(failed.battery, 81)
  assert.ok(failed.rows.some((row) => row.status === 'FAILED' && row.lastError.errorCode === 'GATEWAY_BUFFER_ERROR'))
  assert.ok(failed.timeline.some(({ title }) => title === 'Mission Failed'))
  assert.equal(getMissionMonitoring(999, state, workspace), null)
})

test('notes append without changing mission or attempts and preserve authorship', () => {
  const state = createOperationsState()
  const next = report(state, 1, undefined, 'Inspect the irrigation sensor after collection.')
  assert.deepEqual(next.missions, state.missions)
  assert.equal(next.missionEvents[0].actorName, 'Minh Tran')
  assert.equal(state.missionEvents.length, 0)
  assert.equal(getMissionMonitoring(1, next, workspace).notes.length, 1)
  assert.throws(() => report(state, 1, undefined, '  '), /operational note/)
})

test('partial close preserves evidence, skips pending work, updates dashboards and cannot reopen', () => {
  const state = createOperationsState(), attemptsBefore = structuredClone(collectionAttempts)
  assert.deepEqual(allowedMissionStatuses(state.missions[0], state), ['PARTIAL', 'FAILED', 'CANCELLED'])
  assert.throws(() => report(state, 1, 'COMPLETED'), /transition/)
  const next = report(state, 1, 'PARTIAL')
  const data = getMissionMonitoring(1, next, workspace)
  assert.equal(data.rows.filter(({ status }) => status === 'SKIPPED').length, 4)
  assert.equal(data.progress.collectedTargets, 8)
  assert.equal(data.progress.completedWaypoints, 6)
  assert.equal(data.telemetry, null)
  assert.ok(data.timeline.some(({ id }) => id === 'current-collection'))
  assert.equal(data.timeline.filter(({ title }) => title.startsWith('Mission Partial')).length, 1)
  assert.deepEqual(collectionAttempts, attemptsBefore)
  assert.deepEqual(next.uavs, state.uavs)
  assert.equal(getOperatorDashboard(1, next, workspace).activeMissions, 0)
  assert.equal(getFarmOwnerDashboard(1, workspace, undefined, next.sensorNodes, next).activeMissionCount, 0)
  assert.throws(() => report(next, 1, 'IN_PROGRESS'), /transition/)
})

test('observed start requires due schedule and available equipment; active assignments lock', () => {
  let state = createOperationsState()
  assert.throws(() => report(state, 2, 'IN_PROGRESS'), /scheduled/)
  const points = groupCollectionPoints(state.sensorNodes.filter(({ id }) => id === 5))
  state = saveMissionPlan(state, { ...emptyMissionPlan(), name: 'Observed collection', farmId: 1, sensorIds: [5], points, waypoints: makePlannedWaypoints(workspace.farms[0], points), uavId: 2, gatewayId: 2 }, workspace, actor)
  const missionId = state.missions.at(-1).id
  const running = report(state, missionId, 'IN_PROGRESS')
  assert.equal(getOperatorDashboard(1, running, workspace).activeMissions, 2)
  assert.equal(getOperatorDashboard(1, running, workspace).availableUavs, 0)
  assert.equal(getMissionMonitoring(missionId, running, workspace).telemetry, null)
  assert.throws(() => changeOperationsState(running, { kind: 'gateway', id: 2, action: 'assign', values: { uavId: null } }, workspace, actor), /locked/)
  assert.throws(() => report(running, missionId, 'PARTIAL'), /transition/)
  const completedEvidence = { ...running, missionTargets: running.missionTargets.map((target) => target.missionId === missionId ? { ...target, status: 'COLLECTED' } : target), missionWaypoints: running.missionWaypoints.map((waypoint) => waypoint.missionId === missionId ? { ...waypoint, status: 'COMPLETED' } : waypoint) }
  assert.equal(report(completedEvidence, missionId, 'COMPLETED').missions.at(-1).status, 'COMPLETED')
})

test('all other roles cannot report status or append notes', () => {
  for (const activeRole of ['ADMINISTRATOR', 'FARM_OWNER', 'AGRICULTURAL_ENGINEER']) {
    for (const status of [undefined, 'FAILED']) assert.throws(() => updateMissionMonitoring(createOperationsState(), { missionId: 1, note: 'Test', status }, { ...actor, activeRole }, workspace), /Only operators/)
  }
})
