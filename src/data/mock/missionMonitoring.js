import { collectionAttempts } from './collectionAttempts.js'
import { missionWaypoints } from './missionWaypoints.js'
import { MOCK_NOW, offsetMinutes } from './scenario.js'
import { missionPlanProgress, waypointPosition, availableUavs, availableGateways, validateMissionPlan } from './missionPlanning.js'
import { hasPermission, PERMISSIONS } from '../../config/permissions.js'

const terminal = ['COMPLETED', 'PARTIAL', 'FAILED', 'CANCELLED']
export const supportsTelemetry = (uav) => Boolean(uav?.supports_telemetry ?? uav?.telemetrySupport)

export function createMissionMonitoring() {
  const previous = missionWaypoints.find(({ id }) => id === 106)
  const current = missionWaypoints.find(({ id }) => id === 107)
  return {
    missionEvents: [], nextMissionEventId: 1,
    // Recorded demo snapshot, not a simulated live stream or a flight command.
    missionTelemetry: [{ id: 1, missionId: 1, uavId: 1, currentWaypointId: 107, currentTargetId: 109,
      latitude: (previous.latitude + current.latitude) / 2, longitude: (previous.longitude + current.longitude) / 2,
      batteryPercent: 72, recordedAt: MOCK_NOW, collectionStartedAt: offsetMinutes(MOCK_NOW, -0.5) }],
  }
}

export function elapsedMissionTime(mission) {
  if (!mission.startedAt) return 'Not started'
  const seconds = Math.max(0, Math.floor((Date.parse(mission.completedAt ?? MOCK_NOW) - Date.parse(mission.startedAt)) / 1000))
  if (!Number.isFinite(seconds)) return 'Unavailable'
  return `${Math.floor(seconds / 3600) ? `${Math.floor(seconds / 3600)}h ` : ''}${Math.floor(seconds % 3600 / 60)}m ${seconds % 60}s`
}

