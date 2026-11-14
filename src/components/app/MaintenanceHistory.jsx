import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import { DataTable, TablePagination } from './ui'
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
    ]} footer={(query.data?.pagination?.totalItems ?? 0) > 0 && <TablePagination page={page} totalPages={query.data?.pagination?.totalPages} pageSize={query.data?.pagination?.pageSize} totalItems={query.data?.pagination?.totalItems} onPageChange={setPage} disabled={query.loading} label="Maintenance history pagination" />} />
  </section>
}
