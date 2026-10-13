import { listAll } from './resources.js'
import { validateAnalyticsRange, parseAnalyticsTime, ANALYTICS_DEFAULT_RANGE } from './analytics.js'

export const REPORT_DEFAULT_RANGE = ANALYTICS_DEFAULT_RANGE
const columns = keys => keys.map(([key, header]) => ({ key, header }))
export function reportRequest(filters) {
  const query = { farmId: Number(filters.farmId) }
  if (filters.type !== 'device') {
    const error = validateAnalyticsRange(filters)
    if (error) throw new Error(error)
    query.from = new Date(parseAnalyticsTime(filters.from)).toISOString()
    query.to = new Date(parseAnalyticsTime(filters.to)).toISOString()
    if (filters.zoneId && filters.type !== 'mission') query.zoneId = Number(filters.zoneId)
  }
  return query
}
const definitions = {
  sensor: ['sensor-data', [['sensorChannelId', 'Channel ID'], ['sensorNodeId', 'Sensor ID'], ['channelCode', 'Channel'], ['unit', 'Unit'], ['zoneId', 'Zone ID'], ['readingCount', 'Readings'], ['minimum', 'Minimum'], ['maximum', 'Maximum'], ['average', 'Average'], ['firstMeasuredAt', 'First Measurement'], ['lastMeasuredAt', 'Last Measurement']]],
  mission: ['missions', [['id', 'Mission ID'], ['name', 'Name'], ['status', 'Status'], ['totalTargets', 'Targets'], ['successfulTargets', 'Successful'], ['failedTargets', 'Failed'], ['skippedTargets', 'Skipped'], ['createdAt', 'Created'], ['startedAt', 'Started'], ['completedAt', 'Completed']]],
  alert: ['alerts', [['alertType', 'Type'], ['severity', 'Severity'], ['status', 'Status'], ['count', 'Alerts'], ['firstOpenedAt', 'First Opened'], ['lastOpenedAt', 'Last Opened']]],
  device: ['devices', [['deviceType', 'Type'], ['code', 'Code'], ['name', 'Name'], ['status', 'Status'], ['isActive', 'Active'], ['batteryPercent', 'Battery (%)'], ['lastSeenAt', 'Last Seen']]],
}
export function adaptReport(rows, filters, workspace, title) {
  const total = rows.reduce((sum, r) => sum + (filters.type === 'sensor' ? r.readingCount : filters.type === 'alert' ? r.count : 1), 0)
  return { type: filters.type, title, filters: { ...filters }, farm: workspace.farms.find(f => f.id === Number(filters.farmId))?.name, zone: workspace.zones.find(z => z.id === Number(filters.zoneId))?.name ?? 'All zones', highLevel: true, kpis: [['Records', total], ['Summary Rows', rows.length]], tables: [{ title, columns: columns(definitions[filters.type][1]), rows: rows.map((r, index) => ({ ...r, id: r.id ?? index })) }], notes: [filters.type === 'device' ? 'Latest device state from the backend. This report supports farm scope; date and zone filters are unavailable.' : filters.type === 'mission' ? 'Backend mission summaries support farm and date filters. Zone filters are unavailable.' : 'Backend summary for the selected farm, zone and date range. All API pages are included.'] }
}
export async function loadReport(filters, workspace, title, role) {
  const definition = definitions[filters.type]
  if (!definition) throw new Error('Choose a supported report type.')
  const rows = await listAll(`/api/reports/${definition[0]}`, reportRequest(filters))
  return { ...adaptReport(rows, filters, workspace, title), role, snapshotAt: new Date().toISOString() }
}
