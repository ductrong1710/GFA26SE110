import test from 'node:test'
import assert from 'node:assert/strict'
import { compareSensorAnalytics } from '../src/services/api/analytics.js'

test('sensor comparison keeps sensors in the same zone separate and excludes invalid or foreign readings', () => {
  const workspace = { zones: [{ id: 1, farmId: 1 }, { id: 2, farmId: 2 }] }
  const operations = {
    sensorNodes: [{ id: 1, zoneId: 1, name: 'North' }, { id: 2, zoneId: 1, name: 'South' }, { id: 3, zoneId: 1, name: 'Empty' }, { id: 4, zoneId: 2, name: 'Private' }],
    sensorChannels: [1, 2, 3, 4].map(id => ({ id, sensorNodeId: id, sensorTypeId: 1 })),
    sensorTypes: [{ id: 1, name: 'Temperature', unit: 'C' }],
    readings: [
      { id: 1, sensorChannelId: 1, value: 10, isValid: true },
      { id: 2, sensorChannelId: 2, value: 30, isValid: true },
      { id: 3, sensorChannelId: 2, value: 1000, isValid: false },
      { id: 4, sensorChannelId: 4, value: 99, isValid: true },
    ].map(r => ({ ...r, measuredAt: '2030-01-01T02:00:00Z', receivedAt: '2030-01-01T02:01:00Z' })),
  }
  const rows = compareSensorAnalytics(operations, workspace, { farmId: 1, typeId: 1, sensorId: 1, from: '2030-01-01T08:00', to: '2030-01-01T10:00' })
  assert.deepEqual(rows.map(r => [r.name, r.average, r.count]), [['North', 10, 1], ['South', 30, 1], ['Empty', null, 0]])
})
