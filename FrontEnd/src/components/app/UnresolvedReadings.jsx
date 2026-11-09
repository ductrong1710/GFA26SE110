import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { api } from '../../services/api/index'
import { useApiQuery } from '../../services/api/useApiQuery'
import { SectionCard, DataTable } from './ui'
import ApiState from './ApiState'
import { displayTime } from '../../utils/display'

export default function UnresolvedReadings({ nodeId }) {
  const { user, activeRole } = useAuth()
  const [page, setPage] = useState(1)
  const query = useApiQuery(`unresolved:${user.id}:${activeRole}:${nodeId}:${page}`,
    signal => api.get(`/api/sensor-nodes/${nodeId}/unresolved-records`, { page, pageSize: 20 }, signal))
  return <SectionCard title="Readings needing review" description="Original records with unknown or inconsistent time, or unavailable measurements. Preserved separately and excluded from charts and alerts.">
    <ApiState {...query} />
    <DataTable caption="Preserved sensor records" rows={query.data?.items ?? []} columns={[
      { key: 'sourceRecordKey', header: 'Record' },
      { key: 'receivedAt', header: 'Received by server', render: displayTime },
      { key: 'payload', header: 'Original data', render: payload => <details><summary>View original record</summary><pre>{JSON.stringify(payload, null, 2)}</pre></details> },
    ]} />
    <button className="app-ui-button" disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button>
    <span aria-live="polite"> Page {page} </span>
    <button className="app-ui-button" disabled={query.loading || page >= (query.data?.pagination.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Next</button>
  </SectionCard>
}
