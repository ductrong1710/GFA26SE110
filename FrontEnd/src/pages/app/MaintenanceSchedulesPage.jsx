import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import ApiState from '../../components/app/ApiState'
import MaintenanceScheduleForm from '../../components/app/MaintenanceScheduleForm'
import MaintenanceHistory from '../../components/app/MaintenanceHistory'
import { DataTable, PageHeader, SectionCard } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

export default function MaintenanceSchedulesPage() {
  const { can } = useAuth()
  const allowed = can(P.MAINTENANCE_MANAGE)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState(null)
  const [creating, setCreating] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const query = useApiQuery(`maintenance:${page}:${search}`, signal => api.request('/api/maintenance-schedules', { query: { page, search }, signal }), allowed)
  async function open(id) {
    setPending(true); setError(''); setMessage(''); setSelected(null); setCreating(false)
    try { setSelected(await api.request(`/api/maintenance-schedules/${id}`)) }
    catch (cause) { setError(cause.message) } finally { setPending(false) }
  }
  function saved(row) { setSelected(row); setCreating(false); setMessage('Maintenance schedule saved.'); query.reload() }
  async function disable() {
    if (pending) return
    setPending(true); setError(''); setMessage('')
    try { saved(await api.request(`/api/maintenance-schedules/${selected.id}/disable`, { method: 'POST', body: { expectedRevision: selected.revision } })) }
    catch (cause) { setError(cause.message) } finally { setPending(false) }
  }
  if (!allowed) return <p role="alert">Administrator access required.</p>
  return <><PageHeader title="Maintenance schedules" description="Plan recurring device inspections and assign a technician." />
    <button className="app-ui-button" disabled={pending} onClick={() => { setSelected(null); setCreating(true); setMessage(''); setError('') }}>New schedule</button>
    <label>Search policy<input value={search} onChange={event => { setSearch(event.target.value); setPage(1) }} /></label>
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    {pending && <p role="status">Loading schedule…</p>}{message && <p role="status">{message}</p>}{error && <p role="alert">{error}</p>}
    {selected && <SectionCard title={`Schedule #${selected.id}`}>
      <p>{selected.enabled ? 'Enabled' : 'Disabled'} · Next maintenance: {displayTime(selected.nextDueAt)}</p>
      {selected.enabled && <button disabled={pending} onClick={disable}>Disable schedule</button>}
      {error && <button disabled={pending} onClick={() => open(selected.id)}>Reload schedule and discard draft</button>}
      <MaintenanceHistory key={`${selected.id}:${selected.revision}`} scheduleId={selected.id} />
    </SectionCard>}
    {(selected || creating) && <MaintenanceScheduleForm key={selected ? `${selected.id}:${selected.revision}` : 'new'} schedule={selected} onSaved={saved} onReload={() => open(selected.id)} onBusyChange={setPending} busy={pending} />}
    <DataTable caption="Maintenance schedules" rows={query.data?.items ?? []} loading={query.loading} columns={[
      { key: 'policy', header: 'Service policy' }, { key: 'deviceType', header: 'Scope', render: (value, row) => value ?? `Rental device #${row.rentalDeviceAssignmentId}` },
      { key: 'intervalDays', header: 'Interval (days)' }, { key: 'nextDueAt', header: 'Next maintenance', render: displayTime },
      { key: 'enabled', header: 'Status', render: value => value ? 'Enabled' : 'Disabled' },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Open schedule #{row.id}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page}</span>
      <button disabled={query.loading || page * 20 >= (query.data?.pagination?.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div>} />
  </>
}
