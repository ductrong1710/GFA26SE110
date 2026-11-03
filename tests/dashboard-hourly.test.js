import assert from 'node:assert/strict'
import test from 'node:test'
import { getFarmOwnerDashboard } from '../src/services/api/dashboard.js'

test('owner trend retains the hour without samples between two measurements', () => {
  const hour = 3600000
  const endHour = Math.floor(Date.now() / hour) * hour
  const readings = [0, 24].map((value, index) => {
    const measuredAt = new Date(endHour - (3 - index * 2) * hour).toISOString()
    return { id: index + 1, sensorChannelId: 1, value, measuredAt, receivedAt: measuredAt, isValid: true }
  })
  const dashboard = getFarmOwnerDashboard(1, { farms: [{ id: 1 }], zones: [{ id: 1, farmId: 1 }] }, [],
    [{ id: 1, zoneId: 1, status: 'ONLINE', isActive: true }], { sensorTypes: [{ id: 1 }], readings,
      sensorChannels: [{ id: 1, sensorNodeId: 1, sensorTypeId: 1, isActive: true }] })
  assert.deepEqual(dashboard.environment[0].series.slice(-4).map(point => point.value), [0, null, 24, null])
})
