import { sensorChannels } from './sensorChannels.js'
import { sensorNodes } from './sensorNodes.js'
import { sensorTypes } from './sensorTypes.js'
import { collectionAttempts } from './collectionAttempts.js'
import { missionTargets } from './missionTargets.js'
import { offsetMinutes } from './scenario.js'

function valueFor(channel, measuredAt) {
  const hour = (new Date(measuredAt).getUTCHours() + 7) % 24
  const variation = Math.sin(channel.sensorNodeId + hour / 3)
  const values = {
    1: 28 + variation * 2, 2: 74 + variation * 6, 3: 43 + variation * 8,
    4: Math.max(0, Math.sin((hour - 6) * Math.PI / 12)) * 62000,
    5: 6.4 + variation * 0.3, 6: 105 + variation * 12,
  }
  if (measuredAt >= '2026-10-06T00:00:00.000Z') {
    if (channel.id === 501) values[1] = 36.8
    if (channel.id === 303) values[3] = 15.2
  }
  const type = sensorTypes.find(({ id }) => id === channel.sensorTypeId)
  return Number(values[type.id].toFixed(type.precision))
}

// Historical gateway exports, plus imported readings for the second farm.
const history = sensorChannels.flatMap((channel) => {
  const node = sensorNodes.find(({ id }) => id === channel.sensorNodeId)
  const collectedAt = node.id === 11 ? '2026-10-05T22:35:00.000Z' : '2026-10-06T00:35:00.000Z'
  return Array.from({ length: 12 }, (_, index) => {
    const measuredAt = offsetMinutes(collectedAt, (index - 11) * 60)
    return {
      id: channel.id * 100 + index + 1, sensorChannelId: channel.id,
      gatewayId: node.zoneId === 4 ? null : 2, missionId: null, collectionAttemptId: null,
      source: node.zoneId === 4 ? 'MANUAL_IMPORT' : 'GATEWAY_HISTORY',
      sourceRecordKey: `history:${channel.id}:${index + 1}`, value: valueFor(channel, measuredAt),
      measuredAt, collectedAt, receivedAt: offsetMinutes(collectedAt, 0.5), isValid: true,
    }
  })
})

const collected = collectionAttempts.filter(({ status }) => status === 'SUCCESS').flatMap((attempt) => {
  const target = missionTargets.find(({ id }) => id === attempt.missionTargetId)
  const receivedAt = attempt.missionId === 3 ? '2026-10-05T01:27:00.000Z'
    : attempt.missionId === 4 ? '2026-10-06T00:27:00.000Z'
      : attempt.missionId === 1 && target.sequenceNo <= 2 ? '2026-10-06T01:16:00.000Z' : null
  return sensorChannels.filter(({ sensorNodeId }) => sensorNodeId === target.sensorNodeId).map((channel) => ({
    id: 1_000_000 + attempt.id * 100 + channel.sensorTypeId,
    sensorChannelId: channel.id, gatewayId: attempt.gatewayId, missionId: attempt.missionId,
    collectionAttemptId: attempt.id, source: 'MISSION_COLLECTION',
    sourceRecordKey: `gateway:${attempt.gatewayId}:attempt:${attempt.id}:channel:${channel.id}`,
    value: valueFor(channel, attempt.startedAt), measuredAt: attempt.startedAt,
    collectedAt: attempt.finishedAt, receivedAt, isValid: true,
  }))
})

// receivedAt === null means buffered locally, not yet accepted by the server.
export const sensorReadings = [...history, ...collected]
