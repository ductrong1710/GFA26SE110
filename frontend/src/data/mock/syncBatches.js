import { sensorReadings } from './sensorReadings.js'

function recordsFor(missionId) {
  return sensorReadings.filter((reading) => reading.missionId === missionId).map((reading) => ({
    readingId: reading.id, sensorChannelId: reading.sensorChannelId,
    sourceRecordKey: reading.sourceRecordKey, status: 'ACCEPTED', errorCode: null, errorMessage: null,
  }))
}

const activeRecords = recordsFor(1)
const recoveryRecords = recordsFor(4)
const definitions = [
  {
    id: 1, gatewayId: 1, missionId: 1, status: 'SYNCING',
    createdAt: '2026-10-06T01:16:00.000Z', startedAt: '2026-10-06T01:16:00.000Z', completedAt: null,
    records: activeRecords.slice(0, 12).map((record, index) => ({ ...record, status: index < 6 ? 'ACCEPTED' : index === 6 ? 'PROCESSING' : 'PENDING' })),
  },
  {
    id: 2, gatewayId: 1, missionId: 1, status: 'PENDING',
    createdAt: '2026-10-06T01:17:00.000Z', startedAt: null, completedAt: null,
    records: activeRecords.slice(12).map((record) => ({ ...record, status: 'PENDING' })),
  },
  {
    id: 3, gatewayId: 1, missionId: 3, status: 'SUCCESS',
    createdAt: '2026-10-05T01:26:00.000Z', startedAt: '2026-10-05T01:26:00.000Z', completedAt: '2026-10-05T01:27:00.000Z',
    records: recordsFor(3),
  },
  {
    id: 4, gatewayId: 2, missionId: 4, status: 'PARTIAL',
    createdAt: '2026-10-06T00:26:00.000Z', startedAt: '2026-10-06T00:26:00.000Z', completedAt: '2026-10-06T00:27:00.000Z',
    records: [...recoveryRecords, { ...recoveryRecords[0], status: 'DUPLICATE' }, {
      readingId: null, sensorChannelId: 903, sourceRecordKey: 'gateway:2:invalid:903:1',
      status: 'REJECTED', errorCode: 'OUT_OF_RANGE', errorMessage: 'Soil moisture payload was outside the sensor range.',
    }],
  },
  {
    id: 5, gatewayId: 2, missionId: 5, status: 'FAILED',
    createdAt: '2026-10-05T23:25:00.000Z', startedAt: '2026-10-05T23:25:00.000Z', completedAt: '2026-10-05T23:26:00.000Z',
    records: recordsFor(5).map((record) => ({ ...record, status: 'REJECTED', errorCode: 'INVALID_SIGNATURE', errorMessage: 'Gateway payload signature could not be verified.' })),
  },
]

export const syncBatches = definitions.map((batch) => ({
  ...batch, batchKey: `GW_${String(batch.gatewayId).padStart(3, '0')}:batch:${batch.id}`,
  records: batch.records.map((record, index) => ({ ...record, id: batch.id * 1000 + index + 1 })),
  recordCount: batch.records.length,
  acceptedCount: batch.records.filter(({ status }) => status === 'ACCEPTED').length,
  duplicateCount: batch.records.filter(({ status }) => status === 'DUPLICATE').length,
  rejectedCount: batch.records.filter(({ status }) => status === 'REJECTED').length,
  pendingCount: batch.records.filter(({ status }) => ['PENDING', 'PROCESSING'].includes(status)).length,
}))
