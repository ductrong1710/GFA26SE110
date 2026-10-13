import { missions } from './missions.js'
import { missionTargets } from './missionTargets.js'
import { missionWaypoints } from './missionWaypoints.js'
import { sensorNodes } from './sensorNodes.js'
import { farms } from './farms.js'
import { MOCK_NOW } from './scenario.js'
import { hasPermission, PERMISSIONS } from '../../config/permissions.js'

export const MISSION_STATUSES = ['DRAFT', 'SCHEDULED', 'READY', 'IN_PROGRESS', 'COMPLETED', 'PARTIAL', 'FAILED', 'CANCELLED']
export const WAYPOINT_ACTIONS = ['TAKEOFF', 'MOVE', 'COLLECT_SENSOR', 'WAIT', 'RETURN_HOME', 'LAND']
export const emptyMissionPlan = () => ({ missionId: null, name: '', farmId: '', sensorIds: [], points: [], waypoints: [], uavId: '', gatewayId: '', scheduledLocal: '', notes: '' })
const finite = (value) => String(value ?? '').trim() !== '' && Number.isFinite(Number(value))
const distance = (a, b) => Math.hypot((a.latitude - b.latitude) * 111320, (a.longitude - b.longitude) * 111320 * Math.cos(a.latitude * Math.PI / 180))

// Deterministic 100 m demo grouping, not flight or radio coverage certification.
export function groupCollectionPoints(nodes) {
  const groups = []
  for (const node of [...nodes].sort((a, b) => a.id - b.id)) {
    let group = groups.find((point) => distance(point, node) <= 100)
    if (!group) { group = { id: groups.length + 1, name: `Collection Point ${groups.length + 1}`, latitude: node.latitude, longitude: node.longitude, sensorIds: [] }; groups.push(group) }
    group.sensorIds.push(node.id)
    const members = nodes.filter(({ id }) => group.sensorIds.includes(id))
    group.latitude = Number((members.reduce((sum, item) => sum + item.latitude, 0) / members.length).toFixed(7))
    group.longitude = Number((members.reduce((sum, item) => sum + item.longitude, 0) / members.length).toFixed(7))
  }
  return groups
}

export function makePlannedWaypoints(farm, points) {
  if (!farm || !points.length) return []
  const row = (id, action, point = farm, pointId = '') => ({ id, coordinateType: 'GPS', x: point.latitude, y: point.longitude, altitude: action === 'LAND' ? 0 : 30, action, pointId })
  return [row(1, 'TAKEOFF'), ...points.map((point, index) => row(index + 2, 'COLLECT_SENSOR', point, point.id)), row(points.length + 2, 'RETURN_HOME'), row(points.length + 3, 'LAND')]
}

export function waypointPosition(waypoint, farm) {
  if (!farm || !finite(waypoint.x) || !finite(waypoint.y)) return null
  return waypoint.coordinateType === 'RELATIVE'
    ? { latitude: farm.latitude + Number(waypoint.y) / 111320, longitude: farm.longitude + Number(waypoint.x) / (111320 * Math.cos(farm.latitude * Math.PI / 180)) }
    : { latitude: Number(waypoint.x), longitude: Number(waypoint.y) }
}

export function availableUavs(operations, farmId) {
  return operations.uavs.filter((uav) => uav.farmId === Number(farmId) && ['READY', 'AVAILABLE'].includes(uav.status)
    && Number.isFinite(uav.batteryPercent) && uav.batteryPercent > 20
    && !(operations.missions ?? missions).some((mission) => mission.status === 'IN_PROGRESS' && mission.uavId === uav.id))
}

export function availableGateways(operations, farmId, uavId) {
  return operations.gateways.filter((gateway) => gateway.farmId === Number(farmId) && gateway.status === 'ONLINE'
    && (gateway.uavId === null || gateway.uavId === Number(uavId))
    && !(operations.missions ?? missions).some((mission) => mission.status === 'IN_PROGRESS' && mission.gatewayId === gateway.id))
}

