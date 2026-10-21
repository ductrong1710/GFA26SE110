import { isTello, telloPlanErrors } from './telloPlanning.js'
export { isTello, makeTelloPoints, makeTelloWaypoints, TELLO_ACTIONS } from './telloPlanning.js'
export const MISSION_STATUSES = ['PENDING', 'SCHEDULED', 'RUNNING', 'COMPLETED', 'FAILED', 'CANCELLED']
export const WAYPOINT_ACTIONS = ['TAKEOFF', 'MOVE', 'COLLECT_SENSOR', 'WAIT', 'RETURN_HOME', 'LAND']
export const emptyMissionPlan = () => ({ missionId: null, flightAltitude: 1, name: '', farmId: '', sensorIds: [], points: [], waypoints: [], uavId: '', gatewayId: '', scheduledLocal: '', scheduledEndLocal: '', notes: '' })
const finite = value => value !== null && value !== '' && value !== undefined && Number.isFinite(Number(value))
export function groupCollectionPoints(nodes) {
  return nodes.filter(n => finite(n.latitude) && finite(n.longitude)).map((n, index) => ({ id: index + 1, name: n.name, latitude: n.latitude, longitude: n.longitude, sensorIds: [n.id] }))
}
export function makePlannedWaypoints(farm, points) {
  if (!farm || !finite(farm.latitude) || !finite(farm.longitude)) return []
  const row = (id, action, point = farm, pointId = '') => ({ id, coordinateType: 'GPS', x: point.latitude, y: point.longitude, altitude: action === 'LAND' ? 0 : 30, action, pointId })
  return [row(1, 'TAKEOFF'), ...points.map((p, i) => row(i + 2, 'COLLECT_SENSOR', p, p.id)), row(points.length + 2, 'RETURN_HOME'), row(points.length + 3, 'LAND')]
}
export function waypointPosition(w, farm) {
  if (!finite(w.x) || !finite(w.y)) return null
  if (w.coordinateType === 'GPS') return { latitude: Number(w.x), longitude: Number(w.y) }
  if (!farm || !finite(farm.latitude) || !finite(farm.longitude)) return null
  return { latitude: Number(farm.latitude) + Number(w.y) / 111320, longitude: Number(farm.longitude) + Number(w.x) / (111320 * Math.cos(farm.latitude * Math.PI / 180)) }
}
export function availableUavs(state, _farm, missionId) {
  return state.uavs.filter(u => u.isActive && !state.missions.some(m => m.id !== missionId && ['RUNNING', 'SCHEDULED'].includes(m.status) && m.uavId === u.id))
}
export function availableGateways(state, _farm, uavId, missionId) {
  return state.gateways.filter(g => g.isActive && (g.uavId === null || g.uavId === Number(uavId)) && !state.missions.some(m => m.id !== missionId && ['RUNNING', 'SCHEDULED'].includes(m.status) && m.gatewayId === g.id))
}
export function validateMissionPlan(plan, state, workspace) {
  const errors = Array.from({ length: 8 }, () => [])
  const tello = isTello(state.uavs.find(u => u.id === Number(plan.uavId)))
  if (tello) errors[3].push(...telloPlanErrors(plan))
  if (!workspace.farms.some(f => f.id === Number(plan.farmId))) errors[0].push('Select a farm.')
  if (!plan.sensorIds.length) errors[1].push('Select sensor targets.')
  if (plan.sensorIds.some(id => !state.sensorNodes.some(n => n.id === id && n.isActive && workspace.zones.some(z => z.id === n.zoneId && z.farmId === Number(plan.farmId))))) errors[1].push('Targets must be active sensors in this farm.')
  if (plan.sensorIds.some(id => plan.points.filter(p => p.sensorIds.includes(id)).length !== 1)) errors[2].push('Assign each sensor to one collection point.')
  if (tello && plan.points.some(p => !finite(p.x) || !finite(p.y))) errors[2].push('Enter measured forward/right coordinates for each Tello collection point.')
  if (!plan.waypoints.length) errors[3].push('Define waypoints.')
  for (const w of plan.waypoints) {
    if (tello && ['COLLECT', 'COLLECT_SENSOR'].includes(w.action) && !plan.points.some(p => p.id === Number(w.pointId))) errors[3].push('Select a collection point for each COLLECT waypoint.')
    if (!finite(w.x) || !finite(w.y) || !finite(w.altitude) || Number(w.altitude) < 0) errors[3].push('Waypoints require coordinates and nonnegative altitude.')
    if (w.coordinateType === 'GPS' && (Math.abs(Number(w.x)) > 90 || Math.abs(Number(w.y)) > 180)) errors[3].push('GPS coordinates are out of range.')
  }
  if (plan.points.some(p => !plan.waypoints.some(w => ['COLLECT_SENSOR', 'COLLECT'].includes(w.action) && Number(w.pointId) === p.id))) errors[4].push('Each collection point needs a collection waypoint.')
  if (!state.uavs.some(u => u.id === Number(plan.uavId) && u.isActive)) errors[5].push('Select an active UAV.')
  if (!availableGateways(state, plan.farmId, plan.uavId, plan.missionId).some(g => g.id === Number(plan.gatewayId))) errors[6].push('Select an active compatible gateway.')
  if (!plan.name.trim() || plan.name.length > 150) errors[7].push('Enter a name of at most 150 characters.')
  if (Boolean(plan.scheduledLocal) !== Boolean(plan.scheduledEndLocal)) errors[7].push('Enter both scheduled start and end times.')
  if (plan.scheduledLocal && plan.scheduledEndLocal <= plan.scheduledLocal) errors[7].push('Scheduled end must follow start.')
  return errors.map(e => [...new Set(e)])
}
export function missionPlanProgress(mission, state) {
  const targets = state.missionTargets.filter(t => t.missionId === mission.id)
  const totalTargets = mission.progress?.totalTargets ?? targets.length
  const collectedTargets = mission.progress?.successfulTargets ?? targets.filter(t => t.status === 'COLLECTED').length
  return { totalTargets, collectedTargets, totalWaypoints: state.missionWaypoints.filter(w => w.missionId === mission.id).length, percent: totalTargets ? collectedTargets / totalTargets * 100 : 0 }
}
export function planFromMission(m) {
  const local = at => at ? new Date(Date.parse(at) + 7 * 3600000).toISOString().slice(0, 16) : ''
  const points = m.waypoints.filter(w => m.targets.some(t => t.waypointId === w.id)).map(w => ({ id: w.id, name: `Waypoint ${w.sequenceNo}`, latitude: w.latitude, longitude: w.longitude, x: w.localX ?? '', y: w.localY ?? '', sensorIds: m.targets.filter(t => t.waypointId === w.id).map(t => t.sensorNodeId) }))
  return { ...emptyMissionPlan(), missionId: m.id, flightAltitude: m.waypoints[0]?.altitudeM ?? 1, name: m.name, farmId: m.farm.id, uavId: m.uav?.id ?? '', gatewayId: m.gateway?.id ?? '', sensorIds: m.targets.map(t => t.sensorNodeId), points, notes: m.operatorNotes ?? '', scheduledLocal: local(m.scheduledStartAt), scheduledEndLocal: local(m.scheduledEndAt), waypoints: m.waypoints.map(w => ({ id: w.id, coordinateType: finite(w.latitude) && finite(w.longitude) ? 'GPS' : 'RELATIVE', x: w.latitude ?? w.localX, y: w.longitude ?? w.localY, altitude: w.altitudeM, action: w.actionType ?? 'MOVE', pointId: points.some(p => p.id === w.id) ? w.id : '', holdSeconds: w.plannedHoldSeconds })) }
}
