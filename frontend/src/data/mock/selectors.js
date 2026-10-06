import { farms } from './farms.js'
import { zones } from './zones.js'
import { sensorNodes } from './sensorNodes.js'
import { sensorChannels } from './sensorChannels.js'
import { sensorTypes } from './sensorTypes.js'
import { sensorReadings } from './sensorReadings.js'
import { missions } from './missions.js'
import { missionTargets } from './missionTargets.js'
import { missionWaypoints } from './missionWaypoints.js'
import { collectionAttempts } from './collectionAttempts.js'
import { uavs } from './uavs.js'
import { gateways } from './gateways.js'
import { users } from './users.js'
import { alerts } from './alerts.js'
import { notifications } from './notifications.js'
import { getAlertScope } from '../../config/permissions.js'

export const getZonesByFarmId = (farmId) => zones.filter((zone) => zone.farmId === farmId)

export function getSensorNodesByFarmId(farmId) {
  const zoneIds = new Set(getZonesByFarmId(farmId).map(({ id }) => id))
  return sensorNodes.filter(({ zoneId }) => zoneIds.has(zoneId))
}

// Chronological, accepted readings by default. Opt in to locally buffered data.
export function getReadingsForChannel(channelId, { from, to, includeUnsynced = false } = {}) {
  return sensorReadings.filter((reading) => reading.sensorChannelId === channelId
    && (includeUnsynced || reading.receivedAt !== null)
    && (!from || Date.parse(reading.measuredAt) >= Date.parse(from))
    && (!to || Date.parse(reading.measuredAt) <= Date.parse(to)))
    .sort((a, b) => Date.parse(a.measuredAt) - Date.parse(b.measuredAt) || a.id - b.id)
}

export function getLatestReading(channelId, options) {
  return getReadingsForChannel(channelId, options).at(-1) ?? null
}

export function getSensorNodeDetails(nodeId, readingOptions) {
  const node = sensorNodes.find(({ id }) => id === nodeId)
  if (!node) return null
  const zone = zones.find(({ id }) => id === node.zoneId)
  return {
    ...node, zone, farm: farms.find(({ id }) => id === zone.farmId),
    channels: sensorChannels.filter(({ sensorNodeId }) => sensorNodeId === nodeId).map((channel) => ({
      ...channel, type: sensorTypes.find(({ id }) => id === channel.sensorTypeId),
      latestReading: getLatestReading(channel.id, readingOptions),
    })),
  }
}

export function getMissionProgress(missionId) {
  const mission = missions.find(({ id }) => id === missionId)
  if (!mission) return null
  const waypoints = missionWaypoints.filter((waypoint) => waypoint.missionId === missionId)
  const targets = missionTargets.filter((target) => target.missionId === missionId)
  const completedWaypoints = waypoints.filter(({ status }) => status === 'COMPLETED').length
  const collectedTargets = targets.filter(({ status }) => status === 'COLLECTED').length
  return {
    completedWaypoints, totalWaypoints: waypoints.length, collectedTargets, totalTargets: targets.length,
    waypointPercent: waypoints.length ? Math.round(completedWaypoints / waypoints.length * 100) : 0,
    collectionPercent: targets.length ? Math.round(collectedTargets / targets.length * 100) : 0,
    batteryPercent: mission.batteryAtEndPercent ?? uavs.find(({ id }) => id === mission.uavId).batteryPercent,
  }
}

export function getMissionDetails(missionId) {
  const mission = missions.find(({ id }) => id === missionId)
  if (!mission) return null
  return {
    ...mission, farm: farms.find(({ id }) => id === mission.farmId),
    uav: uavs.find(({ id }) => id === mission.uavId), gateway: gateways.find(({ id }) => id === mission.gatewayId),
    operator: users.find(({ id }) => id === mission.operatorUserId), progress: getMissionProgress(missionId),
    waypoints: missionWaypoints.filter((waypoint) => waypoint.missionId === missionId),
    targets: missionTargets.filter((target) => target.missionId === missionId).map((target) => ({
      ...target, sensorNode: sensorNodes.find(({ id }) => id === target.sensorNodeId),
      attempts: collectionAttempts.filter(({ missionTargetId }) => missionTargetId === target.id),
    })),
  }
}

// Demo filtering only; a future backend must enforce authorization independently.
export function getAlertsForUser(userId, activeRole) {
  const user = users.find(({ id }) => id === userId)
  if (!user) return []
  const scope = getAlertScope(activeRole ?? user.activeRole)
  if (!scope) return []
  return alerts.filter((alert) => {
    if (!user.farmIds.includes(alert.farmId)) return false
    if (scope === 'all') return true
    if (alert.missionId) return missions.find(({ id }) => id === alert.missionId)?.operatorUserId === userId
    if (alert.gatewayId) return gateways.find(({ id }) => id === alert.gatewayId)?.assignedOperatorUserId === userId
    if (alert.uavId) return uavs.find(({ id }) => id === alert.uavId)?.assignedOperatorUserId === userId
    return missionTargets.some((target) => target.sensorNodeId === alert.sensorNodeId
      && missions.some((mission) => mission.id === target.missionId && mission.operatorUserId === userId))
  })
}

export function getNotificationsForUser(userId, { unreadOnly = false } = {}) {
  return notifications.filter((notification) => notification.userId === userId && (!unreadOnly || !notification.readAt))
    .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt) || b.id - a.id)
}
