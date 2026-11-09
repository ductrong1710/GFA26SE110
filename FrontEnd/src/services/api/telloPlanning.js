export const isTello = uav => /tello/i.test(uav?.model ?? '')
export const TELLO_ACTIONS = ['MOVE', 'HOLD', 'COLLECT']
const finite = value => value !== null && value !== undefined && String(value).trim() !== '' && Number.isFinite(Number(value))
const centimeters = value => finite(value) && Math.abs(Number(value) * 100 - Math.round(Number(value) * 100)) < 1e-7
export function telloPlanErrors(plan) {
  const errors = []
  if (plan.waypoints.length > 16) errors.push('Tello supports at most 16 waypoints.')
  if (plan.sensorIds.length > 8) errors.push('Tello supports at most 8 sensor targets.')
  if (new Set(plan.sensorIds).size !== plan.sensorIds.length) errors.push('Sensor targets must be unique.')
  let previous = { x: 0, y: 0 }
  let steps = plan.waypoints.length * 2
  for (const [index, w] of [...plan.waypoints, { x: 0, y: 0 }].entries()) {
    if (index < plan.waypoints.length) {
      if (w.coordinateType !== 'RELATIVE') errors.push('Tello requires local RELATIVE coordinates; GPS conversion is not supported.')
      if (![...TELLO_ACTIONS, 'COLLECT_SENSOR'].includes(w.action)) errors.push('Tello actions are MOVE, HOLD, and COLLECT. Firmware handles takeoff, return to origin, and landing.')
      if (![w.x, w.y].every(v => centimeters(v) && Math.abs(Number(v)) <= 100)) errors.push('Tello coordinates must be meters in 0.01 m increments within ±100 m.')
      if (!finite(w.altitude) || Number(w.altitude) < 0.8 || Number(w.altitude) > 1.2 || !centimeters(w.altitude)) errors.push('Tello altitude must be 0.8–1.2 m in 0.01 m increments.')
      if (!finite(w.holdSeconds ?? 0) || Number(w.holdSeconds ?? 0) < 0 || Number(w.holdSeconds ?? 0) > 30 || !Number.isInteger(Number(w.holdSeconds ?? 0))) errors.push('Tello hold duration must be a whole number of seconds from 0 to 30.')
    }
    for (const axis of ['x', 'y']) {
      const delta = Math.abs(Math.round(Number(w[axis]) * 100) - Math.round(Number(previous[axis]) * 100))
      steps += Math.ceil(delta / 100)
      if (delta > 0 && delta < 20) errors.push(`Tello leg ${index + 1}: each nonzero axis move must be at least 20 cm, including return to origin.`)
    }
    previous = w
  }
  if (steps > 128) errors.push('Tello route exceeds 128 steps including return to origin.')
  return [...new Set(errors)]
}
export function makeTelloPoints(nodes) {
  // Deliberately blank: sensor GPS and stored local positions do not establish this takeoff frame.
  return nodes.map((node, index) => ({ id: index + 1, name: node.name || node.deviceCode, x: '', y: '', sensorIds: [node.id] }))
}
export function makeTelloWaypoints(points, altitude = 1) {
  return points.map((point, index) => ({ id: index + 1, coordinateType: 'RELATIVE', x: point.x, y: point.y, altitude, holdSeconds: 0, action: 'COLLECT', pointId: point.id }))
}
