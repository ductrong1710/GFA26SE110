import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import { DataTable, SectionCard } from './ui'

export default function CropSeasonComparison({ ids, description }) {
  const params = new URLSearchParams(ids.map(id => ['ids', String(id)]))
  const path = `/api/crop-seasons/compare?${params}`
  const query = useApiQuery(path, signal => api.request(path, { signal }))
  return <SectionCard title="Season comparison" description={description}>
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    <DataTable caption="Selected season results" missingValue="No data recorded for this criterion." rows={query.data ?? []} loading={query.loading} columns={[
      { key: 'name', header: 'Season' }, { key: 'crop', header: 'Crop' }, { key: 'areaHectares', header: 'Area (ha)' },
      { key: 'startDate', header: 'Start' }, { key: 'endDate', header: 'End' }, { key: 'durationDays', header: 'Days' },
      { key: 'yieldKg', header: 'Yield (kg)' }, { key: 'totalCostVnd', header: 'Cost (VND)' }, { key: 'revenueVnd', header: 'Revenue (VND)' },
      { key: 'profitVnd', header: 'Profit (VND)' }, { key: 'activities', header: 'Cultivation activities',
        render: counts => counts.map(item => `${item.type} (${item.count})`).join(', ') || 'None recorded' },
    ]} />
  </SectionCard>
}
