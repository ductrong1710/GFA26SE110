import { compareZoneAnalytics, compareSensorAnalytics, formatMetric } from '../../services/api/analytics'
import { ChartCard, DataTable } from './ui'
import EnvironmentalChart from './EnvironmentalChart'
import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from '../../services/api/useApiQuery'
import { api } from '../../services/api'
import { parseAnalyticsTime, validateAnalyticsRange } from '../../services/api/analytics'
import ApiState from './ApiState'

export default function ZoneComparison({ operations, workspace, filters }) {
  const type = (operations.sensorTypes ?? []).find(({ id }) => id === Number(filters.typeId))
  const { user, activeRole } = useAuth()
  const data = type?.unit?.trim() ? compareZoneAnalytics(operations, workspace, filters) : null
  const zoneIds = data?.zones.map(zone => zone.id) ?? []
  const query = useApiQuery(`compare:${user?.id}:${activeRole}:${JSON.stringify(filters)}:${zoneIds.join(',')}`, async signal => {
    const from = new Date(parseAnalyticsTime(filters.from)).toISOString()
    const to = new Date(parseAnalyticsTime(filters.to)).toISOString()
    const summaries = []
    // The comparison endpoint accepts up to 20 zones per request.
    for (let start = 0; start < zoneIds.length; start += 20) {
      const batch = zoneIds.slice(start, start + 20)
      const rows = await api.get('/api/zones/compare', { zoneIds: batch.join(','), sensorTypeId: type.id, from, to }, signal)
      summaries.push(...rows)
    }
    return summaries
  }, Boolean(type) && zoneIds.length > 0 && !validateAnalyticsRange(filters))
  if (!data) return <ChartCard title="Zone Comparison"><p role="alert">Only measurements of the same type and unit can be compared.</p></ChartCard>
  const summaries = query.data ? query.data.map(r => ({ id: r.zoneId, name: r.zoneName, count: r.readingCount, min: r.minimum, max: r.maximum, average: r.average })) : data.summaries
  return <ChartCard title="Zone Comparison" description={`${type?.name} · All zones in the selected farm, using the same date range. Zone and sensor filters apply to the other sections.`} caption="Missing sensor coverage is unavailable, not zero. Historical summaries include readings collected before a sensor went offline.">
    <ApiState {...query} />
    <EnvironmentalChart series={data.series} zones={data.zones} type={type} label="Zone comparison chart" />
    <DataTable caption="Zone comparison summary" rows={summaries} columns={[
      { key: 'name', header: 'Zone' }, ...['min', 'max', 'average'].map((key) => ({ key, header: `${key === 'average' ? 'Average' : key === 'min' ? 'Min' : 'Max'} (${type?.unit})`, render: (value) => formatMetric(value, type) })), { key: 'count', header: 'Readings' },
    ]} />
    <h3>Sensor comparison</h3>
    <p>Individual sensors in the selected zone or farm, using the same sensor type and time range.</p>
    <DataTable caption="Sensor comparison summary" rows={compareSensorAnalytics(operations, workspace, filters)} columns={[
      { key: 'name', header: 'Sensor' }, { key: 'code', header: 'Device code' },
      ...['min', 'max', 'average'].map(key => ({ key, header: `${key} (${type?.unit ?? ''})`, render: value => formatMetric(value, type) })),
      { key: 'count', header: 'Readings' },
    ]} />
  </ChartCard>
}
