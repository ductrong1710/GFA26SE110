import { missions } from './missions.js'
import { missionTargets } from './missionTargets.js'
import { sensorChannels } from './sensorChannels.js'
import { offsetMinutes } from './scenario.js'

export const collectionAttempts = missionTargets.filter(({ status }) => ['COLLECTED', 'FAILED'].includes(status)).flatMap((target) => {
  const mission = missions.find(({ id }) => id === target.missionId)
  const retry = (mission.id === 1 && target.sensorNodeId === 3) || (mission.id === 4 && target.sensorNodeId === 11)
  return Array.from({ length: retry ? 2 : 1 }, (_, index) => {
    const success = target.status === 'COLLECTED' && (!retry || index === 1)
    const start = offsetMinutes(mission.startedAt, (target.waypointId % 100) * 2 - 0.5 - (retry && index === 0 ? 0.5 : 0) + (target.sequenceNo % 2) * 0.1)
    const gatewayError = mission.id === 5 && !success
    return {
      id: target.id * 10 + index + 1, missionId: mission.id, missionTargetId: target.id,
      gatewayId: mission.gatewayId, attemptNo: index + 1, startedAt: start, finishedAt: offsetMinutes(start, 0.15),
      status: success ? 'SUCCESS' : 'FAILED',
      recordsReceived: success ? sensorChannels.filter(({ sensorNodeId }) => sensorNodeId === target.sensorNodeId).length : 0,
      errorCode: success ? null : gatewayError ? 'GATEWAY_BUFFER_ERROR' : 'SENSOR_TIMEOUT',
      errorMessage: success ? null : gatewayError ? 'Gateway cache could not accept the payload.' : 'No response within the collection window.',
    }
  })
})
