import { sensorNodes } from './sensorNodes.js'
import { sensorReadings } from './sensorReadings.js'
import { sensorChannels } from './sensorChannels.js'
import { sensorTypes } from './sensorTypes.js'
import { gateways } from './gateways.js'
import { uavs } from './uavs.js'
import { missions } from './missions.js'

export const DEMO_TIMEZONE = 'Asia/Ho_Chi_Minh'
export function localDate(timestamp) {
  if (!timestamp) return ''
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: DEMO_TIMEZONE, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(timestamp))
  const value = (type) => parts.find((part) => part.type === type).value
  return `${value('year')}-${value('month')}-${value('day')}`
}
export function displayTime(timestamp) {
  return timestamp ? new Intl.DateTimeFormat('en-GB', { timeZone: DEMO_TIMEZONE, day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(timestamp)) : 'Not recorded'
}
export function getAlertDetails(alert, workspace, operations = { sensorNodes, gateways, uavs }) {
  const sensor = operations.sensorNodes.find(({ id }) => id === alert.sensorNodeId)
  const channel = sensorChannels.find(({ id }) => id === alert.sensorChannelId)
  const type = sensorTypes.find(({ id }) => id === channel?.sensorTypeId)
  const device = sensor ?? operations.gateways.find(({ id }) => id === alert.gatewayId) ?? operations.uavs.find(({ id }) => id === alert.uavId)
  return { ...alert, zone: workspace.zones.find(({ id }) => id === sensor?.zoneId),
    farm: workspace.farms.find(({ id }) => id === alert.farmId), deviceName: device?.deviceCode ?? device?.code ?? 'Not device-specific',
    reading: sensorReadings.find(({ id }) => id === alert.readingId),
    mission: missions.find(({ id }) => id === alert.missionId),
    unit: type?.unit ?? (alert.type === 'SENSOR_LOW_BATTERY' ? '%' : ''),
    timeoutMinutes: alert.type === 'SENSOR_DATA_TIMEOUT' ? sensorChannels.find(({ sensorNodeId }) => sensorNodeId === sensor?.id)?.dataTimeoutMinutes : null }
}
export function filterAlerts(records, filters) {
  return records.filter((alert) => (!filters.severity || alert.severity === filters.severity)
    && (!filters.type || alert.type === filters.type) && (!filters.status || alert.status === filters.status)
    && (!filters.farm || alert.farmId === Number(filters.farm)) && (!filters.zone || alert.zone?.id === Number(filters.zone))
    && (!filters.date || localDate(alert.openedAt) === filters.date))
}
