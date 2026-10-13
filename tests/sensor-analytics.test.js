import test from 'node:test'
import assert from 'node:assert/strict'
import { createOperationsState } from '../src/data/mock/operationsState.js'
import { createFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { alerts } from '../src/data/mock/alerts.js'
import { selectAnalytics, engineerOverview, compareZoneAnalytics, hourlySeries, validateAnalyticsRange, ANALYTICS_DEFAULT_RANGE } from '../src/data/mock/sensorAnalytics.js'
import { beginSyncRetry, finishSyncRetry } from '../src/data/mock/syncState.js'

const workspace = createFarmWorkspace()
const filters = { ...ANALYTICS_DEFAULT_RANGE, farmId: 1, typeId: 1 }

test('analytics filters join farm, zone, sensor, type and inclusive measurement time', () => {
  const state = createOperationsState()
  const data = selectAnalytics(state, workspace, { ...filters, zoneId: 2, sensorId: 5, from: '2026-10-06T07:35', to: '2026-10-06T07:35' })
  assert.equal(data.rows.length, 1)
  assert.equal(data.rows[0].value, 36.8)
  assert.deepEqual(data.stats, { min: 36.8, max: 36.8, average: 36.8, count: 1 })
  assert.deepEqual(data.threshold, { min: 18, max: 35 })
  assert.equal(selectAnalytics(state, workspace, { ...filters, farmId: 2, sensorId: 5 }).rows.length, 0)
  assert.equal(selectAnalytics(state, workspace, { ...filters, zoneId: 2, typeId: 5 }).rows.length, 0)
})

test('buffered values require opt-in; mock receipts become available exactly once', () => {
  const state = createOperationsState()
  const synchronized = selectAnalytics(state, workspace, filters)
  const buffered = selectAnalytics(state, workspace, { ...filters, includeBuffered: true })
  assert.ok(synchronized.rows.every(({ receivedAt }) => receivedAt))
  assert.ok(buffered.rows.length > synchronized.rows.length)
  const next = finishSyncRetry(beginSyncRetry(state, 6, { activeRole: 'UAV_DEVICE_OPERATOR', fullName: 'Test operator' }), 6, 1)
  const updated = selectAnalytics(next, workspace, filters)
  assert.equal(updated.rows.length, synchronized.rows.length + 1)
  assert.equal(new Set(updated.rows.map(({ sourceRecordKey }) => sourceRecordKey)).size, updated.rows.length)
})

test('engineer KPI excludes offline/disabled/stale channels and scopes environmental alerts', () => {
  const state = createOperationsState()
  const overview = engineerOverview(state, workspace, alerts, 1)
  assert.equal(overview.activeNodes, 11)
  assert.equal(overview.metrics[0].contributing, 11)
  assert.deepEqual(overview.environmentalAlerts.map(({ id }) => id), [1, 2])
  assert.ok(overview.health.some(({ id, reason }) => id === 11 && reason === 'Offline'))
  assert.ok(overview.metrics[0].latest.find(({ node }) => node.id === 11).stale)
  const greenhouse = engineerOverview(state, workspace, alerts, 1, 2)
  assert.equal(greenhouse.environmentalAlerts.length, 1)
  assert.equal(greenhouse.metrics.find(({ id }) => id === 5).currentAverage, null)
  state.sensorNodes.find(({ id }) => id === 5).isActive = false
  assert.equal(engineerOverview(state, workspace, alerts, 1, 2).metrics[0].contributing, 3)
})

test('hourly aggregation weights channels equally and preserves missing hours as null', () => {
  const rows = [
    { sensorChannelId: 1, measuredAt: '2026-10-06T00:00:00Z', value: 10 },
    { sensorChannelId: 1, measuredAt: '2026-10-06T00:05:00Z', value: 20 },
    { sensorChannelId: 2, measuredAt: '2026-10-06T00:00:00Z', value: 30 },
  ]
  const data = hourlySeries(rows, { from: '2026-10-06T07:00', to: '2026-10-06T08:30' })
  assert.equal(data[0].value, 22.5)
  assert.equal(data[0].channels, 2)
  assert.equal(data[1].value, null)
})

test('zone comparison keeps all zones and missing type coverage is unavailable, not zero', () => {
  const data = compareZoneAnalytics(createOperationsState(), workspace, { ...filters, typeId: 2, zoneId: 2, sensorId: 5 })
  assert.equal(data.zones.length, 3)
  assert.equal(data.summaries[0].average, null)
  assert.equal(data.summaries[2].average, null)
  assert.ok(data.summaries[1].count > 12)
})

test('invalid, reversed, excessive, and empty date ranges do not fabricate readings', () => {
  assert.ok(validateAnalyticsRange({ from: '', to: filters.to }))
  assert.ok(validateAnalyticsRange({ from: filters.to, to: filters.from }))
  assert.ok(validateAnalyticsRange({ from: '2026-01-01T00:00', to: filters.to }))
  const data = selectAnalytics(createOperationsState(), workspace, { ...filters, from: '2026-10-07T00:00', to: '2026-10-07T12:00' })
  assert.equal(data.rows.length, 0)
  assert.equal(data.stats.average, null)
  assert.equal(data.threshold, null)
})