export function getMissionMonitoring(id, state, workspace) {
  const mission = state.missions.find((record) => record.id === id)
  if (!mission) return null
  const farm = workspace.farms.find((record) => record.id === mission.farmId)
  const uav = state.uavs.find((record) => record.id === mission.uavId)
  const gateway = state.gateways.find((record) => record.id === mission.gatewayId)
  const recordedTelemetry = state.missionTelemetry?.filter((record) => record.missionId === id && record.uavId === mission.uavId).sort((a, b) => b.recordedAt.localeCompare(a.recordedAt))[0] ?? null
  const telemetry = mission.status === 'IN_PROGRESS' && supportsTelemetry(uav) ? recordedTelemetry : null
  const persistedWaypoints = state.missionWaypoints.filter((record) => record.missionId === id).sort((a, b) => a.sequenceNo - b.sequenceNo)
  const waypoints = mission.plan?.waypoints.map((waypoint, index) => {
    const recorded = persistedWaypoints.find(({ planWaypointId }) => planWaypointId === waypoint.id)
    return { ...waypoint, ...waypointPosition(waypoint, farm), id: recorded?.id ?? `plan-${waypoint.id}`, sequenceNo: index + 1, status: recorded?.status ?? 'PENDING', reachedAt: recorded?.reachedAt }
  }) ?? persistedWaypoints
  const targets = state.missionTargets.filter((record) => record.missionId === id)
  const points = mission.plan?.points.map((point) => ({ ...point, latitude: point.latitude === '' ? null : Number(point.latitude), longitude: point.longitude === '' ? null : Number(point.longitude) })) ?? [...new Set(targets.map(({ waypointId }) => waypointId))].map((waypointId) => {
    const waypoint = waypoints.find((record) => record.id === waypointId)
    return { id: waypointId, name: `Collection Point ${waypoint?.sequenceNo ?? '?'}`, latitude: waypoint?.latitude, longitude: waypoint?.longitude,
      sensorIds: targets.filter((target) => target.waypointId === waypointId).map(({ sensorNodeId }) => sensorNodeId) }
  })
  const rows = (mission.plan?.sensorIds ?? targets.map(({ sensorNodeId }) => sensorNodeId)).map((sensorNodeId) => {
    const target = targets.find((record) => record.sensorNodeId === sensorNodeId)
    const attempts = collectionAttempts.filter((attempt) => attempt.missionTargetId === target?.id && attempt.missionId === id)
      .sort((a, b) => a.attemptNo - b.attemptNo).map((attempt) => ({ ...attempt, displayStatus: attempt.errorCode === 'SENSOR_TIMEOUT' ? 'TIMEOUT' : attempt.status }))
    const lastError = attempts.filter(({ status }) => status !== 'SUCCESS').at(-1)
    const status = target?.status === 'COLLECTED' ? 'SUCCESS' : telemetry?.currentTargetId === target?.id && target ? 'IN_PROGRESS' : target?.status ?? 'PENDING'
    return { id: target?.id ?? `sensor-${sensorNodeId}`, node: state.sensorNodes.find((record) => record.id === sensorNodeId),
      point: points.find(({ sensorIds }) => sensorIds.includes(sensorNodeId)), attempts, records: attempts.reduce((total, attempt) => total + attempt.recordsReceived, 0),
      status, lastError, errorRecovered: status === 'SUCCESS' && Boolean(lastError) }
  })
  const attempts = rows.flatMap((row) => row.attempts)
  const finished = attempts.filter(({ finishedAt }) => finishedAt)
  const successRate = finished.length ? Math.round(finished.filter(({ status }) => status === 'SUCCESS').length / finished.length * 100) : null
  const events = []
  if (mission.startedAt) events.push({ id: 'start', at: mission.startedAt, title: 'Mission Started', description: mission.code, status: 'INFO' })
  for (const waypoint of waypoints.filter(({ reachedAt }) => reachedAt)) events.push({ id: `waypoint-${waypoint.id}`, at: waypoint.reachedAt, title: 'Waypoint Reached', description: `Waypoint ${waypoint.sequenceNo}`, status: 'SUCCESS' })
  for (const row of rows) for (const attempt of row.attempts) {
    if (attempt.attemptNo > 1) events.push({ id: `retry-${attempt.id}`, at: attempt.startedAt, title: 'Retry', description: `${row.node?.deviceCode} · Attempt ${attempt.attemptNo}`, status: 'WARNING' })
    events.push({ id: `attempt-start-${attempt.id}`, at: attempt.startedAt, title: 'Sensor Collection Started', description: `${row.node?.deviceCode} · Attempt ${attempt.attemptNo}`, status: 'INFO' })
    if (attempt.finishedAt) events.push({ id: `attempt-end-${attempt.id}`, at: attempt.finishedAt, title: attempt.status === 'SUCCESS' ? 'Collection Success' : 'Collection Failed', description: `${row.node?.deviceCode} · ${attempt.errorMessage ?? `${attempt.recordsReceived} records collected`}`, status: attempt.status === 'SUCCESS' ? 'SUCCESS' : 'FAILED' })
  }
  if (recordedTelemetry?.currentTargetId && recordedTelemetry.collectionStartedAt) events.push({ id: 'current-collection', at: recordedTelemetry.collectionStartedAt, title: 'Sensor Collection Started', description: rows.find((row) => row.id === recordedTelemetry.currentTargetId)?.node?.deviceCode, status: 'INFO' })
  const manualEvents = state.missionEvents?.filter((event) => event.missionId === id) ?? []
  if (mission.completedAt && !manualEvents.some((event) => event.toStatus === mission.status)) events.push({ id: 'end', at: mission.completedAt, title: `Mission ${mission.status.charAt(0)}${mission.status.slice(1).toLowerCase()}`, description: mission.failureReason ?? mission.notes, status: mission.status })
  for (const event of manualEvents) events.push({ ...event, id: `manual-${event.id}`, title: event.toStatus ? `Mission ${event.toStatus === 'IN_PROGRESS' ? 'Started' : event.toStatus.toLowerCase().replace(/^./, (letter) => letter.toUpperCase())} · Manual report` : 'Operational Note', description: event.note, status: event.toStatus ?? 'INFO', author: event.actorName })
  // A manual start already has its own audit event.
  const timeline = events.filter((event) => !(event.id === 'start' && manualEvents.some(({ toStatus }) => toStatus === 'IN_PROGRESS'))).sort((a, b) => Date.parse(a.at) - Date.parse(b.at))
  return { mission, farm, uav, gateway, telemetry, waypoints, points, rows, progress: missionPlanProgress(mission, state), successRate, timeline,
    elapsed: elapsedMissionTime(mission), currentWaypoint: telemetry ? waypoints.find((waypoint) => waypoint.id === telemetry.currentWaypointId) : null,
    battery: terminal.includes(mission.status) ? mission.batteryAtEndPercent : telemetry?.batteryPercent ?? uav?.batteryPercent,
    notes: manualEvents.filter(({ toStatus }) => !toStatus) }
}

