import assert from 'node:assert/strict'
import test from 'node:test'
import { getStatusMeta, getBatteryState, getProgress } from '../src/components/app/ui/uiState.js'

test('every agreed status has the correct semantic color', () => {
  const groups = {
    success: 'ACTIVE ONLINE SUCCESS COMPLETED CLOSED COLLECTED',
    info: 'READY SCHEDULED IN_PROGRESS SYNCING INFO ACKNOWLEDGED',
    warning: 'PENDING PARTIAL WARNING LOW_BATTERY MAINTENANCE OPEN',
    danger: 'FAILED ERROR CRITICAL OFFLINE TIMEOUT',
    neutral: 'DRAFT INACTIVE CANCELLED SKIPPED',
  }
  for (const [tone, statuses] of Object.entries(groups)) {
    for (const status of statuses.split(' ')) assert.equal(getStatusMeta(status).tone, tone, status)
  }
  assert.deepEqual(getStatusMeta(' in-progress '), { status: 'IN_PROGRESS', label: 'In progress', tone: 'info' })
})

test('unknown statuses never imply success or throw', () => {
  for (const status of [null, undefined, '', '   ', 'NEW_STATUS', '__proto__', {}, 0]) {
    assert.equal(getStatusMeta(status).tone, 'neutral')
    assert.ok(getStatusMeta(status).label)
  }
})

test('progress clamps measured values and keeps unknown data distinct from zero', () => {
  assert.deepEqual(getProgress(4, 8), { value: 4, max: 8, percent: 50 })
  assert.equal(getProgress(-5).percent, 0)
  assert.equal(getProgress(999).percent, 100)
  assert.equal(getProgress(0).value, 0)
  for (const invalid of [null, undefined, NaN, Infinity, '40']) assert.equal(getProgress(invalid).value, null)
  for (const max of [0, -1, NaN, Infinity, null]) assert.equal(getProgress(10, max).percent, null)
})

test('battery thresholds, empty battery and unavailable telemetry', () => {
  for (const value of [0, 10]) assert.equal(getBatteryState(value).tone, 'danger')
  for (const value of [11, 20]) assert.equal(getBatteryState(value).tone, 'warning')
  for (const value of [21, 100]) assert.equal(getBatteryState(value).tone, 'success')
  assert.equal(getBatteryState(150).level, 100)
  assert.equal(getBatteryState(-1).level, 0)
  assert.equal(getBatteryState(null).tone, 'neutral')
  assert.equal(getBatteryState(null).level, null)
})
