import { sensorChannels } from './sensorChannels.js'
import { sensorReadings } from './sensorReadings.js'
import { sensorTypes } from './sensorTypes.js'
import { missions } from './missions.js'
import { missionTargets } from './missionTargets.js'
import { collectionAttempts } from './collectionAttempts.js'
import { syncBatches } from './syncBatches.js'
import { getLatestReading, getMissionProgress } from './selectors.js'
import { sensorDisplayStatus } from './operationsState.js'

export function getOperationalSensor(node, workspace) {
  const zone = workspace.zones.find(({ id }) => id === node.zoneId)
  const channelIds = new Set(sensorChannels.filter(({ sensorNodeId }) => sensorNodeId === node.id).map(({ id }) => id))
  const lastCollectedAt = sensorReadings.filter(({ sensorChannelId }) => channelIds.has(sensorChannelId)).reduce((latest, reading) => !latest || reading.collectedAt > latest ? reading.collectedAt : latest, null)
  return { ...node, zone, farm: workspace.farms.find(({ id }) => id === zone?.farmId), displayStatus: sensorDisplayStatus(node), lastCollectedAt }
}

export function getOperationalSensorDetails(id, operations, workspace) {
  const node = operations.sensorNodes.find((sensor) => sensor.id === id)
  if (!node) return null
  const history = collectionAttempts.filter(({ missionTargetId }) => missionTargets.some((target) => target.id === missionTargetId && target.sensorNodeId === id))
    .map((attempt) => ({ ...attempt, mission: missions.find((mission) => mission.id === attempt.missionId), gateway: operations.gateways.find((gateway) => gateway.id === attempt.gatewayId) }))
    .sort((a, b) => b.startedAt.localeCompare(a.startedAt))
  return { ...getOperationalSensor(node, workspace), history,
    channels: sensorChannels.filter(({ sensorNodeId }) => sensorNodeId === id).map((channel) => ({ ...channel, type: sensorTypes.find(({ id: typeId }) => typeId === channel.sensorTypeId), latestReading: getLatestReading(channel.id, { includeUnsynced: true }) })) }
}

export function getGatewayLastSync(gatewayId) {
  return syncBatches.filter((batch) => batch.gatewayId === gatewayId && batch.completedAt && batch.acceptedCount > 0)
    .map(({ completedAt }) => completedAt).sort().at(-1) ?? null
}

export function getOperatorDashboard(farmId, operations, workspace) {
  const nodes = operations.sensorNodes.map((node) => getOperationalSensor(node, workspace)).filter(({ farm }) => farm?.id === farmId)
  const uavs = operations.uavs.filter((device) => device.farmId === farmId)
  const gateways = operations.gateways.filter((device) => device.farmId === farmId)
  const farmMissions = missions.filter((mission) => mission.farmId === farmId)
  const active = farmMissions.filter(({ status }) => status === 'IN_PROGRESS')
  const pending = syncBatches.filter((batch) => gateways.some(({ id }) => id === batch.gatewayId) && ['PENDING', 'SYNCING'].includes(batch.status))
  const failures = missionTargets.filter((target) => target.status === 'FAILED' && farmMissions.some(({ id }) => id === target.missionId))
    .map((target) => ({ ...target, sensor: operations.sensorNodes.find(({ id }) => id === target.sensorNodeId), mission: farmMissions.find(({ id }) => id === target.missionId),
      lastAttempt: collectionAttempts.filter(({ missionTargetId }) => missionTargetId === target.id).at(-1) }))
  return { nodes, uavs, gateways, pending, failures,
    availableUavs: uavs.filter(({ status }) => status === 'READY').length,
    onlineGateways: gateways.filter(({ status }) => ['ONLINE', 'SYNCING'].includes(status)).length,
    activeNodes: nodes.filter(({ isActive }) => isActive).length,
    readyMissions: farmMissions.filter(({ status }) => status === 'READY').length,
    activeMissions: active.length,
    mission: active.length ? { ...active[0], progress: getMissionProgress(active[0].id),
      uav: uavs.find(({ id }) => id === active[0].uavId), gateway: gateways.find(({ id }) => id === active[0].gatewayId) } : null,
    upcoming: farmMissions.filter(({ status }) => ['READY', 'SCHEDULED'].includes(status)).sort((a, b) => a.scheduledStartAt.localeCompare(b.scheduledStartAt)),
  }
}
