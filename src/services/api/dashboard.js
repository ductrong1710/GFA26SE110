export function getAdminDashboard(workspace, management, operations) {
  const directory = management?.users ?? []
  const currentAlerts = management?.alerts ?? []
  const currentMissions = operations.missions ?? []
  const devices = [...operations.sensorNodes, ...operations.uavs, ...operations.gateways]
  const unresolved = currentAlerts.filter(({ status }) => status !== 'CLOSED')
  return {
    totalUsers: directory.length, totalSensors: operations.sensorNodes.length,
    offlineDevices: devices.filter(({ status, isActive }) => status === 'OFFLINE' && isActive !== false).length,
    openAlerts: unresolved.length, activeMissions: currentMissions.filter(({ status }) => status === 'RUNNING').length,
    pendingSync: (operations.syncBatches ?? []).filter(({ status }) => ['PENDING', 'SYNCING'].includes(status)).length,
    criticalAlerts: unresolved.filter(({ severity }) => severity === 'CRITICAL').sort((a, b) => b.openedAt.localeCompare(a.openedAt)),
    farms: workspace.farms.map((farm) => {
      const zones = workspace.zones.filter(({ farmId }) => farmId === farm.id)
      const nodes = operations.sensorNodes.filter(({ zoneId }) => zones.some(({ id }) => id === zoneId))
      return { ...farm, zoneCount: zones.length, sensorCount: nodes.length, onlineCount: nodes.filter(({ status, isActive }) => status === 'ONLINE' && isActive).length }
    }),
    deviceGroups: [['Sensor nodes', operations.sensorNodes.filter(({ isActive }) => isActive)], ['UAVs', operations.uavs], ['Gateways', operations.gateways]].map(([name, records]) => ({
      name, total: records.length, offline: records.filter(({ status }) => status !== 'ONLINE').length,
      lowBattery: records.filter(({ batteryPercent }) => batteryPercent != null && batteryPercent <= (management?.settings?.lowBatteryPercent ?? 20)).length,
    })),
    missions: [...currentMissions].sort((a, b) => Number(b.status === 'RUNNING') - Number(a.status === 'RUNNING') || (b.scheduledStartAt ?? '').localeCompare(a.scheduledStartAt ?? '')),
    activity: [
      ...(management?.activity ?? []).map((event) => ({ ...event, id: `management-${event.id}` })),
      ...(workspace.activity ?? []).map((event) => ({ ...event, id: `local-${event.id}` })),
      ...currentAlerts.filter(({ acknowledgedByUserId }) => acknowledgedByUserId).map((alert) => ({ id: `alert-${alert.id}`, userId: alert.acknowledgedByUserId, at: alert.acknowledgedAt, title: 'Acknowledged alert', description: alert.title })),
    ].sort((a, b) => Date.parse(b.at) - Date.parse(a.at))
      .map((event) => ({ ...event, userName: directory.find(({ id }) => id === event.userId)?.fullName ?? 'Unknown user' })).slice(0, 6),
  }
}

