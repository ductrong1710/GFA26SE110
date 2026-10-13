// DTO adaptation is explicit: unavailable backend fields stay absent/null.
export const farmView = x => ({ ...x, status: x.isActive ? 'ACTIVE' : 'INACTIVE' })
export const zoneView = x => ({ ...x, latitude: x.centerLatitude, longitude: x.centerLongitude })
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
export function missionRequest(plan) {
  const waypoints = plan.waypoints.map((w, index) => ({ sequenceNo: index + 1, latitude: w.coordinateType === 'GPS' ? nullableNumber(w.x) : null, longitude: w.coordinateType === 'GPS' ? nullableNumber(w.y) : null, localX: w.coordinateType !== 'GPS' ? nullableNumber(w.x) : null, localY: w.coordinateType !== 'GPS' ? nullableNumber(w.y) : null, altitudeM: nullableNumber(w.altitude), actionType: w.action, plannedHoldSeconds: nullableNumber(w.holdSeconds) }))
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
