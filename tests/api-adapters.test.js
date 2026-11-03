import test from 'node:test'
import assert from 'node:assert/strict'
import { missionRequest, equipmentRequest, farmView, farmRecordRequest, zoneView, nodeView } from '../src/services/api/adapters.js'
import { availableUavs, availableGateways, planFromMission } from '../src/services/api/planning.js'
import { readingThreshold } from '../src/services/api/analytics.js'

test('farm and zone adapters preserve GeoJSON and nullable legacy centers', () => {
  const boundary = { type: 'Polygon', coordinates: [[[106, 10], [107, 10], [107, 11], [106, 10]]] }
  const values = { name: 'Field', latitude: '10.3', longitude: '106.6', boundary }
  assert.deepEqual(farmRecordRequest('farm', values), { name: 'Field', description: null, latitude: 10.3, longitude: 106.6, boundary })
  assert.deepEqual(farmRecordRequest('zone', values), { name: 'Field', description: null, centerLatitude: 10.3, centerLongitude: 106.6, boundary })
  assert.equal(farmRecordRequest('farm', { name: 'Legacy' }).boundary, null)
  assert.equal(farmRecordRequest('farm', { name: 'Legacy' }).latitude, null)
  assert.equal(farmView({ boundary }).boundary, boundary)
  assert.equal(zoneView({ boundary }).boundary, boundary)
  assert.ok(zoneView({ boundary }).areaHectares > 0)
})

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
test('equipment selection requires active rental on the chosen farm', () => {
  const rental = { farmId: 1, status: 'ACTIVE', startAt: '2020-01-01T00:00:00Z', endAt: '2099-01-01T00:00:00Z' }
  const state = { uavs: [{ id: 5, isActive: true, rental }], gateways: [{ id: 8, isActive: true, uavId: 5, rental }], missions: [] }
  assert.equal(availableUavs(state, 1).length, 1)
  assert.equal(availableUavs(state, 2).length, 0)
  assert.equal(availableUavs({ ...state, uavs: [{ id: 5, isActive: true }] }, 1).length, 0)
  assert.equal(availableGateways(state, 1, 5).length, 1)
  assert.equal(availableGateways(state, 2, 5).length, 0)
  rental.endAt = '2021-01-01T00:00:00Z'
  assert.equal(availableUavs(state, 1).length, 0)
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
