import { missions } from './missions.js'

const fullRoute = [1, 2, 2, 3, 4, 4, 5, 6, 7, 8, 9, 10]
export const missionTargets = missions.flatMap((mission) => {
  const nodeIds = [1, 3].includes(mission.id) ? Array.from({ length: 12 }, (_, i) => i + 1)
    : mission.id === 4 ? [9, 10, 11, 12] : [5, 6, 7, 8]
  return nodeIds.map((sensorNodeId, index) => ({
    id: mission.id * 100 + sensorNodeId, missionId: mission.id, sensorNodeId,
    waypointId: mission.id * 100 + ([1, 3].includes(mission.id) ? fullRoute[index] : index + 1), sequenceNo: index + 1,
    status: mission.id === 1 ? index < 8 ? 'COLLECTED' : 'PENDING'
      : mission.id === 2 ? 'PENDING'
        : mission.id === 4 ? sensorNodeId === 11 ? 'FAILED' : 'COLLECTED'
          : mission.id === 5 ? index === 0 ? 'COLLECTED' : index === 1 ? 'FAILED' : 'SKIPPED'
            : 'COLLECTED',
  }))
})
