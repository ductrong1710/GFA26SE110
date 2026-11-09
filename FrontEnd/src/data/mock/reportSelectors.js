import { getReportTypes } from '../../config/reports.js'
import { ROLES } from '../../config/roles.js'
import { selectAnalytics, analyticsTypes, compareZoneAnalytics, validateAnalyticsRange, parseAnalyticsTime } from './sensorAnalytics.js'
import { collectionAttempts } from './collectionAttempts.js'
import { MOCK_NOW } from './scenario.js'

export const REPORT_DEFAULT_RANGE = { from: '2026-10-05T00:00', to: '2026-10-06T08:18' }
const groupCounts = (records, field) => [...new Set(records.map((record) => record[field]))].sort().map((value) => ({ id: value, name: value, count: records.filter((record) => record[field] === value).length }))
const table = (title, columns, rows) => ({ title, columns: columns.map(([key, header]) => ({ key, header })), rows })
const metricColumns = [['name', 'Sensor Type'], ['unit', 'Unit'], ['count', 'Reading Count'], ['min', 'Minimum'], ['max', 'Maximum'], ['average', 'Average']]
const rounded = (value, precision = 1) => value === null ? null : Number(value.toFixed(precision))

export function buildReport(role, filters, operations, workspace, management) {
  const type = getReportTypes(role).find(({ id }) => id === filters.type)
  if (!type) throw new Error('Your active role cannot view this report type.')
  const error = validateAnalyticsRange(filters)
  if (error) throw new Error(error)
  const farm = workspace.farms.find(({ id }) => id === Number(filters.farmId))
  if (!farm) throw new Error('Select an existing farm.')
  const zone = filters.zoneId ? workspace.zones.find(({ id, farmId }) => id === Number(filters.zoneId) && farmId === farm.id) : null
  if (filters.zoneId && !zone) throw new Error('Select a zone in the selected farm.')
  const inRange = (timestamp) => timestamp && Date.parse(timestamp) >= parseAnalyticsTime(filters.from) && Date.parse(timestamp) <= parseAnalyticsTime(filters.to)
  const farmZones = workspace.zones.filter(({ farmId }) => farmId === farm.id)
  const nodes = operations.sensorNodes.filter((node) => farmZones.some(({ id }) => id === node.zoneId) && (!zone || node.zoneId === zone.id))
  const nodeIds = new Set(nodes.map(({ id }) => id))
  const touchesZone = (mission) => !zone || operations.missionTargets.some((target) => target.missionId === mission.id && nodeIds.has(target.sensorNodeId)) || mission.plan?.sensorIds.some((id) => nodeIds.has(id))
  const missionsInFarm = operations.missions.filter((mission) => mission.farmId === farm.id && touchesZone(mission))
  const highLevel = role === ROLES.FARM_OWNER
  const report = { role, type: filters.type, title: type.name, filters: { ...filters }, farm: farm.name, zone: zone?.name ?? 'All zones', snapshotAt: MOCK_NOW, highLevel, kpis: [], tables: [], detail: null, notes: [] }
  if (filters.type === 'sensor') {
    const metric = analyticsTypes.find(({ id }) => id === Number(filters.metricId))
    if (!metric) throw new Error('Choose a chart metric.')
    const sets = analyticsTypes.map((type) => ({ type, data: selectAnalytics(operations, workspace, { ...filters, typeId: type.id, includeBuffered: false }) }))
    const rows = sets.flatMap(({ data }) => data.rows)
    const stats = sets.map(({ type, data }) => ({ id: type.id, name: type.name, unit: type.unit, count: data.stats.count, min: rounded(data.stats.min, type.precision), max: rounded(data.stats.max, type.precision), average: rounded(data.stats.average, type.precision) }))
    const selected = sets.find(({ type }) => type.id === metric.id).data
    const comparison = compareZoneAnalytics(operations, workspace, { ...filters, typeId: metric.id, includeBuffered: false })
    // Report comparisons honor the selected zone; use All zones for cross-zone reports.
    const comparisonZones = comparison.zones.filter((item) => !zone || item.id === zone.id)
    report.kpis = [['Reading Count', rows.length], ['Reporting Sensors', new Set(rows.map(({ node }) => node.id)).size], ['Sensor Types', stats.filter(({ count }) => count > 0).length]]
    report.tables.push(table('Sensor statistics', metricColumns, stats))
    report.chart = { type: metric, series: selected.series, threshold: selected.threshold, comparison: { ...comparison, zones: comparisonZones, summaries: comparison.summaries.filter(({ id }) => !zone || id === zone.id) } }
    report.tables.push(table(`Zone comparison · ${metric.name} (${metric.unit})`, [['name', 'Zone'], ['count', 'Readings'], ['min', 'Minimum'], ['max', 'Maximum'], ['average', 'Average']], report.chart.comparison.summaries.map((row) => ({ ...row, min: rounded(row.min, metric.precision), max: rounded(row.max, metric.precision), average: rounded(row.average, metric.precision) }))))
    if (!highLevel) report.detail = table('Sensor reading details', [['sensor', 'Sensor'], ['zone', 'Zone'], ['type', 'Sensor Type'], ['value', 'Value'], ['unit', 'Unit'], ['measuredAt', 'Measured At (UTC)']], rows.map((row) => ({ id: row.id, sensor: row.node.deviceCode, zone: row.zone.name, type: row.type.name, value: row.value, unit: row.type.unit, measuredAt: row.measuredAt })))
    report.notes.push('Date range applies to measurement time. Only synchronized, valid readings are included; mock sync receipts are respected.', 'Statistics keep sensor types and units separate. Charts use hourly channel means; tables summarize individual measurements. Missing values remain unavailable.')
  }
  if (filters.type === 'mission') {
    const missions = missionsInFarm.filter((mission) => inRange(mission.startedAt ?? mission.scheduledStartAt))
    const targets = operations.missionTargets.filter((target) => missions.some(({ id }) => id === target.missionId) && (!zone || nodeIds.has(target.sensorNodeId)))
    const attempts = collectionAttempts.filter((attempt) => targets.some(({ id }) => id === attempt.missionTargetId) && attempt.finishedAt && inRange(attempt.finishedAt))
    const rate = attempts.length ? Math.round(attempts.filter(({ status }) => status === 'SUCCESS').length / attempts.length * 100) : null
    report.kpis = [['Mission Count', missions.length], ['Completed', missions.filter(({ status }) => status === 'COMPLETED').length], ['Partial', missions.filter(({ status }) => status === 'PARTIAL').length], ['Failed', missions.filter(({ status }) => status === 'FAILED').length], ['Collection Success Rate', rate === null ? 'Unavailable' : `${rate}%`]]
    report.tables.push(table('Mission status summary', [['name', 'Status'], ['count', 'Missions']], groupCounts(missions, 'status')))
    if (!highLevel) report.detail = table('Mission details', [['code', 'Mission Code'], ['name', 'Name'], ['status', 'Status'], ['startedAt', 'Start / Scheduled (UTC)'], ['uav', 'UAV'], ['gateway', 'Gateway'], ['targets', 'Targets in scope'], ['collected', 'Collected']], missions.map((mission) => ({ id: mission.id, code: mission.code, name: mission.name, status: mission.status, startedAt: mission.startedAt ?? mission.scheduledStartAt, uav: operations.uavs.find(({ id }) => id === mission.uavId)?.code ?? 'Unassigned', gateway: operations.gateways.find(({ id }) => id === mission.gatewayId)?.code ?? 'Unassigned', targets: targets.filter(({ missionId }) => missionId === mission.id).length, collected: targets.filter((target) => target.missionId === mission.id && target.status === 'COLLECTED').length })))
    report.notes.push('Missions are selected by actual start time, falling back to scheduled time. Unscheduled drafts have no report date and are excluded. Status is the latest recorded status.', 'A zone selects missions with targets in that zone. Success rate is successful finished attempts / all finished attempts within the date and zone scope, including retries.')
  }
  if (filters.type === 'device') {
    const sensors = nodes.map((node) => ({ ...node, code: node.deviceCode, kind: 'Sensor', status: node.isActive ? node.status : 'INACTIVE' }))
    const devices = [['UAV', operations.uavs, 'uavId'], ['Gateway', operations.gateways, 'gatewayId']].flatMap(([kind, items, key]) => items.filter((item) => item.farmId === farm.id && (!zone || missionsInFarm.some((mission) => mission[key] === item.id))).map((item) => ({ ...item, kind })))
    const all = [...sensors, ...devices], selected = all.filter(({ lastSeenAt }) => inRange(lastSeenAt))
    report.kpis = [['Devices', selected.length], ['Offline', selected.filter(({ status }) => status === 'OFFLINE').length], ['Maintenance', selected.filter(({ status }) => status === 'MAINTENANCE').length], ['Excluded: No Last Seen', all.filter(({ lastSeenAt }) => !lastSeenAt).length]]
    for (const kind of ['Sensor', 'UAV', 'Gateway']) report.tables.push(table(`${kind} status`, [['name', 'Status'], ['count', 'Devices']], groupCounts(selected.filter((item) => item.kind === kind), 'status')))
    if (!highLevel) report.detail = table('Device details', [['code', 'Code'], ['name', 'Name'], ['kind', 'Device Type'], ['status', 'Status'], ['batteryPercent', 'Battery (%)'], ['lastSeenAt', 'Last Seen (UTC)']], selected.map((device) => ({ ...device, id: `${device.kind}-${device.id}` })))
    report.notes.push('Latest-known device status only; no historical status log exists. The date range filters Last Seen, not installation date. Never-seen devices are excluded and counted separately.', 'Sensor zone membership is direct. UAVs and gateways match a zone through recorded mission targets; farm-wide reports include all farm devices.')
  }
  if (filters.type === 'alert') {
    const environmental = role === ROLES.AGRICULTURAL_ENGINEER
    const alerts = management.alerts.filter((alert) => alert.farmId === farm.id && inRange(alert.openedAt)
      && (!environmental || ['SENSOR_THRESHOLD', 'SENSOR_DATA_TIMEOUT'].includes(alert.type))
      && (!zone || (alert.sensorNodeId ? nodeIds.has(alert.sensorNodeId) : alert.missionId && missionsInFarm.some(({ id }) => id === alert.missionId))))
    report.kpis = [['Alerts', alerts.length], ['Open', alerts.filter(({ status }) => status === 'OPEN').length], ['Acknowledged', alerts.filter(({ status }) => status === 'ACKNOWLEDGED').length], ['Closed', alerts.filter(({ status }) => status === 'CLOSED').length]]
    for (const [field, title] of [['severity', 'Alerts by severity'], ['type', 'Alerts by type'], ['status', 'Alert lifecycle']]) report.tables.push(table(title, [['name', title === 'Alerts by type' ? 'Type' : field === 'severity' ? 'Severity' : 'Status'], ['count', 'Alerts']], groupCounts(alerts, field)))
    if (!highLevel) report.detail = table('Alert details', [['title', 'Alert'], ['severity', 'Severity'], ['type', 'Type'], ['status', 'Status'], ['openedAt', 'Detected At (UTC)']], alerts.map((alert) => ({ ...alert })))
    report.notes.push('Date range filters detection time. Lifecycle status reflects the current local alert state, not its historical state at the range end.', environmental ? 'Engineer scope includes environmental threshold and sensor-data-timeout alerts only.' : 'A zone includes alerts linked to its sensors or to missions targeting that zone. Unlinked farm-level alerts appear only under All zones.')
  }
  return structuredClone(report)
}
