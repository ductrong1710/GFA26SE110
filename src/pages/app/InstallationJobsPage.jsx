import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import ApiState from '../../components/app/ApiState'
import JobAssignmentForm from '../../components/app/JobAssignmentForm'
import InstallationReports from '../../components/app/InstallationReports'
import InstallationReportForm from '../../components/app/InstallationReportForm'
import InstallationDeviceAllocation from '../../components/app/InstallationDeviceAllocation'
import RentalContractForm from '../../components/app/RentalContractForm'
import { DataTable, PageHeader, SectionCard, StatusBadge } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

export default function InstallationJobsPage() {
  const { can, user } = useAuth()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const query = useApiQuery(`installation-jobs:${page}`, signal => api.request('/api/installation-jobs', { query: { page }, signal }))
  async function open(id) {
    setPending(true); setError(''); setNotice(''); setSelected(null)
    try { setSelected(await api.request(`/api/installation-jobs/${id}`)) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function start() {
    if (pending) return
    setPending(true); setError(''); setNotice('')
    try {
      setSelected(await api.request(`/api/installation-jobs/${selected.id}/start`, { method: 'POST', body: { expectedRevision: selected.revision } }))
      query.reload(); setNotice('Installation work started.')
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <><PageHeader title="Installation jobs" description="Appointments, assigned work and installation history." />
    <ApiState loading={query.loading} error={query.error} />{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {selected && <SectionCard title={`Installation job #${selected.id}`} actions={<StatusBadge status={selected.status} />}>
      <h3>{selected.farmName}</h3><p>{selected.farmLocation}</p><p>Technician #{selected.technicianId} · Appointment: {displayTime(selected.scheduledAt)}</p>
      <ul>{selected.lines.map(line => <li key={line.id}>{line.quantity} × {line.deviceType} · {line.installationLocation}</li>)}</ul>
      {can(P.INSTALLATIONS_WORK) && selected.technicianId === user?.id && selected.status === 'ASSIGNED' && <button disabled={pending} onClick={start}>Start installation</button>}
      {can(P.INSTALLATIONS_REVIEW) && ['ASSIGNED', 'IN_PROGRESS', 'REWORK'].includes(selected.status) && <JobAssignmentForm key={`${selected.id}:${selected.revision}`} job={selected}
        onSaved={row => { setSelected(row); query.reload(); setNotice('Assignment updated.'); setError('') }} />}
      <h3>Job history</h3><ol>{selected.history.map(item => <li key={item.id}><strong>{item.action}</strong> · {displayTime(item.createdAt)} · User #{item.userId}
        <p>Technician #{item.technicianId} · Appointment: {displayTime(item.scheduledAt)}</p><p>{item.note}</p></li>)}</ol>
      <InstallationReports key={selected.id} job={selected} canConfirm={can(P.INSTALLATIONS_CONFIRM)} canReview={can(P.INSTALLATIONS_REVIEW)} onUpdated={row => { setSelected(row); query.reload() }} />
      {can(P.INSTALLATIONS_REVIEW) && ['ASSIGNED', 'IN_PROGRESS', 'REWORK'].includes(selected.status) && <InstallationDeviceAllocation key={`devices:${selected.id}:${selected.revision}`} job={selected} onSaved={() => open(selected.id)} />}
      {can(P.INSTALLATIONS_WORK) && selected.technicianId === user?.id && ['IN_PROGRESS', 'REWORK'].includes(selected.status) && <InstallationReportForm key={`report:${selected.id}:${selected.revision}`} job={selected} onSaved={() => open(selected.id)} />}
      {can(P.RENTALS_MANAGE) && selected.status === 'APPROVED' && <RentalContractForm key={`rental:${selected.id}`} jobId={selected.id} />}
    </SectionCard>}
    <DataTable caption="Installation jobs" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Job' }, { key: 'farmId', header: 'Farm' }, { key: 'technicianId', header: 'Technician' },
      { key: 'scheduledAt', header: 'Appointment', render: displayTime }, { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Open job #{row.id}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div>} />
  </>
}
