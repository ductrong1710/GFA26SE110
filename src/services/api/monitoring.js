import { missionPlanProgress } from './planning.js'
export const supportsTelemetry = () => true
export const allowedMissionStatuses = m => ({ PENDING: ['CANCELLED'], SCHEDULED: ['RUNNING', 'CANCELLED'], RUNNING: ['COMPLETED', 'FAILED', 'CANCELLED'] }[m.status] ?? [])
export function getMissionMonitoring(id, state, workspace) {
  const mission = state.missions.find(m => m.id === id)
  if (!mission) return null
  const farm = workspace.farms.find(f => f.id === mission.farmId)
  const targets = state.missionTargets.filter(t => t.missionId === id)
  const waypoints = state.missionWaypoints.filter(w => w.missionId === id).map(w => ({ ...w, status: 'UNKNOWN' }))
  const points = waypoints.filter(w => targets.some(t => t.waypointId === w.id)).map(w => ({ ...w, name: `Waypoint ${w.sequenceNo}`, sensorIds: targets.filter(t => t.waypointId === w.id).map(t => t.sensorNodeId) }))
  const rows = targets.map(t => {
    const attempts = state.collectionAttempts.filter(a => a.missionTargetId === t.id).map(a => ({ ...a, displayStatus: a.status }))
    const lastError = attempts.filter(a => a.errorCode || a.errorMessage).at(-1)
    return { ...t, node: state.sensorNodes.find(n => n.id === t.sensorNodeId), point: points.find(p => p.id === t.waypointId), attempts, records: attempts.reduce((sum, a) => sum + a.recordsReceived, 0), lastError, errorRecovered: Boolean(lastError && t.status === 'COLLECTED') }
  })
  const telemetry = state.latestTelemetry
  const progress = { ...missionPlanProgress(mission, state), completedWaypoints: null }
  return { mission: { ...mission, notes: mission.operatorNotes }, farm, uav: state.uavs.find(u => u.id === mission.uavId), gateway: state.gateways.find(g => g.id === mission.gatewayId), progress, rows, points, waypoints, telemetry, currentWaypoint: waypoints.find(w => w.sequenceNo === telemetry?.currentWaypointNo), elapsed: mission.startedAt && mission.completedAt ? `${Math.round((Date.parse(mission.completedAt) - Date.parse(mission.startedAt)) / 60000)} min` : 'Not available', battery: telemetry?.batteryPercent ?? null, successRate: state.collectionAttempts.filter(a => a.finishedAt).length ? state.collectionAttempts.filter(a => a.finishedAt && a.status === 'SUCCESS').length / state.collectionAttempts.filter(a => a.finishedAt).length * 100 : null, timeline: state.missionLogs.map(l => ({ id: l.id, title: l.logType, description: l.message, at: l.createdAt })), notes: [] }
}