export function validateMissionPlan(plan, operations, workspace) {
  const farm = workspace.farms.find(({ id }) => id === Number(plan.farmId))
  const nodes = operations.sensorNodes.filter(({ id }) => plan.sensorIds.includes(id))
  const errors = Array.from({ length: 8 }, () => [])
  if (!farm) errors[0].push('Select an existing farm.')
  if (!nodes.length || nodes.length !== plan.sensorIds.length || new Set(plan.sensorIds).size !== plan.sensorIds.length) errors[1].push('Select at least one valid sensor target.')
  if (nodes.some((node) => !node.isActive || workspace.zones.find(({ id }) => id === node.zoneId)?.farmId !== farm?.id)) errors[1].push('Targets must be enabled sensors in the selected farm.')
  const groupedIds = plan.points.flatMap(({ sensorIds }) => sensorIds)
  if (!plan.points.length || new Set(plan.points.map(({ id }) => id)).size !== plan.points.length
    || plan.points.some((point) => !point.sensorIds.length || !point.name.trim() || !finite(point.latitude) || Math.abs(Number(point.latitude)) > 90 || !finite(point.longitude) || Math.abs(Number(point.longitude)) > 180)
    || groupedIds.length !== plan.sensorIds.length || new Set(groupedIds).size !== groupedIds.length || groupedIds.some((id) => !plan.sensorIds.includes(id))) errors[2].push('Configure collection points with valid coordinates; each target must belong to exactly one point.')
  if (nodes.some((node) => { const point = plan.points.find(({ sensorIds }) => sensorIds.includes(node.id)); return point && distance(point, node) > 150 })) errors[2].push('Each target must be within 150 m of its collection point in this demo plan.')
  if (plan.waypoints.length < 4 || new Set(plan.waypoints.map(({ id }) => id)).size !== plan.waypoints.length) errors[3].push('Define a route with at least four uniquely identified waypoints.')
  for (const [index, waypoint] of plan.waypoints.entries()) {
    const gps = waypoint.coordinateType === 'GPS'
    const valid = ['GPS', 'RELATIVE'].includes(waypoint.coordinateType) && WAYPOINT_ACTIONS.includes(waypoint.action)
      && finite(waypoint.x) && finite(waypoint.y) && Math.abs(Number(waypoint.x)) <= (gps ? 90 : 5000) && Math.abs(Number(waypoint.y)) <= (gps ? 180 : 5000)
      && finite(waypoint.altitude) && Number(waypoint.altitude) >= 0 && Number(waypoint.altitude) <= 120
      && (waypoint.action === 'LAND' ? Number(waypoint.altitude) === 0 : Number(waypoint.altitude) > 0)
    if (!valid) errors[3].push(`Waypoint ${index + 1}: check action, coordinates, and altitude (demo range 0–120 m; LAND must be 0 m).`)
    if (waypoint.action === 'COLLECT_SENSOR' && !plan.points.some(({ id }) => id === Number(waypoint.pointId))) errors[3].push(`Waypoint ${index + 1}: select a collection point.`)
  }
  if (plan.waypoints[0]?.action !== 'TAKEOFF' || plan.waypoints.at(-1)?.action !== 'LAND'
    || plan.waypoints.filter(({ action }) => action === 'TAKEOFF').length !== 1 || plan.waypoints.filter(({ action }) => action === 'LAND').length !== 1
    || plan.waypoints.at(-2)?.action !== 'RETURN_HOME') errors[4].push('The route must begin with TAKEOFF and end with RETURN_HOME followed by LAND, with no intermediate takeoff or landing.')
  const visits = plan.waypoints.filter(({ action }) => action === 'COLLECT_SENSOR').map(({ pointId }) => Number(pointId))
  if (plan.points.some(({ id }) => !visits.includes(id))) errors[4].push('Every collection point must have a COLLECT_SENSOR waypoint.')
  if (farm) {
    for (const waypoint of plan.waypoints) {
      const position = waypointPosition(waypoint, farm)
      if (!position || Math.abs(position.latitude) > 90 || Math.abs(position.longitude) > 180 || distance(position, farm) > 5000) errors[4].push('Keep the route within the demo planning extent of 5 km from the farm center.')
      const point = plan.points.find(({ id }) => id === Number(waypoint.pointId))
      if (waypoint.action === 'COLLECT_SENSOR' && point && position && distance(position, point) > 30) errors[4].push('Collection waypoints must be within 30 m of their assigned point.')
      if (['TAKEOFF', 'RETURN_HOME', 'LAND'].includes(waypoint.action) && position && distance(position, farm) > 30) errors[4].push('Takeoff, return-home, and landing points must be within 30 m of the farm center.')
    }
  }
  const uav = availableUavs(operations, plan.farmId).find(({ id }) => id === Number(plan.uavId))
  if (!uav) errors[5].push('Select an available UAV in this farm with more than 20% battery.')
  if (uav && plan.waypoints.some(({ coordinateType }) => coordinateType === 'GPS') && !uav.gpsSupport) errors[5].push('GPS waypoints require a UAV with GPS support.')
  if (!availableGateways(operations, plan.farmId, plan.uavId).some(({ id }) => id === Number(plan.gatewayId))) errors[6].push('Select an online gateway that is unassigned or paired with the selected UAV.')
  if (!plan.name.trim() || plan.name.trim().length > 120) errors[7].push('Enter a mission name between 1 and 120 characters.')
  if (plan.scheduledLocal) {
    const date = Date.parse(`${plan.scheduledLocal}:00+07:00`)
    if (!Number.isFinite(date) || date < Date.parse(MOCK_NOW)) errors[7].push('Schedule must be valid and no earlier than the demo snapshot (Vietnam time).')
  }
  return errors.map((items) => [...new Set(items)])
}

