import { compareZoneAnalytics, formatMetric, analyticsTypes } from '../../data/mock/sensorAnalytics'
import { ChartCard, DataTable } from './ui'
import EnvironmentalChart from './EnvironmentalChart'

export default function ZoneComparison({ operations, workspace, filters }) {
  const data = compareZoneAnalytics(operations, workspace, filters)
  const type = analyticsTypes.find(({ id }) => id === Number(filters.typeId))
  return <ChartCard title="Zone Comparison" description={`${type?.name} · All zones in the selected farm, using the same date range. Zone and sensor filters apply to the other sections.`} caption="Missing sensor coverage is unavailable, not zero. Historical summaries include readings collected before a sensor went offline.">
    <EnvironmentalChart series={data.series} zones={data.zones} type={type} label="Zone comparison chart" />
    <DataTable caption="Zone comparison summary" rows={data.summaries} columns={[
      { key: 'name', header: 'Zone' }, ...['min', 'max', 'average'].map((key) => ({ key, header: `${key === 'average' ? 'Average' : key === 'min' ? 'Min' : 'Max'} (${type?.unit})`, render: (value) => formatMetric(value, type) })), { key: 'count', header: 'Readings' },
    ]} />
  </ChartCard>
}
