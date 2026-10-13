import test from 'node:test'
import assert from 'node:assert/strict'
import { createOperationsState } from '../src/data/mock/operationsState.js'
import { beginSyncRetry, finishSyncRetry, getSyncOverview, syncCounts } from '../src/data/mock/syncState.js'
import { createFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { getOperatorDashboard, getGatewayLastSync } from '../src/data/mock/operationsSelectors.js'
import { getAdminDashboard } from '../src/data/mock/adminDashboard.js'
import { getFarmOwnerDashboard } from '../src/data/mock/farmOwnerDashboard.js'
import { sensorReadings } from '../src/data/mock/sensorReadings.js'
import { MOCK_NOW } from '../src/data/mock/scenario.js'

const actor = { id: 2, activeRole: 'UAV_DEVICE_OPERATOR', fullName: 'Minh Tran' }
const workspace = createFarmWorkspace()

test('sync view reconciles all statuses, record outcomes, and unique offline queue', () => {
  const state = createOperationsState(), view = getSyncOverview(state, 1, workspace)
  assert.deepEqual(view.counts, { PENDING: 1, SYNCING: 1, SUCCESS: 1, PARTIAL: 1, FAILED: 2 })
  assert.equal(view.rejected, 4)
  assert.equal(view.localQueueCount, 18)
  assert.equal(getSyncOverview(state, 2, workspace).batches.length, 0)
  for (const batch of view.batches) {
    assert.ok(batch.gateway && batch.mission)
    const counts = syncCounts(batch.records)
    assert.equal(counts.recordCount, counts.acceptedCount + counts.duplicateCount + counts.rejectedCount + counts.pendingCount)
  }
})

test('transport failure retries through syncing to success and persists receipt/history once', () => {
  const initial = createOperationsState()
  const running = beginSyncRetry(initial, 6, actor)
  assert.equal(running.syncBatches[5].status, 'SYNCING')
  assert.equal(running.syncBatches[5].completedAt, null)
  assert.equal(getAdminDashboard(workspace, undefined, running).pendingSync, 3)
  assert.equal(getOperatorDashboard(1, running, workspace).pending.length, 3)
  assert.throws(() => beginSyncRetry(running, 6, actor), /Only failed/)
  const complete = finishSyncRetry(running, 6, 1), batch = complete.syncBatches[5]
  assert.equal(batch.status, 'SUCCESS')
  assert.equal(batch.acceptedCount, 3)
  assert.equal(batch.rejectedCount, 0)
  assert.equal(batch.retryCount, 1)
  assert.equal(batch.history.length, 2)
  assert.equal(batch.history[0].status, 'FAILED')
  assert.equal(batch.history[1].actorName, 'Minh Tran')
  assert.equal(batch.history[1].status, 'SUCCESS')
  assert.equal(complete.syncReceipts.length, 3)
  assert.equal(finishSyncRetry(complete, 6, 1), complete)
  assert.equal(getGatewayLastSync(1, complete.syncBatches), MOCK_NOW)
  assert.equal(getFarmOwnerDashboard(1, workspace, undefined, complete.sensorNodes, complete).latestSyncAt, MOCK_NOW)
  assert.equal(getSyncOverview(complete, 1, workspace).unacknowledgedCount, 15)
  assert.equal(initial.syncBatches[5].status, 'FAILED')
  assert.ok(batch.records.every(({ readingId }) => !sensorReadings.find(({ id }) => id === readingId).receivedAt))
})

test('invalid signature records stay rejected after a retry, without database receipts', () => {
  const state = finishSyncRetry(beginSyncRetry(createOperationsState(), 5, actor), 5, 1)
  const batch = state.syncBatches[4]
  assert.equal(batch.status, 'FAILED')
  assert.equal(batch.acceptedCount, 0)
  assert.equal(batch.rejectedCount, 3)
  assert.equal(state.syncReceipts.length, 0)
  assert.ok(batch.records.every(({ status, errorCode }) => status === 'REJECTED' && errorCode === 'INVALID_SIGNATURE'))
})

test('queued retransmissions deduplicate on stable source keys; mixed invalid data yields partial', () => {
  let state = finishSyncRetry(beginSyncRetry(createOperationsState(), 6, actor), 6, 1)
  state = { ...state, syncBatches: state.syncBatches.map((batch) => batch.id === 2 ? { ...batch, status: 'FAILED' } : batch) }
  state = finishSyncRetry(beginSyncRetry(state, 2, actor), 2, 1)
  assert.equal(state.syncBatches[1].duplicateCount, 3)
  assert.equal(state.syncBatches[1].acceptedCount, 9)
  assert.equal(new Set(state.syncReceipts.map(({ sourceRecordKey }) => sourceRecordKey)).size, state.syncReceipts.length)
  const mixed = createOperationsState()
  mixed.syncBatches[5].records.push({ ...mixed.syncBatches[4].records[0], id: 6999 })
  const complete = finishSyncRetry(beginSyncRetry(mixed, 6, actor), 6, 1)
  assert.equal(complete.syncBatches[5].status, 'PARTIAL')
  assert.equal(complete.syncBatches[5].rejectedCount, 1)
  assert.equal(complete.syncReceipts.length, 3)
})

test('authorization, offline protection, invalid payloads and stale callbacks are enforced', () => {
  const state = createOperationsState()
  for (const activeRole of ['ADMINISTRATOR', 'FARM_OWNER', 'AGRICULTURAL_ENGINEER']) assert.throws(() => beginSyncRetry(state, 6, { ...actor, activeRole }), /Only operators/)
  assert.throws(() => beginSyncRetry(state, 999, actor), /Only failed/)
  assert.throws(() => beginSyncRetry(state, 3, actor), /Only failed/)
  state.gateways[0].status = 'OFFLINE'
  assert.throws(() => beginSyncRetry(state, 6, actor), /offline/)
  state.gateways[0].status = 'ONLINE'
  state.syncBatches[5].records[0].sourceRecordKey = 'invalid-key'
  const running = beginSyncRetry(state, 6, actor)
  assert.equal(finishSyncRetry(running, 6, 99), running)
  const completed = finishSyncRetry(running, 6, 1)
  assert.equal(completed.syncBatches[5].status, 'PARTIAL')
  assert.equal(completed.syncBatches[5].records[0].status, 'REJECTED')
})
