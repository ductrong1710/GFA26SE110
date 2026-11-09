import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import { DataTable } from './ui'
import { displayTime } from '../../utils/display'

export default function MaintenanceHistory({ scheduleId }) {
  const [page, setPage] = useState(1)
  const query = useApiQuery(`maintenance-history:${scheduleId}:${page}`, signal => api.request(`/api/maintenance-schedules/${scheduleId}/occurrences`, { query: { page }, signal }))
  return <section aria-label="Maintenance work history"><h3>Generated maintenance work</h3>
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    <DataTable caption="Maintenance work history" rows={query.data?.items ?? []} loading={query.loading} columns={[
      { key: 'jobId', header: 'Service job', render: value => <Link to={`/app/service-jobs?jobId=${value}`}>{value}</Link> },
      { key: 'deviceCode', header: 'Device' }, { key: 'dueAt', header: 'Due date', render: displayTime },
      { key: 'createdAt', header: 'Created', render: displayTime }, { key: 'policy', header: 'Recorded policy' }, { key: 'status', header: 'Job status' },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous work</button><span>Page {page}</span>
      <button disabled={query.loading || page * 20 >= (query.data?.pagination?.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next work</button></div>} />
  </section>
}
