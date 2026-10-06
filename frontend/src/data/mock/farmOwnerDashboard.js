import { farms } from './farms.js'
import { zones as allZones } from './zones.js'
import { sensorNodes } from './sensorNodes.js'
import { sensorChannels } from './sensorChannels.js'
import { sensorTypes } from './sensorTypes.js'
import { sensorReadings } from './sensorReadings.js'
import { missions } from './missions.js'
import { alerts } from './alerts.js'
import { syncBatches } from './syncBatches.js'
import { MOCK_NOW } from './scenario.js'
import { getMissionDetails, getLatestReading } from './selectors.js'

export function formatSnapshotAge(timestamp) {
  if (!timestamp) return 'Not available'
  const minutes = Math.max(0, Math.floor((Date.parse(MOCK_NOW) - Date.parse(timestamp)) / 60000))
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`
  const hours = Math.floor(minutes / 60)
  return `${hours} hour${hours === 1 ? '' : 's'} ago`
}

// One farm-scoped view model. All counts and readings come from domain fixtures.
export function getFarmOwnerDashboard(farmId, workspace = { farms, zones: allZones }) {
  const farm = workspace.farms.find(({ id }) => id === farmId)
  if (!farm) return null
  const zones = workspace.zones.filter((zone) => zone.farmId === farmId)
  const nodes = sensorNodes.filter(({ zoneId }) => zones.some(({ id }) => id === zoneId))
  const nodeIds = new Set(nodes.map(({ id }) => id))
  const channels = sensorChannels.filter(({ sensorNodeId }) => nodeIds.has(sensorNodeId))
  const farmMissions = missions.filter((mission) => mission.farmId === farmId)
  const missionIds = new Set(farmMissions.map(({ id }) => id))
  const activeMissions = farmMissions.filter(({ status }) => status === 'IN_PROGRESS')
  const openAlerts = alerts.filter((alert) => alert.farmId === farmId && alert.status !== 'CLOSED')
  const importantAlerts = openAlerts.filter(({ severity }) => ['CRITICAL', 'WARNING'].includes(severity))
    .sort((a, b) => Number(b.severity === 'CRITICAL') - Number(a.severity === 'CRITICAL') || Date.parse(b.openedAt) - Date.parse(a.openedAt))
  const batches = syncBatches.filter(({ missionId }) => missionIds.has(missionId))
  const received = sensorReadings.filter((reading) => reading.receivedAt && channels.some(({ id }) => id === reading.sensorChannelId))
  const latestSyncAt = received.reduce((latest, reading) => !latest || reading.receivedAt > latest ? reading.receivedAt : latest, null)
  const environment = [1, 2, 3, 5].map((typeId) => {
    const type = sensorTypes.find(({ id }) => id === typeId)
    const matching = channels.filter((channel) => channel.sensorTypeId === typeId
      && nodes.find(({ id }) => id === channel.sensorNodeId).status === 'ONLINE')
    const readings = matching.map((channel) => getLatestReading(channel.id)).filter(Boolean)
    const average = (values) => values.length ? Number((values.reduce((sum, reading) => sum + reading.value, 0) / values.length).toFixed(type.precision)) : null
    const series = Array.from({ length: 13 }, (_, index) => {
      const to = new Date(Date.parse(MOCK_NOW) - (12 - index) * 3600000).toISOString()
      return { at: to, value: average(matching.map((channel) => getLatestReading(channel.id, { to })).filter(Boolean)) }
    })
    return { ...type, name: typeId === 2 ? 'Humidity' : type.name, value: average(readings), series,
      sensorCount: readings.length, attentionCount: importantAlerts.filter((alert) => matching.some(({ id }) => id === alert.sensorChannelId)).length }
  })
  const activity = [
    ...batches.filter(({ acceptedCount }) => acceptedCount > 0).map((batch) => ({
      id: `sync-${batch.id}`, title: 'Sensor data synchronized',
      description: `${batch.acceptedCount} readings received from ${farmMissions.find(({ id }) => id === batch.missionId).name}.`,
      at: batch.completedAt ?? batch.records.filter(({ status }) => status === 'ACCEPTED')
        .map(({ readingId }) => received.find(({ id }) => id === readingId)?.receivedAt)
        .filter(Boolean).sort().at(-1), icon: 'database',
    })),
    ...farmMissions.filter(({ status }) => status === 'COMPLETED').map((mission) => ({ id: `mission-${mission.id}`, title: 'Mission completed', description: mission.name, at: mission.completedAt, icon: 'mission' })),
    ...alerts.filter((alert) => alert.farmId === farmId && alert.acknowledgedAt).map((alert) => ({ id: `alert-${alert.id}`, title: 'Alert acknowledged', description: alert.title, at: alert.acknowledgedAt, icon: 'check' })),
  ].sort((a, b) => Date.parse(b.at) - Date.parse(a.at)).slice(0, 7)
  return { farm, zones, nodes, onlineSensors: nodes.filter(({ status }) => status === 'ONLINE').length,
    activeMissionCount: activeMissions.length, currentMission: activeMissions.length ? getMissionDetails(activeMissions[0].id) : null,
    openAlertCount: openAlerts.length, importantAlerts, environment, activity, latestSyncAt,
    health: importantAlerts.some(({ severity }) => severity === 'CRITICAL') ? 'Needs attention' : openAlerts.length ? 'Watch closely' : nodes.length ? 'Good' : 'No data' }
}
