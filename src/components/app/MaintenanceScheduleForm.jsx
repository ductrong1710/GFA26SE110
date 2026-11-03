import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'

function localTime(value) {
  if (!value) return ''
  const date = new Date(value)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}
export default function MaintenanceScheduleForm({ schedule, onSaved, onReload, onBusyChange, busy = false }) {
  const [values, setValues] = useState({ policy: schedule?.policy ?? '', deviceType: schedule?.deviceType ?? 'UAV',
    technicianId: schedule?.technicianId ?? '', intervalDays: schedule?.intervalDays ?? 30,
    nextDueAt: localTime(schedule?.nextDueAt), enabled: schedule?.enabled ?? true })
  const [mode, setMode] = useState(schedule?.rentalDeviceAssignmentId ? 'CURRENT' : 'TYPE')
  const [page, setPage] = useState(1)
  const [contractId, setContractId] = useState('')
  const [assignmentId, setAssignmentId] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const technicians = useApiQuery('maintenance-technicians', signal => listAll('/api/installation-jobs/technicians', {}, signal))
  const contracts = useApiQuery(`maintenance-rentals:${page}`, signal => api.request('/api/rental-contracts', { query: { page }, signal }), mode === 'DEVICE')
  const contract = useApiQuery(`maintenance-rental:${contractId}`, signal => api.request(`/api/rental-contracts/${contractId}`, { signal }), mode === 'DEVICE' && Boolean(contractId))
  const devices = contract.data?.status !== 'ENDED' ? (contract.data?.devices ?? []).filter(row => !row.returnedAt) : []
  const field = name => ({ value: values[name], onChange: event => setValues({ ...values, [name]: event.target.value }) })
  async function save(event) {
    event.preventDefault(); if (pending || busy) return
    setError('')
    if (mode === 'DEVICE' && !devices.some(row => row.id === Number(assignmentId))) { setError('Choose a currently assigned rental device.'); return }
    setPending(true); onBusyChange?.(true)
    try {
      onSaved(await api.request(schedule ? `/api/maintenance-schedules/${schedule.id}` : '/api/maintenance-schedules', {
        method: schedule ? 'PUT' : 'POST', body: { policy: values.policy.trim(), intervalDays: Number(values.intervalDays),
          nextDueAt: new Date(values.nextDueAt).toISOString(), technicianId: Number(values.technicianId), enabled: values.enabled,
          expectedRevision: schedule?.revision ?? 0, deviceType: mode === 'TYPE' ? values.deviceType : null,
          rentalDeviceAssignmentId: mode === 'CURRENT' ? schedule.rentalDeviceAssignmentId : mode === 'DEVICE' ? Number(assignmentId) : null },
      }))
    } catch (cause) { setError(cause.message) } finally { setPending(false); onBusyChange?.(false) }
  }
  return <form className="management-form" aria-label="Maintenance schedule" onSubmit={save}>
    <ApiState loading={technicians.loading} error={technicians.error} reload={technicians.reload} />
    <fieldset disabled={pending || busy} className="management-form"><legend>{schedule ? 'Edit schedule' : 'Create maintenance schedule'}</legend>
      <label>Schedule scope<select value={mode} onChange={event => { setMode(event.target.value); setContractId(''); setAssignmentId('') }}>
        <option value="TYPE">Device type</option><option value="DEVICE">Choose rental device</option>
        {schedule?.rentalDeviceAssignmentId && <option value="CURRENT">Keep current rental device #{schedule.rentalDeviceAssignmentId}</option>}
      </select></label>
      {mode === 'TYPE' && <label>Device type<select {...field('deviceType')}><option value="UAV">UAV</option><option value="GATEWAY">Gateway</option><option value="SENSOR">Sensor</option></select></label>}
      {mode === 'DEVICE' && <>
        <ApiState loading={contracts.loading} error={contracts.error} reload={contracts.reload} />
        <label>Rental contract<select required value={contractId} onChange={event => { setContractId(event.target.value); setAssignmentId('') }}>
          <option value="">Choose contract</option>{(contracts.data?.items ?? []).filter(row => row.status !== 'ENDED').map(row => <option key={row.id} value={row.id}>Contract #{row.id} · Farm #{row.farmId}</option>)}
        </select></label>
        <div className="management-row-actions"><button type="button" disabled={page === 1 || contracts.loading} onClick={() => { setPage(page - 1); setContractId(''); setAssignmentId('') }}>Previous contracts</button><span>Page {page}</span>
          <button type="button" disabled={contracts.loading || page * 20 >= (contracts.data?.pagination?.totalItems ?? 0)} onClick={() => { setPage(page + 1); setContractId(''); setAssignmentId('') }}>Next contracts</button></div>
        <ApiState loading={contract.loading} error={contract.error} reload={contract.reload} />
        <label>Rental device<select required value={assignmentId} onChange={event => setAssignmentId(event.target.value)} disabled={!contract.data || contract.loading}>
          <option value="">Choose device</option>{devices.map(row => <option key={row.id} value={row.id}>{row.code} · {row.name}</option>)}
        </select></label>
      </>}
      <label>Service policy<textarea required maxLength={2000} {...field('policy')} /></label>
      <label>Interval (days)<input type="number" required min="1" max="3650" step="1" {...field('intervalDays')} /></label>
      <label>Next maintenance<input type="datetime-local" required {...field('nextDueAt')} /></label>
      <label>Technician<select required {...field('technicianId')}><option value="">Choose active technician</option>{(technicians.data ?? []).map(row => <option key={row.id} value={row.id}>{row.fullName}</option>)}</select></label>
      <label><input type="checkbox" checked={values.enabled} onChange={event => setValues({ ...values, enabled: event.target.checked })} />Enabled</label>
      <button className="app-ui-button" disabled={technicians.loading || !!technicians.error || (mode === 'DEVICE' && (contract.loading || !!contract.error))}>{schedule ? 'Save schedule' : 'Create schedule'}</button>
    </fieldset>
    {error && <p role="alert">{error}</p>}
    {error && schedule && <button type="button" disabled={pending} onClick={onReload}>Reload schedule and discard draft</button>}
  </form>
}
