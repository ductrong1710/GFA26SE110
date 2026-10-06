import { users } from './users.js'
import { sensorNodes } from './sensorNodes.js'
import { uavs } from './uavs.js'
import { gateways } from './gateways.js'
import { missions } from './missions.js'
import { syncBatches } from './syncBatches.js'
import { alerts } from './alerts.js'

export function getAdminDashboard(workspace) {
  const devices = [...sensorNodes, ...uavs, ...gateways]
  const unresolved = alerts.filter(({ status }) => status !== 'CLOSED')
  return {
    totalUsers: users.length, totalSensors: sensorNodes.length,
    offlineDevices: devices.filter(({ status }) => status === 'OFFLINE').length,
    openAlerts: unresolved.length, activeMissions: missions.filter(({ status }) => status === 'IN_PROGRESS').length,
    pendingSync: syncBatches.filter(({ status }) => ['PENDING', 'SYNCING'].includes(status)).length,
    criticalAlerts: unresolved.filter(({ severity }) => severity === 'CRITICAL').sort((a, b) => b.openedAt.localeCompare(a.openedAt)),
    farms: workspace.farms.map((farm) => {
      const zones = workspace.zones.filter(({ farmId }) => farmId === farm.id)
      const nodes = sensorNodes.filter(({ zoneId }) => zones.some(({ id }) => id === zoneId))
      return { ...farm, zoneCount: zones.length, sensorCount: nodes.length, onlineCount: nodes.filter(({ status }) => status === 'ONLINE').length }
    }),
    deviceGroups: [['Sensor nodes', sensorNodes], ['UAVs', uavs], ['Gateways', gateways]].map(([name, records]) => ({
      name, total: records.length, offline: records.filter(({ status }) => status === 'OFFLINE').length,
      lowBattery: records.filter(({ batteryPercent }) => batteryPercent <= 20).length,
    })),
    missions: [...missions].sort((a, b) => Number(b.status === 'IN_PROGRESS') - Number(a.status === 'IN_PROGRESS') || b.scheduledStartAt.localeCompare(a.scheduledStartAt)),
    activity: [
      ...workspace.activity.map((event) => ({ ...event, id: `local-${event.id}` })),
      ...alerts.filter(({ acknowledgedByUserId }) => acknowledgedByUserId).map((alert) => ({ id: `alert-${alert.id}`, userId: alert.acknowledgedByUserId, at: alert.acknowledgedAt, title: 'Acknowledged alert', description: alert.title })),
    ].sort((a, b) => Date.parse(b.at) - Date.parse(a.at))
      .map((event) => ({ ...event, userName: users.find(({ id }) => id === event.userId)?.fullName ?? 'Demo administrator' })).slice(0, 6),
  }
}
