import test from 'node:test'
import assert from 'node:assert/strict'
import { getMissionMonitoring } from '../src/services/api/monitoring.js'

test('latest error follows backend attempt timestamps and ID tie-break regardless of input order', () => {
  const attempts = [
    { id: 3, missionTargetId: 1, startedAt: '2026-10-28T02:00:00Z', errorCode: 'LATEST', recordsReceived: 0 },
    { id: 2, missionTargetId: 1, startedAt: '2026-10-28T02:00:00Z', errorCode: 'SAME_TIME', recordsReceived: 0 },
    { id: 1, missionTargetId: 1, startedAt: '2026-10-28T01:00:00Z', errorCode: 'OLDEST', recordsReceived: 0 },
  ]
  const state = { missions: [{ id: 1, farmId: 1 }], missionTargets: [{ id: 1, missionId: 1 }], missionWaypoints: [], sensorNodes: [], uavs: [], gateways: [], missionLogs: [], collectionAttempts: attempts }
  for (const order of [attempts, [...attempts].reverse()]) {
    const data = getMissionMonitoring(1, { ...state, collectionAttempts: order }, { farms: [{ id: 1 }] })
    assert.equal(data.rows[0].lastError.errorCode, 'LATEST')
    assert.deepEqual(data.rows[0].attempts.map(a => a.id), [3, 2, 1])
  }
  assert.deepEqual(attempts.map(a => a.id), [3, 2, 1])
})
