import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import ApiState from '../../components/app/ApiState'
import EvidenceImages from '../../components/app/EvidenceImages'
import ServiceJobAssignmentForm from '../../components/app/ServiceJobAssignmentForm'
import ServiceReportForm from '../../components/app/ServiceReportForm'
import ServiceReports from '../../components/app/ServiceReports'
import ServicePlan from '../../components/app/ServicePlan'
import { DataTable, PageHeader, SectionCard, StatusBadge } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

export default function ServiceJobsPage() {
  const { can, user } = useAuth()
  const [page, setPage] = useState(1)
  const [params] = useSearchParams()
  const [selectedId, setSelectedId] = useState(() => {
    const id = Number(params.get('jobId'))
    return Number.isSafeInteger(id) && id > 0 ? id : null
  })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const query = useApiQuery(`service-jobs:${page}`, signal => api.request('/api/service-jobs', { query: { page }, signal }))
  const detail = useApiQuery(`service-job:${selectedId}`, signal => api.request(`/api/service-jobs/${selectedId}`, { signal }), Boolean(selectedId))
  const selected = detail.data
  const setSelected = detail.replaceData
  function open(id) {
    setError('')
    if (id === selectedId) detail.reload()
    else setSelectedId(id)
  }
  async function start() {
    if (pending) return
    setPending(true); setError('')
    try { setSelected(await api.request(`/api/service-jobs/${selected.id}/start`, { method: 'POST', body: { expectedRevision: selected.revision } })); query.reload() }
    catch (cause) { setError(cause.message) } finally { setPending(false) }
  }
  return <><PageHeader title="Service jobs" description="Assigned repair and maintenance appointments." />
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    <ApiState loading={detail.loading} error={detail.error} reload={detail.reload} />
    {pending && <p role="status">Loading service job…</p>}{error && <p role="alert">{error}</p>}
    {selected && <SectionCard title={`Service job #${selected.id}`} actions={<StatusBadge status={selected.status} />}>
      <p>{selected.type} · {selected.deviceCode} · Farm #{selected.farmId}</p><p>{selected.installationLocation}</p>
      <p>Current device: {selected.currentDeviceCode ?? selected.deviceCode}</p>
      {selected.replacements?.length > 0 && <section aria-label="Device replacement history"><h3>Replacement history</h3><ol>{selected.replacements.map(item => <li key={item.id}>
        <strong>{item.oldDeviceCode} → {item.newDeviceCode}</strong> · {displayTime(item.createdAt)} · Technician #{item.technicianId}<p>{item.note}</p>
      </li>)}</ol></section>}
      <p>Technician #{selected.technicianId} · {displayTime(selected.scheduledAt)}</p>
      <p style={{ whiteSpace: 'pre-wrap' }}>{selected.description}</p><EvidenceImages images={selected.images} />
      {can(P.SERVICE_JOBS_WORK) && selected.technicianId === user?.id && selected.status === 'ASSIGNED' && <button disabled={pending} onClick={start}>Start service work</button>}
      {can(P.SERVICE_JOBS_MANAGE) && ['ASSIGNED', 'IN_PROGRESS', 'REWORK'].includes(selected.status) && <ServiceJobAssignmentForm key={`${selected.id}:${selected.revision}`} job={selected} onSaved={row => { setSelected(row); query.reload() }} />}
      <h3>Job history</h3><ol>{selected.history.map(item => <li key={item.id}><strong>{item.action}</strong> · {displayTime(item.createdAt)} · User #{item.userId}
        <p>Technician #{item.technicianId} · {displayTime(item.scheduledAt)}</p><p>{item.note}</p></li>)}</ol>
      <ServiceReports key={selected.id} job={selected} canConfirm={can(P.SERVICE_JOBS_CONFIRM)} canReview={can(P.SERVICE_JOBS_MANAGE)} onUpdated={row => { setSelected(row); query.reload() }} />
      {(can(P.SERVICE_JOBS_MANAGE) || (can(P.SERVICE_JOBS_WORK) && selected.technicianId === user?.id)) && <ServicePlan key={`plan:${selected.id}:${selected.revision}`} job={selected}
        canWork={can(P.SERVICE_JOBS_WORK) && selected.technicianId === user?.id} canReview={can(P.SERVICE_JOBS_MANAGE)}
        onUpdated={row => { setSelected(row); query.reload() }}>
        {ready => ready && can(P.SERVICE_JOBS_WORK) && selected.technicianId === user?.id && ['IN_PROGRESS', 'REWORK'].includes(selected.status) &&
          <ServiceReportForm job={selected} onSaved={() => { open(selected.id); query.reload() }} />}
      </ServicePlan>}
    </SectionCard>}
    <DataTable caption="Service jobs" rows={query.data?.items ?? []} loading={query.loading} columns={[
      { key: 'id', header: 'Job' }, { key: 'deviceCode', header: 'Device' }, { key: 'type', header: 'Type' },
      { key: 'scheduledAt', header: 'Appointment', render: displayTime }, { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Open service job #{row.id}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page}</span>
      <button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div>} />
  </>
}
