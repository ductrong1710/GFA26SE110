import test from 'node:test'
import assert from 'node:assert/strict'
import { createOperationsState } from '../src/data/mock/operationsState.js'
import { createFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { emptyMissionPlan, groupCollectionPoints, makePlannedWaypoints, validateMissionPlan, saveMissionPlan, waypointPosition, availableUavs, availableGateways, MISSION_STATUSES } from '../src/data/mock/missionPlanning.js'
import { getOperatorDashboard } from '../src/data/mock/operationsSelectors.js'
import { getAdminDashboard } from '../src/data/mock/adminDashboard.js'

const workspace = createFarmWorkspace()
const actor = { id: 2, activeRole: 'UAV_DEVICE_OPERATOR' }
function validPlan(state) {
  const sensorIds = [5, 6, 7, 8]
  const points = groupCollectionPoints(state.sensorNodes.filter(({ id }) => sensorIds.includes(id)))
  return { ...emptyMissionPlan(), name: 'Greenhouse afternoon collection', farmId: 1, sensorIds, points, waypoints: makePlannedWaypoints(workspace.farms[0], points), uavId: 2, gatewayId: 2 }
}

test('mock mission states and equipment availability use shared linked data', () => {
  const state = createOperationsState()
  assert.deepEqual(new Set(state.missions.map(({ status }) => status)), new Set(MISSION_STATUSES))
  assert.deepEqual(availableUavs(state, 1).map(({ id }) => id), [2])
  assert.deepEqual(availableGateways(state, 1, 2).map(({ id }) => id), [2])
  assert.equal(availableGateways(state, 1, 1).length, 0)
  for (const target of state.missionTargets) {
    assert.ok(state.missions.some(({ id }) => id === target.missionId))
    assert.ok(state.missionWaypoints.some((waypoint) => waypoint.id === target.waypointId && waypoint.missionId === target.missionId))
  }
})

test('nearby grouping and both coordinate types produce a valid planning route', () => {
  const state = createOperationsState(), plan = validPlan(state)
  assert.equal(plan.points.length, 1)
  assert.deepEqual(validateMissionPlan(plan, state, workspace).flat(), [])
  plan.waypoints[0] = { ...plan.waypoints[0], coordinateType: 'RELATIVE', x: 0, y: 0 }
  assert.deepEqual(waypointPosition(plan.waypoints[0], workspace.farms[0]), { latitude: workspace.farms[0].latitude, longitude: workspace.farms[0].longitude })
  assert.deepEqual(validateMissionPlan(plan, state, workspace).flat(), [])
})

test('validation rejects cross-farm targets, incomplete point coverage, and invalid routes', () => {
  const state = createOperationsState()
  for (const mutate of [
    (plan) => plan.sensorIds.push(13),
    (plan) => plan.points[0].sensorIds.pop(),
    (plan) => { plan.points[0].latitude += 1 },
    (plan) => { plan.waypoints[1].x = '' },
    (plan) => { plan.waypoints[1].altitude = 121 },
    (plan) => { plan.waypoints[1].pointId = 999 },
    (plan) => { plan.waypoints[1].action = 'MOVE' },
    (plan) => { plan.waypoints.at(-1).altitude = 30 },
    (plan) => { plan.waypoints[0].action = 'WAIT' },
    (plan) => { plan.waypoints[1].coordinateType = 'RELATIVE'; plan.waypoints[1].x = 5001 },
    (plan) => { plan.scheduledLocal = '2026-10-05T08:00' },
  ]) {
    const plan = validPlan(state); mutate(plan)
    assert.ok(validateMissionPlan(plan, state, workspace).flat().length)
    assert.throws(() => saveMissionPlan(state, plan, workspace, actor))
  }
})

test('equipment and sensor eligibility are rechecked at final save', () => {
  for (const mutate of [
    (state) => { state.uavs[1].batteryPercent = 20 },
    (state) => { state.uavs[1].status = 'MAINTENANCE' },
    (state) => { state.uavs[1].gpsSupport = false },
    (state) => { state.gateways[1].uavId = 1 },
    (state) => { state.gateways[1].status = 'OFFLINE' },
    (state) => { state.sensorNodes[4].isActive = false },
  ]) {
    const state = createOperationsState(), plan = validPlan(state); mutate(state)
    assert.throws(() => saveMissionPlan(state, plan, workspace, actor))
  }
})

test('draft can be resumed and finalized once, preserving IDs and normalizing relations', () => {
  const original = createOperationsState()
  const draftState = saveMissionPlan(original, { ...emptyMissionPlan(), wizardStep: 3 }, workspace, actor, true)
  const draft = draftState.missions.at(-1)
  assert.equal(draft.status, 'DRAFT')
  assert.equal(draft.plan.wizardStep, 3)
  assert.equal(original.missions.length, 8)
  const plan = { ...validPlan(draftState), missionId: draft.id, scheduledLocal: '2026-10-06T14:00' }
  const ready = saveMissionPlan(draftState, plan, workspace, actor)
  assert.equal(ready.missions.length, 9)
  assert.equal(ready.missions.at(-1).status, 'READY')
  assert.equal(ready.missions.at(-1).scheduledStartAt, '2026-10-06T07:00:00.000Z')
  const targets = ready.missionTargets.filter(({ missionId }) => missionId === draft.id)
  assert.equal(targets.length, 4)
  assert.ok(targets.every((target) => ready.missionWaypoints.some((waypoint) => waypoint.id === target.waypointId && waypoint.missionId === draft.id)))
  assert.equal(getOperatorDashboard(1, ready, workspace).readyMissions, 2)
  assert.equal(getAdminDashboard(workspace, undefined, ready).missions.length, 9)
  assert.throws(() => saveMissionPlan(ready, plan, workspace, actor), /Only draft/)
  plan.points[0].name = 'Changed outside state'
  assert.notEqual(ready.missions.at(-1).plan.points[0].name, plan.points[0].name)
})

test('other roles cannot save even a draft', () => {
  for (const activeRole of ['ADMINISTRATOR', 'FARM_OWNER', 'AGRICULTURAL_ENGINEER']) {
    assert.throws(() => saveMissionPlan(createOperationsState(), emptyMissionPlan(), workspace, { id: 1, activeRole }, true), /cannot create/)
  }
})
