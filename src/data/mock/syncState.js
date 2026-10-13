import { syncBatches } from './syncBatches.js'
import { sensorReadings } from './sensorReadings.js'
import { MOCK_NOW } from './scenario.js'
import { hasPermission, PERMISSIONS } from '../../config/permissions.js'

export const SYNC_STATUSES = ['PENDING', 'SYNCING', 'SUCCESS', 'PARTIAL', 'FAILED']
export function syncCounts(records) {
  return { recordCount: records.length, acceptedCount: records.filter(({ status }) => status === 'ACCEPTED').length,
    duplicateCount: records.filter(({ status }) => status === 'DUPLICATE').length, rejectedCount: records.filter(({ status }) => status === 'REJECTED').length,
    pendingCount: records.filter(({ status }) => ['PENDING', 'PROCESSING'].includes(status)).length }
}

export function createSyncState() {
  // An interrupted retransmission uses the SAME source keys as queued records.
  // They remain locally queued in batch 2; accepting this upload must not create copies.
  const interrupted = { id: 6, gatewayId: 1, missionId: 1, batchKey: 'GW_001:batch:6', status: 'FAILED',
    createdAt: '2026-10-06T01:17:00.000Z', startedAt: '2026-10-06T01:17:15.000Z', completedAt: '2026-10-06T01:17:30.000Z',
    description: 'Interrupted upload of three buffered readings. Queued copies in batch 2 retain the same source keys for duplicate detection.',
    records: syncBatches.find(({ id }) => id === 2).records.slice(0, 3).map((record, index) => ({ ...record, id: 6001 + index, status: 'PENDING' })),
    lastError: 'NETWORK_TIMEOUT: Internet connection dropped before the server acknowledged the upload.' }
  return {
    syncReceipts: [],
    syncBatches: [...syncBatches, interrupted].map((batch) => {
      const lastError = batch.lastError ?? batch.records.find(({ status }) => status === 'REJECTED')?.errorMessage ?? null
      return { ...structuredClone(batch), ...syncCounts(batch.records), code: `SYNC-2026-${String(batch.id).padStart(3, '0')}`, retryCount: 0, activeRetry: false, lastError,
        history: batch.startedAt ? [{ id: 0, startedAt: batch.startedAt, completedAt: batch.completedAt, status: batch.status, actorName: 'Gateway', error: lastError, ...syncCounts(batch.records) }] : [] }
    }),
  }
}

export function beginSyncRetry(state, batchId, actor) {
  if (!hasPermission(actor.activeRole, PERMISSIONS.SYNC_MANAGE)) throw new Error('Only operators can retry synchronization.')
  const batch = state.syncBatches.find(({ id }) => id === batchId)
  if (!batch || batch.status !== 'FAILED') throw new Error('Only failed batches can be retried.')
  const gateway = state.gateways.find(({ id }) => id === batch.gatewayId)
  if (!gateway || !['ONLINE', 'SYNCING'].includes(gateway.status)) throw new Error('The gateway is offline. Local records are retained until connectivity returns.')
  const retryCount = batch.retryCount + 1
  return { ...state, syncBatches: state.syncBatches.map((record) => record.id !== batchId ? record : {
    ...batch, retryCount, status: 'SYNCING', activeRetry: true, startedAt: MOCK_NOW, completedAt: null,
    history: [...batch.history, { id: retryCount, actorName: actor.fullName ?? 'Operator', startedAt: MOCK_NOW, completedAt: null, status: 'SYNCING', error: null }],
  }) }
}

// Completes an already-authorized mock job; stale/duplicate timer callbacks are no-ops.
export function finishSyncRetry(state, batchId, retryCount) {
  const batch = state.syncBatches.find(({ id }) => id === batchId)
  if (!batch || !batch.activeRetry || batch.status !== 'SYNCING' || batch.retryCount !== retryCount) return state
  const stored = new Set([...sensorReadings.filter(({ receivedAt }) => receivedAt).map(({ sourceRecordKey }) => sourceRecordKey), ...state.syncReceipts.map(({ sourceRecordKey }) => sourceRecordKey)])
  const receipts = [...state.syncReceipts]
  const records = batch.records.map((record) => {
    // Rejected payloads are never silently repaired by a retry.
    if (['REJECTED', 'ACCEPTED', 'DUPLICATE'].includes(record.status)) return record
    const reading = sensorReadings.find(({ id }) => id === record.readingId)
    if (!reading || !reading.isValid || reading.sourceRecordKey !== record.sourceRecordKey || reading.sensorChannelId !== record.sensorChannelId || reading.gatewayId !== batch.gatewayId || reading.missionId !== batch.missionId) {
      return { ...record, status: 'REJECTED', errorCode: 'INVALID_RECORD', errorMessage: 'Payload does not match a valid registered source reading.' }
    }
    if (stored.has(record.sourceRecordKey)) return { ...record, status: 'DUPLICATE', errorCode: null, errorMessage: null }
    stored.add(record.sourceRecordKey)
    receipts.push({ readingId: reading.id, sourceRecordKey: reading.sourceRecordKey, receivedAt: MOCK_NOW, batchId })
    return { ...record, status: 'ACCEPTED', errorCode: null, errorMessage: null }
  })
  const counts = syncCounts(records)
  const status = counts.recordCount && !counts.rejectedCount ? 'SUCCESS' : counts.acceptedCount + counts.duplicateCount > 0 ? 'PARTIAL' : 'FAILED'
  const lastError = counts.rejectedCount ? `${counts.rejectedCount} invalid record(s) remain rejected. ${records.find(({ status }) => status === 'REJECTED').errorMessage}` : null
  return { ...state, syncReceipts: receipts, syncBatches: state.syncBatches.map((record) => record.id !== batchId ? record : {
    ...batch, records, ...counts, status, activeRetry: false, completedAt: MOCK_NOW, lastError,
    history: batch.history.map((attempt) => attempt.id === retryCount ? { ...attempt, status, completedAt: MOCK_NOW, error: lastError, ...counts } : attempt),
  }) }
}

export function getSyncOverview(state, farmId, workspace) {
  const batches = state.syncBatches.filter((batch) => state.gateways.some((gateway) => gateway.id === batch.gatewayId && gateway.farmId === farmId)).map((batch) => ({ ...batch,
    gateway: state.gateways.find(({ id }) => id === batch.gatewayId), mission: state.missions.find(({ id }) => id === batch.missionId) }))
  const pendingKeys = new Set(batches.flatMap(({ records }) => records.filter(({ status }) => ['PENDING', 'PROCESSING'].includes(status)).map(({ sourceRecordKey }) => sourceRecordKey)))
  const acknowledged = new Set(state.syncReceipts.map(({ sourceRecordKey }) => sourceRecordKey))
  return { farm: workspace.farms.find(({ id }) => id === farmId), batches,
    counts: Object.fromEntries(SYNC_STATUSES.map((status) => [status, batches.filter((batch) => batch.status === status).length])),
    rejected: batches.reduce((sum, batch) => sum + batch.rejectedCount, 0), localQueueCount: pendingKeys.size,
    unacknowledgedCount: [...pendingKeys].filter((key) => !acknowledged.has(key)).length }
}
