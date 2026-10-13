export const SENSOR_PROTOCOLS = []
export const UAV_PLATFORMS = []
export const GATEWAY_TYPES = ['RASPBERRY_PI', 'ESP32', 'MOBILE_COMPUTER']
export function deviceAssignmentLocked(kind, id, missions = []) { return missions.some(m => ['RUNNING', 'SCHEDULED'].includes(m.status) && (kind === 'uav' ? m.uavId === id : m.gatewayId === id)) }
export function getOperationalSensor(node, workspace) {
  const zone = workspace.zones.find(z => z.id === node.zoneId)
  return { ...node, zone, farm: workspace.farms.find(f => f.id === zone?.farmId), displayStatus: node.isActive ? node.status : 'INACTIVE' }
}
export function getOperationalSensorDetails(id, state, workspace) {
  const node = state.sensorNodes.find(n => n.id === id)
  if (!node) return null
  return { ...getOperationalSensor(node, workspace), channels: state.sensorChannels.filter(c => c.sensorNodeId === id).map(c => ({ ...c, type: state.sensorTypes.find(t => t.id === c.sensorTypeId) ?? {}, latestReading: state.latestReadings.find(r => r.sensorChannelId === c.id)?.reading })), history: [] }
}
export function getGatewayLastSync() { return null }
