import { sensorReadings } from './sensorReadings.js'
import { sensorChannels } from './sensorChannels.js'
import { sensorTypes } from './sensorTypes.js'
import { MOCK_NOW } from './scenario.js'

export const ANALYTICS_DEFAULT_RANGE = { from: '2026-10-05T20:18', to: '2026-10-06T08:18' }
export const analyticsTypes = sensorTypes
export const parseAnalyticsTime = (value) => /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value) ? Date.parse(`${value}:00+07:00`) : NaN
export function validateAnalyticsRange({ from, to }) {
  const start = parseAnalyticsTime(from), end = parseAnalyticsTime(to)
  if (!Number.isFinite(start) || !Number.isFinite(end)) return 'Choose valid start and end times (Vietnam time).'
  if (start > end) return 'Start time must be before or equal to end time.'
  if (end - start > 31 * 86400000) return 'Choose a date range of 31 days or less.'
  return ''
}
export const formatMetric = (value, type) => value === null || value === undefined ? 'Unavailable' : Number(value).toLocaleString('en-US', { maximumFractionDigits: type?.precision ?? 1 })
const average = (values) => values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : null

export function analyticsReadings(operations, workspace) {
  const receipts = new Map((operations.syncReceipts ?? []).map((receipt) => [receipt.readingId, receipt.receivedAt]))
  const nodes = new Map(operations.sensorNodes.map((node) => [node.id, node]))
  const channels = new Map(sensorChannels.map((channel) => [channel.id, channel]))
  return sensorReadings.filter(({ isValid, value }) => isValid && Number.isFinite(value)).map((reading) => {
    const channel = channels.get(reading.sensorChannelId), node = nodes.get(channel?.sensorNodeId)
    const zone = workspace.zones.find(({ id }) => id === node?.zoneId)
    return { ...reading, receivedAt: receipts.get(reading.id) ?? reading.receivedAt, channel, node, zone,
      stale: channel ? Date.parse(MOCK_NOW) - Date.parse(reading.measuredAt) > channel.dataTimeoutMinutes * 60000 : true,
      type: sensorTypes.find(({ id }) => id === channel?.sensorTypeId), farmId: zone?.farmId }
  }).filter(({ channel, node, zone, type }) => channel && node && zone && type)
}

export function selectAnalytics(operations, workspace, filters) {
  const error = validateAnalyticsRange(filters)
  const zones = workspace.zones.filter(({ farmId }) => farmId === Number(filters.farmId))
  const nodes = operations.sensorNodes.filter((node) => zones.some(({ id }) => id === node.zoneId) && (!filters.zoneId || node.zoneId === Number(filters.zoneId)))
  const start = parseAnalyticsTime(filters.from), end = parseAnalyticsTime(filters.to)
  const rows = error ? [] : analyticsReadings(operations, workspace).filter((reading) => reading.farmId === Number(filters.farmId)
    && (!filters.zoneId || reading.zone.id === Number(filters.zoneId)) && (!filters.sensorId || reading.node.id === Number(filters.sensorId))
    && (!filters.typeId || reading.type.id === Number(filters.typeId)) && (filters.includeBuffered || reading.receivedAt)
    && Date.parse(reading.measuredAt) >= start && Date.parse(reading.measuredAt) <= end).sort((a, b) => b.measuredAt.localeCompare(a.measuredAt) || b.id - a.id)
  const latest = [...new Map([...rows].reverse().map((row) => [row.sensorChannelId, row])).values()]
  const values = rows.map(({ value }) => value)
  const stats = { min: values.length ? Math.min(...values) : null, max: values.length ? Math.max(...values) : null, average: average(values), count: values.length }
  const channels = [...new Map(rows.map(({ channel }) => [channel.id, channel])).values()]
  const bounds = [...new Set(channels.map(({ warningMin, warningMax }) => `${warningMin}:${warningMax}`))]
  const threshold = bounds.length === 1 && channels.length ? { min: channels[0].warningMin, max: channels[0].warningMax } : null
  return { error, rows, latest, stats, threshold, channels, zones, nodes, type: sensorTypes.find(({ id }) => id === Number(filters.typeId)), series: hourlySeries(rows, filters) }
}

export function hourlySeries(rows, range) {
  if (validateAnalyticsRange(range)) return []
  const start = Math.floor(parseAnalyticsTime(range.from) / 3600000) * 3600000, end = parseAnalyticsTime(range.to)
  const buckets = new Map()
  for (const row of rows) {
    const hour = Math.floor(Date.parse(row.measuredAt) / 3600000) * 3600000
    if (!buckets.has(hour)) buckets.set(hour, new Map())
    const channels = buckets.get(hour)
    if (!channels.has(row.sensorChannelId)) channels.set(row.sensorChannelId, [])
    channels.get(row.sensorChannelId).push(row.value)
  }
  return Array.from({ length: Math.floor((end - start) / 3600000) + 1 }, (_, index) => {
    const at = start + index * 3600000, channels = buckets.get(at)
    return { at, value: channels ? average([...channels.values()].map(average)) : null, channels: channels?.size ?? 0 }
  })
}

export function engineerOverview(operations, workspace, alerts, farmId, zoneId = '') {
  const filters = { ...ANALYTICS_DEFAULT_RANGE, farmId, zoneId }
  const base = selectAnalytics(operations, workspace, filters)
  const metrics = sensorTypes.map((type) => {
    const data = selectAnalytics(operations, workspace, { ...filters, typeId: type.id })
    const fresh = data.latest.filter((row) => row.node.isActive && row.node.status === 'ONLINE' && row.channel.isActive && Date.parse(MOCK_NOW) - Date.parse(row.measuredAt) <= row.channel.dataTimeoutMinutes * 60000)
    return { ...type, ...data, currentAverage: average(fresh.map(({ value }) => value)), contributing: fresh.length }
  })
  const health = base.nodes.map((node) => {
    const latest = base.latest.filter((row) => row.node.id === node.id).sort((a, b) => b.measuredAt.localeCompare(a.measuredAt))[0]
    const stale = !latest || Date.parse(MOCK_NOW) - Date.parse(latest.measuredAt) > latest.channel.dataTimeoutMinutes * 60000
    return { ...node, lastReadingAt: latest?.measuredAt, reason: !node.isActive ? 'Disabled' : node.status !== 'ONLINE' ? 'Offline' : stale ? 'No recent synchronized reading' : null }
  }).filter(({ reason }) => reason)
  return { metrics, health, activeNodes: base.nodes.filter(({ isActive, status }) => isActive && status === 'ONLINE').length,
    environmentalAlerts: alerts.filter((alert) => alert.type === 'SENSOR_THRESHOLD' && alert.status !== 'CLOSED' && alert.farmId === Number(farmId) && base.nodes.some(({ id }) => id === alert.sensorNodeId)) }
}

export function compareZoneAnalytics(operations, workspace, filters) {
  const zones = workspace.zones.filter(({ farmId }) => farmId === Number(filters.farmId))
  const data = zones.map((zone) => ({ zone, ...selectAnalytics(operations, workspace, { ...filters, zoneId: zone.id, sensorId: '' }) }))
  return { zones, summaries: data.map((item) => ({ id: item.zone.id, name: item.zone.name, ...item.stats })),
    series: (data[0]?.series ?? []).map((bucket, index) => Object.fromEntries([['at', bucket.at], ...data.map((item) => [`zone${item.zone.id}`, item.series[index]?.value ?? null])])) }
}