import { missionPlanProgress } from './planning.js'
export function formatSnapshotAge(timestamp) {
  if (!timestamp) return 'Not available'
  const minutes = Math.max(0, Math.floor((Date.now() - Date.parse(timestamp)) / 60000))
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`
  const hours = Math.floor(minutes / 60)
  return `${hours} hour${hours === 1 ? '' : 's'} ago`
}

// One farm-scoped view model. All counts and readings come from supplied API records.
export function getFarmOwnerDashboard(farmId, workspace, alertRecords, nodeRecords, operations) {
  const farm = workspace.farms.find(({ id }) => id === farmId)
  if (!farm) return null
  const zones = workspace.zones.filter((zone) => zone.farmId === farmId)
  const nodes = nodeRecords.filter(({ zoneId }) => zones.some(({ id }) => id === zoneId))
  const nodeIds = new Set(nodes.map(({ id }) => id))
  const channels = (operations.sensorChannels ?? []).filter(({ sensorNodeId }) => nodeIds.has(sensorNodeId))
  const farmMissions = (operations?.missions ?? []).filter((mission) => mission.farmId === farmId)
  const missionIds = new Set(farmMissions.map(({ id }) => id))
  const activeMissions = farmMissions.filter(({ status }) => status === 'RUNNING')
  const openAlerts = alertRecords.filter((alert) => alert.farmId === farmId && alert.status !== 'CLOSED')
  const importantAlerts = openAlerts.filter(({ severity }) => ['CRITICAL', 'WARNING'].includes(severity))
    .sort((a, b) => Number(b.severity === 'CRITICAL') - Number(a.severity === 'CRITICAL') || Date.parse(b.openedAt) - Date.parse(a.openedAt))
  const batches = (operations?.syncBatches ?? []).filter(({ missionId }) => missionIds.has(missionId))
  const received = (operations.readings ?? []).map((reading) => {
    const receipt = operations?.syncReceipts?.find(({ readingId }) => readingId === reading.id)
    return receipt ? { ...reading, receivedAt: receipt.receivedAt } : reading
  }).filter((reading) => reading.isValid && Number.isFinite(reading.value) && reading.receivedAt && channels.some(({ id }) => id === reading.sensorChannelId))
  const latestSyncAt = received.reduce((latest, reading) => !latest || reading.receivedAt > latest ? reading.receivedAt : latest, null)
  const latestReading = (id, to = new Date().toISOString()) => received.filter(r => r.sensorChannelId === id && r.measuredAt <= to).sort((a,b) => b.measuredAt.localeCompare(a.measuredAt))[0]
  const environment = (operations.sensorTypes ?? []).map((type) => {
    const typeId = type.id
    const matching = channels.filter((channel) => channel.sensorTypeId === typeId && channel.isActive
      && nodes.some((node) => node.id === channel.sensorNodeId && node.status === 'ONLINE' && node.isActive))
    const readings = matching.map((channel) => latestReading(channel.id)).filter(Boolean)
    const average = (values) => values.length ? Number((values.reduce((sum, reading) => sum + reading.value, 0) / values.length).toFixed(type.precision ?? 1)) : null
    const series = Array.from({ length: 13 }, (_, index) => {
      const to = new Date(Date.now() - (12 - index) * 3600000).toISOString()
      return { at: to, value: average(matching.map((channel) => latestReading(channel.id, to)).filter(Boolean)) }
    })
    return { ...type, name: type.name, value: average(readings), series,
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
    ...alertRecords.filter((alert) => alert.farmId === farmId && alert.acknowledgedAt).map((alert) => ({ id: `alert-${alert.id}`, title: 'Alert acknowledged', description: alert.title, at: alert.acknowledgedAt, icon: 'check' })),
  ].sort((a, b) => Date.parse(b.at) - Date.parse(a.at)).slice(0, 7)
  return { farm, zones, nodes, onlineSensors: nodes.filter(({ status, isActive }) => status === 'ONLINE' && isActive).length,
    activeMissionCount: activeMissions.length, currentMission: activeMissions.length ? operations ? {
      ...activeMissions[0], uav: operations.uavs.find(({ id }) => id === activeMissions[0].uavId), gateway: operations.gateways.find(({ id }) => id === activeMissions[0].gatewayId),
      progress: { ...missionPlanProgress(activeMissions[0], operations), batteryPercent: operations.uavs.find(({ id }) => id === activeMissions[0].uavId)?.batteryPercent },
      waypoints: operations.missionWaypoints.filter(({ missionId }) => missionId === activeMissions[0].id),
      targets: operations.missionTargets.filter(({ missionId }) => missionId === activeMissions[0].id).map((target) => ({ ...target, sensorNode: nodeRecords.find(({ id }) => id === target.sensorNodeId) })),
    } : null : null,
    openAlertCount: openAlerts.length, importantAlerts, environment, activity, latestSyncAt,
    health: importantAlerts.some(({ severity }) => severity === 'CRITICAL') ? 'Needs attention' : openAlerts.length ? 'Watch closely' : nodes.length ? 'Good' : 'No data' }
}
