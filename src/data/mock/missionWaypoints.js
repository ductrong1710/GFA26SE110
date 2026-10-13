import { missions } from './missions.js'
import { farms } from './farms.js'
import { offsetMinutes } from './scenario.js'

export const missionWaypoints = missions.flatMap((mission) => {
  const farm = farms.find(({ id }) => id === mission.farmId)
  const count = [1, 3].includes(mission.id) ? 10 : 6
  return Array.from({ length: count }, (_, index) => {
    const sequenceNo = index + 1
    const status = mission.id === 2 ? 'PENDING'
      : mission.id === 1 ? sequenceNo <= 6 ? 'COMPLETED' : 'PENDING'
        : mission.id === 5 ? sequenceNo === 1 ? 'COMPLETED' : sequenceNo === 2 ? 'FAILED' : 'SKIPPED'
          : 'COMPLETED'
    return {
      id: mission.id * 100 + sequenceNo, missionId: mission.id, sequenceNo,
      latitude: Number((farm.latitude + Math.sin(sequenceNo) * 0.001).toFixed(7)),
      longitude: Number((farm.longitude + Math.cos(sequenceNo) * 0.001).toFixed(7)),
      altitudeMeters: 30, actionType: 'COLLECT', plannedHoldSeconds: 30, status,
      reachedAt: status === 'COMPLETED' ? offsetMinutes(mission.startedAt, sequenceNo * 2) : null,
    }
  })
})