export function createMissionPlanning() {
  const extra = ['DRAFT', 'SCHEDULED', 'CANCELLED'].map((status, index) => {
    const id = index + 6
    const points = groupCollectionPoints(sensorNodes.filter((node) => [9, 10].includes(node.id)))
    const plan = { ...emptyMissionPlan(), missionId: id, name: ['Afternoon Irrigation Survey', 'Next Day Irrigation Check', 'Cancelled Evening Survey'][index], farmId: 1, sensorIds: [9, 10], points: status === 'DRAFT' ? [] : points,
      waypoints: status === 'DRAFT' ? [] : makePlannedWaypoints(farms.find(({ id }) => id === 1), points), uavId: status === 'DRAFT' ? '' : 2, gatewayId: status === 'DRAFT' ? '' : 2,
      scheduledLocal: status === 'SCHEDULED' ? '2026-10-07T09:00' : '' }
    return { id, code: `MSN-2026-${String(id).padStart(3, '0')}`, name: plan.name, farmId: 1, uavId: plan.uavId || null, gatewayId: plan.gatewayId || null, createdByUserId: 2, operatorUserId: 2, status,
      scheduledStartAt: status === 'SCHEDULED' ? '2026-10-07T02:00:00.000Z' : null, startedAt: null, completedAt: null, notes: '', plan }
  })
  const state = { missions: [...missions.map((mission) => ({ ...mission })), ...extra], missionTargets: missionTargets.map((target) => ({ ...target })), missionWaypoints: missionWaypoints.map((waypoint) => ({ ...waypoint })),
    nextMissionId: 9, nextTargetId: Math.max(...missionTargets.map(({ id }) => id)) + 1, nextWaypointId: Math.max(...missionWaypoints.map(({ id }) => id)) + 1 }
  for (const mission of extra.filter(({ status }) => status !== 'DRAFT')) {
    const linked = linkPlan(mission.plan, mission.id, state, farms.find(({ id }) => id === mission.farmId))
    state.missionTargets.push(...linked.targets)
    state.missionWaypoints.push(...linked.waypoints)
    state.nextTargetId = linked.nextTargetId
    state.nextWaypointId = linked.nextWaypointId
  }
  return state
}

