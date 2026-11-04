import test from 'node:test'
import assert from 'node:assert/strict'
import { compareZoneAnalytics } from '../src/services/api/analytics.js'

test('API zone comparison separates units and aligns missing hours without inventing zero readings', () => {
  const workspace = { zones: [{ id: 1, farmId: 1 }, { id: 2, farmId: 1 }, { id: 3, farmId: 2 }] }
  const operations = {
    sensorNodes: [{ id: 1, zoneId: 1 }, { id: 2, zoneId: 2 }, { id: 3, zoneId: 3 }],
    sensorTypes: [{ id: 1, name: 'Temperature', unit: 'C' }, { id: 2, name: 'Humidity', unit: '%' }],
    sensorChannels: [
      { id: 1, sensorNodeId: 1, sensorTypeId: 1 },
      { id: 2, sensorNodeId: 2, sensorTypeId: 1 },
      { id: 3, sensorNodeId: 1, sensorTypeId: 2 },
      { id: 4, sensorNodeId: 3, sensorTypeId: 1 },
    ],
    readings: [[1, 0, 0], [2, 2, 24], [3, 1, 85], [4, 0, 99]].map(([sensorChannelId, hour, value], id) => ({
      id, sensorChannelId, value, isValid: true,
      measuredAt: `2026-11-01T0${hour}:00:00Z`, receivedAt: '2026-11-01T03:00:00Z',
    })),
  }
  const filters = { farmId: 1, typeId: 1, from: '2026-11-01T07:00', to: '2026-11-01T09:00' }
  const temperature = compareZoneAnalytics(operations, workspace, filters)
  assert.deepEqual(temperature.summaries.map(({ average, count }) => [average, count]), [[0, 1], [24, 1]])
  assert.deepEqual(temperature.series, [
    { at: Date.parse('2026-11-01T00:00:00Z'), zone1: 0, zone2: null },
    { at: Date.parse('2026-11-01T01:00:00Z'), zone1: null, zone2: null },
    { at: Date.parse('2026-11-01T02:00:00Z'), zone1: null, zone2: 24 },
  ])
  const humidity = compareZoneAnalytics(operations, workspace, { ...filters, typeId: 2 })
  assert.deepEqual(humidity.summaries.map(({ average, count }) => [average, count]), [[85, 1], [null, 0]])
})
