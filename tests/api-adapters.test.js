import test from 'node:test'
import assert from 'node:assert/strict'
import { missionRequest, equipmentRequest, zoneView, nodeView } from '../src/services/api/adapters.js'
import { availableUavs, planFromMission } from '../src/services/api/planning.js'
import { readingThreshold } from '../src/services/api/analytics.js'

test('missing and one-sided thresholds do not become zero-value alarm limits', () => {
  assert.equal(readingThreshold(20, { warningMin: null, warningMax: null }).label, 'Not configured')
  assert.equal(readingThreshold(-5, { warningMin: null, warningMax: 10 }).status, 'SUCCESS')
  assert.equal(readingThreshold(20, { warningMin: 5, warningMax: null }).status, 'SUCCESS')
  assert.equal(readingThreshold(20, { warningMin: null, warningMax: 10 }).status, 'WARNING')
})

test('sensor adapter preserves null coordinates and explicitly serializes supported fields only', () => {
  assert.equal(zoneView({ centerLatitude: null, centerLongitude: null }).latitude, null)
  assert.equal(nodeView({ isActive: false, status: 'ONLINE' }).displayStatus, 'INACTIVE')
  const request = equipmentRequest('sensor', { zoneId: '3', deviceCode: 'N', name: 'Node', protocol: 'LORA', serialNumber: 'fake', latitude: '', longitude: '' })
  assert.equal(request.latitude, null)
  assert.equal(request.zoneId, 3)
  assert.equal(Object.hasOwn(request, 'protocol'), false)
})
test('equipment is available without inventing farm ownership or GPS capabilities', () => {
  assert.equal(availableUavs({ uavs: [{ id: 5, isActive: true }], missions: [] }, 1).length, 1)
  const request = equipmentRequest('uav', { name: 'UAV', code: 'U', farmId: 99, gpsSupport: true })
  assert.equal(Object.hasOwn(request, 'farmId'), false)
  assert.equal(Object.hasOwn(request, 'gpsSupport'), false)
})
test('mission target assignments reference submitted waypoint sequences and UTC window', () => {
  const request = missionRequest({ name: 'Survey', farmId: '1', sensorIds: [7], points: [{ id: 2, sensorIds: [7] }], waypoints: [{ id: 10, pointId: '2', coordinateType: 'GPS', x: '10', y: '106', altitude: '30', action: 'COLLECT_SENSOR' }], scheduledLocal: '2026-10-11T09:00', scheduledEndLocal: '2026-10-11T10:00' })
  assert.deepEqual(request.targetAssignments, [{ sensorNodeId: 7, sequenceNo: 1, waypointSequenceNo: 1 }])
  assert.equal(request.waypoints[0].latitude, 10)
  assert.equal(request.scheduledStartAt, '2026-10-11T02:00:00.000Z')
})
test('mission rejects missing target waypoint and half scheduling window', () => {
  const plan = { name: 'Survey', farmId: 1, sensorIds: [], points: [], waypoints: [], scheduledLocal: '2026-10-11T09:00' }
  assert.throws(() => missionRequest(plan), /both scheduled/)
  assert.throws(() => missionRequest({ ...plan, scheduledLocal: '', sensorIds: [7] }), /collection waypoint/)
})
test('backend mission can resume without fabricating targets or coordinates', () => {
  const plan = planFromMission({ id: 1, name: 'Draft', farm: { id: 3 }, targets: [], waypoints: [], scheduledStartAt: null })
  assert.equal(plan.farmId, 3)
  assert.deepEqual(plan.sensorIds, [])
})
