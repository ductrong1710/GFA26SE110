import { compareZoneAnalytics, formatMetric } from '../../services/api/analytics'
import { ChartCard, DataTable } from './ui'
import EnvironmentalChart from './EnvironmentalChart'
import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from '../../services/api/useApiQuery'
import { api } from '../../services/api'
import { parseAnalyticsTime, validateAnalyticsRange } from '../../services/api/analytics'
import ApiState from './ApiState'

export default function ZoneComparison({ operations, workspace, filters }) {
  const data = compareZoneAnalytics(operations, workspace, filters)
  const type = (operations.sensorTypes ?? []).find(({ id }) => id === Number(filters.typeId))
  const { user, activeRole } = useAuth()
  const query = useApiQuery(`compare:${user?.id}:${activeRole}:${JSON.stringify(filters)}:${data.zones.map(z => z.id).join(',')}`, signal => api.get('/api/zones/compare', { zoneIds: data.zones.map(z => z.id).join(','), sensorTypeId: Number(filters.typeId), from: new Date(parseAnalyticsTime(filters.from)).toISOString(), to: new Date(parseAnalyticsTime(filters.to)).toISOString() }, signal), Boolean(type) && data.zones.length > 0 && !validateAnalyticsRange(filters))
  const summaries = query.data ? query.data.map(r => ({ id: r.zoneId, name: r.zoneName, count: r.readingCount, min: r.minimum, max: r.maximum, average: r.average })) : data.summaries
  return <ChartCard title="Zone Comparison" description={`${type?.name} · All zones in the selected farm, using the same date range. Zone and sensor filters apply to the other sections.`} caption="Missing sensor coverage is unavailable, not zero. Historical summaries include readings collected before a sensor went offline.">
    <ApiState {...query} />
    <EnvironmentalChart series={data.series} zones={data.zones} type={type} label="Zone comparison chart" />
    <DataTable caption="Zone comparison summary" rows={summaries} columns={[
      { key: 'name', header: 'Zone' }, ...['min', 'max', 'average'].map((key) => ({ key, header: `${key === 'average' ? 'Average' : key === 'min' ? 'Min' : 'Max'} (${type?.unit})`, render: (value) => formatMetric(value, type) })), { key: 'count', header: 'Readings' },
    ]} />
  </ChartCard>
}
