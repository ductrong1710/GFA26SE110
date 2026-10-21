import test from 'node:test'
import assert from 'node:assert/strict'
import * as planning from '../src/services/api/planning.js'
import { missionRequest } from '../src/services/api/adapters.js'
const uav = { id: 1, model: 'DJI TeLLo EDU', isActive: true }
const state = { uavs: [uav], missions: [], gateways: [{ id: 1, isActive: true, uavId: 1 }], sensorNodes: [{ id: 1, zoneId: 1, isActive: true }] }
const workspace = { farms: [{ id: 1 }], zones: [{ id: 1, farmId: 1 }] }
function plan() { return { ...planning.emptyMissionPlan(), name: 'Indoor', farmId: 1, uavId: 1, gatewayId: 1, flightAltitude: 1, sensorIds: [1], points: [{ id: 1, name: 'Point', x: 1, y: 0, sensorIds: [1] }], waypoints: [{ id: 1, coordinateType: 'RELATIVE', x: 1, y: 0, altitude: 1, holdSeconds: 0, action: 'COLLECT', pointId: 1 }] } }
test('Tello recognizes model and permits a local collection route without GPS or lifecycle waypoints', () => {
  assert.equal(typeof planning.isTello, 'function')
  assert.equal(planning.isTello(uav), true)
  assert.equal(planning.isTello({ model: 'Other' }), false)
  assert.deepEqual(planning.validateMissionPlan(plan(), state, workspace).flat(), [])
})
test('Tello rejects unsafe units, distances, actions, capacities and holds', () => {
  for (const mutate of [p => p.waypoints[0].coordinateType = 'GPS', p => p.waypoints[0].x = 0.1, p => p.waypoints[0].x = 100.01, p => p.waypoints[0].x = 1.001, p => p.waypoints[0].holdSeconds = 31, p => p.waypoints[0].altitude = 30, p => p.waypoints[0].action = 'LAND', p => p.waypoints = Array.from({ length: 17 }, (_, i) => ({ ...p.waypoints[0], id: i + 1 })), p => p.sensorIds = Array.from({ length: 9 }, (_, i) => i + 1)]) {
    const p = plan(); mutate(p)
    assert.ok(planning.validateMissionPlan(p, state, workspace).flat().length, JSON.stringify(p))
    assert.throws(() => missionRequest(p, uav))
  }
})
test('Tello request uses meters and COLLECT, preserving target assignment; resumes local points', () => {
  const p = plan(); p.waypoints[0].action = 'COLLECT_SENSOR'
  const request = missionRequest(p, uav)
  assert.equal(request.waypoints[0].actionType, 'COLLECT')
  assert.equal(request.waypoints[0].localX, 1)
  assert.equal(request.waypoints[0].latitude, null)
  assert.equal(request.waypoints[0].altitudeM, 1)
  assert.equal(request.targetAssignments[0].waypointSequenceNo, 1)
  const resumed = planning.planFromMission({ id: 1, name: 'Indoor', farm: { id: 1 }, uav, targets: [{ sensorNodeId: 1, waypointId: 9 }], waypoints: [{ id: 9, ...request.waypoints[0] }] })
  assert.equal(resumed.points[0].x, 1)
  assert.equal(resumed.flightAltitude, 1)
})
test('Tello validates movement from origin, between waypoints, and return leg', () => {
  const p = plan(); p.waypoints.push({ ...p.waypoints[0], id: 2, x: 1.1 })
  assert.ok(planning.validateMissionPlan(p, state, workspace).flat().some(e => /20 cm/.test(e)))
  p.waypoints[1].x = 0.1
  assert.ok(planning.validateMissionPlan(p, state, workspace).flat().some(e => /20 cm/.test(e)))
})