function linkPlan(plan, id, state, farm) {
  let nextWaypointId = state.nextWaypointId
  let nextTargetId = state.nextTargetId
  const waypoints = plan.waypoints.map((waypoint, index) => ({ id: nextWaypointId++, missionId: id, sequenceNo: index + 1, ...waypointPosition(waypoint, farm), altitudeMeters: Number(waypoint.altitude), actionType: waypoint.action, status: 'PENDING', planWaypointId: waypoint.id }))
  const targets = plan.sensorIds.map((sensorNodeId, index) => {
    const point = plan.points.find(({ sensorIds }) => sensorIds.includes(sensorNodeId))
    const planned = plan.waypoints.find((waypoint) => waypoint.action === 'COLLECT_SENSOR' && Number(waypoint.pointId) === point.id)
    return { id: nextTargetId++, missionId: id, sensorNodeId, sequenceNo: index + 1, waypointId: waypoints.find(({ planWaypointId }) => planWaypointId === planned.id).id, status: 'PENDING' }
  })
  return { targets, waypoints, nextTargetId, nextWaypointId }
}

export function missionPlanProgress(mission, state) {
  const targets = state.missionTargets.filter(({ missionId }) => missionId === mission.id)
  const waypoints = state.missionWaypoints.filter(({ missionId }) => missionId === mission.id)
  const totalTargets = mission.plan?.sensorIds.length ?? targets.length
  const totalWaypoints = mission.plan?.waypoints.length ?? waypoints.length
  const collectedTargets = targets.filter(({ status }) => status === 'COLLECTED').length
  return { totalTargets, collectedTargets, totalWaypoints, completedWaypoints: waypoints.filter(({ status }) => status === 'COMPLETED').length, percent: totalTargets ? Math.round(collectedTargets / totalTargets * 100) : 0 }
}

export function saveMissionPlan(state, plan, workspace, actor, draft = false) {
  if (!hasPermission(actor.activeRole, PERMISSIONS.MISSIONS_CREATE)) throw new Error('Your role cannot create missions.')
  const existing = state.missions.find(({ id }) => id === plan.missionId)
  if (plan.missionId && (!existing || existing.status !== 'DRAFT')) throw new Error('Only draft missions can be edited in the planning wizard.')
  if (!draft) {
    const errors = validateMissionPlan(plan, state, workspace).flat()
    if (errors.length) throw new Error(errors.join(' '))
  }
  const id = existing?.id ?? state.nextMissionId
  const snapshot = structuredClone({ ...plan, missionId: id })
  const record = { ...existing, id, code: existing?.code ?? `MSN-2026-${String(id).padStart(3, '0')}`, name: plan.name.trim() || 'Untitled Mission', farmId: Number(plan.farmId) || null,
    uavId: Number(plan.uavId) || null, gatewayId: Number(plan.gatewayId) || null, status: draft ? 'DRAFT' : 'READY', createdByUserId: existing?.createdByUserId ?? actor.id, operatorUserId: actor.id,
    scheduledStartAt: plan.scheduledLocal && Number.isFinite(Date.parse(`${plan.scheduledLocal}:00+07:00`)) ? new Date(`${plan.scheduledLocal}:00+07:00`).toISOString() : null,
    startedAt: null, completedAt: null, notes: plan.notes, plan: snapshot }
  const { targets, waypoints, nextTargetId, nextWaypointId } = draft ? { targets: [], waypoints: [], nextTargetId: state.nextTargetId, nextWaypointId: state.nextWaypointId }
    : linkPlan(plan, id, state, workspace.farms.find((farm) => farm.id === Number(plan.farmId)))
  return { ...state, missions: existing ? state.missions.map((mission) => mission.id === id ? record : mission) : [...state.missions, record],
    missionWaypoints: [...state.missionWaypoints.filter((waypoint) => waypoint.missionId !== id), ...waypoints], missionTargets: [...state.missionTargets.filter((target) => target.missionId !== id), ...targets],
    nextMissionId: existing ? state.nextMissionId : id + 1, nextWaypointId, nextTargetId }
}
