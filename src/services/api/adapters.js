import { isTello, telloPlanErrors } from './telloPlanning.js'
// DTO adaptation is explicit: unavailable backend fields stay absent/null.
import { calculatePolygonArea } from '../../utils/geo.js'
export const farmView = x => ({ ...x, status: x.isActive ? 'ACTIVE' : 'INACTIVE', areaHectares: x.boundary ? calculatePolygonArea(x.boundary) / 10000 : null })
export const zoneView = x => ({ ...x, latitude: x.centerLatitude, longitude: x.centerLongitude, areaHectares: x.boundary ? calculatePolygonArea(x.boundary) / 10000 : null })
export const farmRecordRequest = (kind, values) => ({ name: values.name, description: values.description || null,
  ...(kind === 'farm' ? { latitude: nullableNumber(values.latitude), longitude: nullableNumber(values.longitude) }
    : { centerLatitude: nullableNumber(values.latitude), centerLongitude: nullableNumber(values.longitude) }),
  boundary: values.boundary ?? null })
export const nodeView = x => ({ ...x, displayStatus: x.isActive ? x.status : 'INACTIVE' })
export const gatewayView = x => ({ ...x, softwareVersion: x.firmwareVersion })
export const missionView = x => ({ ...x, farmId: x.farmId ?? x.farm?.id, uavId: x.uavId ?? x.uav?.id, gatewayId: x.gatewayId ?? x.gateway?.id, code: `#${x.id}` })
export const alertView = x => ({ ...x, title: x.message, type: x.alertType, createdAt: x.openedAt, history: (x.history ?? []).map(h => ({ ...h, occurredAt: h.createdAt })) })
export const nullableNumber = value => value === '' || value === undefined || value === null ? null : Number(value)
export function equipmentRequest(kind, values) {
  const common = { name: values.name, status: values.status || 'OFFLINE', batteryPercent: nullableNumber(values.batteryPercent), lastSeenAt: values.lastSeenAt || null }
  if (kind === 'sensor') return { ...common, zoneId: Number(values.zoneId), deviceCode: values.deviceCode, latitude: nullableNumber(values.latitude), longitude: nullableNumber(values.longitude), localX: nullableNumber(values.localX), localY: nullableNumber(values.localY) }
  if (kind === 'uav') return { ...common, code: values.code, model: values.model || null }
  return { ...common, code: values.code, gatewayType: values.gatewayType, uavId: nullableNumber(values.uavId), firmwareVersion: values.softwareVersion || null }
}
export function missionRequest(plan, uav) {
  const tello = isTello(uav)
  if (tello) { const errors = telloPlanErrors(plan); if (errors.length) throw new Error(errors.join(' ')) }
  const waypoints = plan.waypoints.map((w, index) => ({ sequenceNo: index + 1, latitude: w.coordinateType === 'GPS' ? nullableNumber(w.x) : null, longitude: w.coordinateType === 'GPS' ? nullableNumber(w.y) : null, localX: w.coordinateType !== 'GPS' ? nullableNumber(w.x) : null, localY: w.coordinateType !== 'GPS' ? nullableNumber(w.y) : null, altitudeM: nullableNumber(w.altitude), actionType: tello && w.action === 'COLLECT_SENSOR' ? 'COLLECT' : w.action, plannedHoldSeconds: nullableNumber(w.holdSeconds) }))
  const targetAssignments = plan.sensorIds.map((sensorNodeId, index) => {
    const point = plan.points.find(p => p.sensorIds.includes(sensorNodeId))
    const waypoint = plan.waypoints.findIndex(w => Number(w.pointId) === point?.id && ['COLLECT_SENSOR', 'COLLECT', 'COLLECT_DATA'].includes(w.action))
    if (waypoint < 0) throw new Error('Assign every sensor to a collection waypoint before saving.')
    return { sensorNodeId, sequenceNo: index + 1, waypointSequenceNo: waypoint + 1 }
  })
  const scheduledStartAt = plan.scheduledLocal ? new Date(`${plan.scheduledLocal}+07:00`).toISOString() : null
  const scheduledEndAt = plan.scheduledEndLocal ? new Date(`${plan.scheduledEndLocal}+07:00`).toISOString() : null
  if (Boolean(scheduledStartAt) !== Boolean(scheduledEndAt)) throw new Error('Enter both scheduled start and end times.')
  return { name: plan.name, farmId: Number(plan.farmId), uavId: nullableNumber(plan.uavId), gatewayId: nullableNumber(plan.gatewayId), scheduledStartAt, scheduledEndAt, sensorNodeIds: plan.sensorIds, targetAssignments, waypoints, operatorNotes: plan.notes || null }
}
