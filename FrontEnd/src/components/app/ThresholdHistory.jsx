import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { displayTime } from '../../utils/display'
import ApiState from './ApiState'
import { DataTable } from './ui'

export default function ThresholdHistory({ channel }) {
  const [page, setPage] = useState(1)
  const query = useApiQuery(`threshold-history:${channel.id}:${page}`, signal =>
    api.request(`/api/sensor-channels/${channel.id}/threshold/history`, { query: { page }, signal }))
  return <section aria-label="Threshold change history"><h3>Threshold history · {channel.channelCode}</h3>
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    <DataTable caption="Recorded threshold settings" rows={query.data?.items ?? []} loading={query.loading} columns={[
      { key: 'changedAt', header: 'Recorded', render: displayTime },
      { key: 'changedByUserId', header: 'Changed by', render: id => id == null ? 'Legacy configuration' : `User #${id}` },
      { key: 'minValue', header: 'Minimum' }, { key: 'maxValue', header: 'Maximum' },
      { key: 'dataTimeoutMinutes', header: 'Timeout (min)' }, { key: 'lowBatteryPercent', header: 'Low battery (%)' },
      { key: 'isEnabled', header: 'Rule', render: enabled => enabled ? 'Enabled' : 'Disabled' },
    ]} />
    <button type="button" disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Newer changes</button>
    <button type="button" disabled={query.loading || page >= (query.data?.pagination?.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Older changes</button>
  </section>
}