export function allowedMissionStatuses(mission, state) {
  if (['READY', 'SCHEDULED'].includes(mission.status)) return ['IN_PROGRESS', 'CANCELLED']
  if (mission.status !== 'IN_PROGRESS') return []
  const progress = missionPlanProgress(mission, state)
  return [...(progress.totalTargets > 0 && progress.collectedTargets === progress.totalTargets && progress.totalWaypoints > 0 && progress.completedWaypoints === progress.totalWaypoints ? ['COMPLETED'] : []),
    ...(progress.collectedTargets > 0 && progress.collectedTargets < progress.totalTargets ? ['PARTIAL'] : []), 'FAILED', 'CANCELLED']
}

export function updateMissionMonitoring(state, { missionId, note, status }, actor, workspace) {
  if (!hasPermission(actor.activeRole, PERMISSIONS.MISSIONS_MANAGE)) throw new Error('Only operators can update mission monitoring records.')
  const mission = state.missions.find(({ id }) => id === missionId)
  if (!mission) throw new Error('Mission not found.')
  const text = String(note ?? '').trim()
  if (!text || text.length > 2000) throw new Error('Enter an operational note between 1 and 2000 characters.')
  if (status && !allowedMissionStatuses(mission, state).includes(status)) throw new Error('This status transition is not allowed by the recorded mission progress.')
  if (status === 'IN_PROGRESS') {
    if (mission.scheduledStartAt && Date.parse(mission.scheduledStartAt) > Date.parse(MOCK_NOW)) throw new Error('This mission is scheduled after the demo snapshot; it cannot be reported as started yet.')
    if (!availableUavs(state, mission.farmId).some(({ id }) => id === mission.uavId) || !availableGateways(state, mission.farmId, mission.uavId).some(({ id }) => id === mission.gatewayId)) throw new Error('Assigned equipment is unavailable or incompatible with this mission.')
    if (mission.plan && validateMissionPlan(mission.plan, state, workspace).flat().length) throw new Error('The saved plan is no longer valid. Review its sensor and route configuration.')
  }
  const event = { id: state.nextMissionEventId, missionId, actorId: actor.id, actorName: actor.fullName ?? 'Operator', at: MOCK_NOW, note: text, fromStatus: status ? mission.status : null, toStatus: status || null }
  const finished = terminal.includes(status)
  return { ...state, nextMissionEventId: state.nextMissionEventId + 1, missionEvents: [...state.missionEvents, event],
    missions: state.missions.map((record) => record.id !== missionId || !status ? record : { ...record, status,
      startedAt: status === 'IN_PROGRESS' ? MOCK_NOW : record.startedAt, completedAt: finished ? MOCK_NOW : record.completedAt,
      failureReason: ['FAILED', 'PARTIAL'].includes(status) ? text : record.failureReason }),
    // Closing a report leaves successes/errors intact and marks unvisited work skipped.
    missionTargets: state.missionTargets.map((target) => finished && target.missionId === missionId && ['PENDING', 'IN_PROGRESS'].includes(target.status) ? { ...target, status: 'SKIPPED' } : target),
    missionWaypoints: state.missionWaypoints.map((waypoint) => finished && waypoint.missionId === missionId && ['PENDING', 'IN_PROGRESS'].includes(waypoint.status) ? { ...waypoint, status: 'SKIPPED' } : waypoint),
  }
}
